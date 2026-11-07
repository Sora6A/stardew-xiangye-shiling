using XiangyeShiling.Transactions;

namespace XiangyeShiling.Networking;

public enum HostRequestBeginStatus
{
    Started, Replayed, InvalidRequest, WrongSession, WrongActor,
    IntentMismatch, InProgress, CapacityExceeded, Quarantined
}

public enum HostRequestCompletionStatus { Recorded, Retryable, InvalidLease, Quarantined }

/// <summary>
/// 主机已核验提交的历史回执。Request 保留整份原意图，避免把旧设备的确认应用到新目标。
/// AppliedRevision 是该次提交后的修订，不代表设备当前修订；回执重放不得覆盖当前设备状态。
/// </summary>
public sealed record HostRequestReceipt(MachineRequestEnvelope Request, long AppliedRevision)
{
    public Guid RequestId => this.Request.RequestId;
    public long ActorId => this.Request.ActorId;
    public Guid SessionId => this.Request.SessionId;
    public Guid InstanceId => this.Request.InstanceId;
}

/// <summary>
/// 一次执行尝试的能力令牌。仅账本创建，完成时按当前令牌引用核验；失败重试会取得新令牌。
/// 此令牌不证明主机权限、库存所有权或事务成功，不能作为客户端可提交的消息字段。
/// </summary>
public sealed class HostRequestLease
{
    public MachineRequestEnvelope Request { get; }
    internal HostRequestLease(MachineRequestEnvelope request) => this.Request = request;
}

/// <summary>Started 只允许调用者继续准备事务；只有可信提交器的 Applied 才能形成成功回执。</summary>
public sealed record HostRequestBeginResult(HostRequestBeginStatus Status, HostRequestLease? Lease = null, HostRequestReceipt? Receipt = null);

/// <summary>
/// P1 主机会话内的意图绑定与成功回执账本，不读写游戏、背包或设备，也不注册网络处理器。
/// 未来写入适配器应在游戏主线程构造并调用；身份须取自已建立信任的来源，在 HostRequestGate 准入后使用。
/// SMAPI FromPlayerID 元数据本身不满足认证前置条件；P1-NET-02 只读桥不调用本账本。
/// </summary>
/// <remarks>
/// 同 actor/request ID 永久绑定完整不可变意图；可重试失败仅释放活动令牌，不能改绑修订或目标。
/// 成功记录在本会话内不淘汰；容量耗尽拒绝新 ID，避免遗忘历史后重复执行。断线不能清空账本。
/// 结束世界会话后创建新账本和新 SessionId；此处不保证进程重启后的确认恢复或远端库存原子性。
/// 方法和可变状态读取均要求构造线程，不使用线程锁冒充游戏主线程或分布式所有权。
/// </remarks>
public sealed class HostRequestLedger
{
    private readonly int OwnerThread = Environment.CurrentManagedThreadId;
    private readonly Dictionary<(long ActorId, Guid RequestId), Entry> Entries = new();
    private bool Quarantined;
    public Guid SessionId { get; }
    public int Capacity { get; }
    public int BoundRequestCount { get { this.RequireOwnerThread(); return this.Entries.Count; } }
    public bool IsQuarantined { get { this.RequireOwnerThread(); return this.Quarantined; } }

    public HostRequestLedger(Guid sessionId, int capacity = 256)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("A host session must have an identity.", nameof(sessionId));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.SessionId = sessionId;
        this.Capacity = capacity;
    }

    /// <summary>
    /// authenticatedActorId 必须来自可信传输上下文，不能再次取自 Request.ActorId。
    /// 本地防御检查不代替主机角色、位置、配方或真实库存所有权的准入核验。
    /// </summary>
    public HostRequestBeginResult TryBegin(MachineRequestEnvelope? request, long authenticatedActorId)
    {
        this.RequireOwnerThread();
        if (request is null || !MachineRequestCodec.IsValid(request)) return new(HostRequestBeginStatus.InvalidRequest);
        if (request.SessionId != this.SessionId) return new(HostRequestBeginStatus.WrongSession);
        if (authenticatedActorId == 0 || request.ActorId != authenticatedActorId) return new(HostRequestBeginStatus.WrongActor);
        if (this.Quarantined) return new(HostRequestBeginStatus.Quarantined);

        var key = (request.ActorId, request.RequestId);
        if (!this.Entries.TryGetValue(key, out Entry? entry))
        {
            if (this.Entries.Count >= this.Capacity) return new(HostRequestBeginStatus.CapacityExceeded);
            entry = new Entry(request);
            this.Entries.Add(key, entry);
        }
        else if (entry.Request != request) return new(HostRequestBeginStatus.IntentMismatch);

        if (entry.Receipt is not null) return new(HostRequestBeginStatus.Replayed, Receipt: entry.Receipt);
        if (entry.Lease is not null) return new(HostRequestBeginStatus.InProgress);
        entry.Lease = new HostRequestLease(entry.Request);
        return new(HostRequestBeginStatus.Started, entry.Lease);
    }

    /// <summary>
    /// result 和 appliedRevision 只能来自可信提交器的最终核验，不能传 Decision.Accepted 或客户端确认。
    /// Applied 后回执数据矛盾时，真实写入可能已经发生，必须隔离，不能释放成可重试请求。
    /// </summary>
    public HostRequestCompletionStatus Complete(HostRequestLease? lease, CommitResult result, long? appliedRevision = null)
    {
        this.RequireOwnerThread();
        if (this.Quarantined) return HostRequestCompletionStatus.Quarantined;
        if (!this.TryGetActive(lease, out Entry? entry)) return HostRequestCompletionStatus.InvalidLease;

        if (result == CommitResult.Applied)
        {
            if (entry!.Request.ExpectedRevision == long.MaxValue
                || appliedRevision is null || appliedRevision != entry.Request.ExpectedRevision + 1)
                return this.Quarantine();
            entry.Receipt = new HostRequestReceipt(entry.Request, appliedRevision.Value);
            entry.Lease = null;
            return HostRequestCompletionStatus.Recorded;
        }
        if (result is CommitResult.Conflict or CommitResult.Reentrant or CommitResult.PreparationFailed or CommitResult.RolledBack)
        {
            if (appliedRevision is not null) return this.Quarantine();
            entry!.Lease = null;
            return HostRequestCompletionStatus.Retryable;
        }
        // RecoveryFailed / Quarantined，以及未知提交结果都不能证明两侧已恢复。
        return this.Quarantine();
    }

    /// <summary>
    /// 规则拒绝且尚未写入时释放执行尝试（例如缺料或满包）。调用者负责证明没有写入；
    /// 已进入提交器的失败须使用 Complete 的明确结果，不能用此方法隐藏恢复失败。
    /// </summary>
    public HostRequestCompletionStatus ReleaseForRetry(HostRequestLease? lease)
    {
        this.RequireOwnerThread();
        if (this.Quarantined) return HostRequestCompletionStatus.Quarantined;
        if (!this.TryGetActive(lease, out Entry? entry)) return HostRequestCompletionStatus.InvalidLease;
        entry!.Lease = null;
        return HostRequestCompletionStatus.Retryable;
    }

    /// <summary>无法确认恢复时终止本会话所有执行；保留意图与回执供诊断，但不再接受重放或新写入。</summary>
    public HostRequestCompletionStatus Quarantine()
    {
        this.RequireOwnerThread();
        this.Quarantined = true;
        foreach (Entry entry in this.Entries.Values) entry.Lease = null;
        return HostRequestCompletionStatus.Quarantined;
    }

    private bool TryGetActive(HostRequestLease? lease, out Entry? entry)
    {
        entry = null;
        return lease is not null
            && this.Entries.TryGetValue((lease.Request.ActorId, lease.Request.RequestId), out entry)
            && ReferenceEquals(entry.Lease, lease);
    }

    private void RequireOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != this.OwnerThread)
            throw new InvalidOperationException("Host request ledger must be used on its construction thread.");
    }

    private sealed class Entry
    {
        public MachineRequestEnvelope Request { get; }
        public HostRequestLease? Lease { get; set; }
        public HostRequestReceipt? Receipt { get; set; }
        public Entry(MachineRequestEnvelope request) => this.Request = request;
    }
}
