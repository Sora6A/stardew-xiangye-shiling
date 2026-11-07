namespace XiangyeShiling.Networking;

// 新值只追加：诊断旧数值保持含义；线上Welcome/Hello格式没有变化。
public enum ProtocolReceiveKind { Ignored, RateLimited, InvalidMessage, HelloAccepted, WelcomeAccepted, RejectionProduced, RejectionAccepted, WelcomeRejected }
public sealed record ProtocolObservation(ProtocolReceiveKind Kind, bool BodyReadAttempted,
    Guid? RequestId = null, ProtocolRejection? Reason = null, Guid? Challenge = null);
public sealed record ProtocolPeerStatus(long PlayerId, Guid ConnectionId, bool IsHost, bool Ready, Guid Challenge);
/// <summary>实际入站peer固定窗口的本地起点和计数；超额保持Count=8/Allowed=false，未读正文。不是网络时钟。</summary>
public sealed record ProtocolQuotaWindow(long StartMilliseconds, int Count, bool Allowed);
public enum ProtocolTraceKind { Reset, Processed, StaleQueue, SendAttempt, SendSuppressed, ProbeAttempt, Faulted, WelcomeReplayAttempt }

/// <summary>只记固定类型、标识和结果；无消息原文、库存、设备raw、玩家名或路径。Sequence在进程内递增。</summary>
public sealed record ProtocolTrace(long Sequence, long Milliseconds, long WorldGeneration, long DisconnectGeneration,
    int ThreadId, ProtocolTraceKind Kind, long? PeerId, string? MessageType, ProtocolObservation? Observation,
    ProtocolQuotaWindow? Quota = null, Guid? ConnectionId = null);

/// <summary>独立快照只供本地诊断，WritesEnabled恒false；LastSequence/Evicted用于拒绝把丢失记录当完整证据。</summary>
public sealed record ProtocolDiagnosticSnapshot(Guid RunId, bool ProbeConfigured, bool ProbeAllowed,
    bool Active, bool IsHost, bool Faulted, bool WritesEnabled, long LocalPlayerId, Guid? SessionId,
    long WorldGeneration, long DisconnectGeneration, int QueueCount, long QueueRejected,
    long LastSequence, long Evicted, IReadOnlyList<ProtocolPeerStatus> Peers, IReadOnlyList<ProtocolTrace> Trace);

/// <summary>
/// 构造线程拥有的有界诊断尾部。满时淘汰最旧的诊断并显式计数；它不是成功账本，不能参与重放或授权。
/// Snapshot复制容器引用，record本身不可变；返回标题不清尾部，以便同进程核对Reset/旧队列事件。
/// </summary>
public sealed class ProtocolTraceBuffer
{
    private readonly Thread Owner = Thread.CurrentThread;
    private readonly Queue<ProtocolTrace> Items = new();
    private readonly int Capacity;
    private long Sequence;
    private long Removed;
    public long LastSequence { get { this.RequireOwner(); return this.Sequence; } }
    public long Evicted { get { this.RequireOwner(); return this.Removed; } }

    public ProtocolTraceBuffer(int capacity = 256)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.Capacity = capacity;
    }

    public void Record(long milliseconds, long worldGeneration, long disconnectGeneration,
        ProtocolTraceKind kind, long? peerId = null, string? messageType = null, ProtocolObservation? observation = null,
        ProtocolQuotaWindow? quota = null, Guid? connectionId = null)
    {
        this.RequireOwner();
        var value = new ProtocolTrace(checked(++this.Sequence), milliseconds, worldGeneration, disconnectGeneration,
            Environment.CurrentManagedThreadId, kind, peerId, messageType, observation, quota, connectionId);
        if (this.Items.Count == this.Capacity) { this.Items.Dequeue(); this.Removed++; }
        this.Items.Enqueue(value);
    }

    public IReadOnlyList<ProtocolTrace> Snapshot() { this.RequireOwner(); return this.Items.ToArray(); }
    private void RequireOwner()
    {
        if (!ReferenceEquals(this.Owner, Thread.CurrentThread)) throw new InvalidOperationException("Trace buffer belongs to its construction thread.");
    }
}
