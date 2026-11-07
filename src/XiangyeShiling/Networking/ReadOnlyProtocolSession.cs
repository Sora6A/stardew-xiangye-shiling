namespace XiangyeShiling.Networking;

/// <summary>
/// 游戏适配器提供当前已连接 peer 元数据；HasMatchingMod 指完全相同的模组版本。
/// SMAPI 的消息来源是序列化元数据，这个快照只支持正常协议路由，不能证明远端库存可写权。
/// </summary>
public sealed record ProtocolPeer(long PlayerId, bool IsHost, bool HasMatchingMod, bool IsSplitScreen,
    Guid ConnectionId = default);

/// <summary>
/// 单一游戏线程拥有的只读会话。每个主机世界有新 SessionId；客户端用每次连接的新 challenge
/// 匹配 welcome。最多32个匹配 peer，每 peer 每秒8个已知入站消息，先限流再调用读取委托。
/// 本层无 Machines/Transactions/游戏引用；连合法 Start/Collect 也只返回 WritesDisabled。
/// </summary>
public sealed class ReadOnlyProtocolSession
{
    public const int MaxPeers = 32;
    public const int MessagesPerWindow = 8;
    public const long WindowMilliseconds = 1000;
    public const long HelloRetryMilliseconds = 5000;
    private readonly Thread Owner = Thread.CurrentThread;
    private readonly Dictionary<long, PeerState> Peers = new();
    private readonly long LocalPlayerId;
    private readonly bool IsHost;
    private readonly string ModId;
    private readonly Guid HostSessionId;
    private long LastClock = -1;

    /// <summary>仅诊断状态；不得把 RemoteSessionId 非空当成开放库存写入。没有存档字段。</summary>
    public Guid? RemoteSessionId { get; private set; }
    public ProtocolRejected? LastRejection { get; private set; }
    public int PeerCount { get { this.RequireOwner(); return this.Peers.Count; } }
    public Guid? ActiveSessionId { get { this.RequireOwner(); return this.IsHost ? this.HostSessionId : this.RemoteSessionId; } }
    public ProtocolObservation LastObservation { get; private set; } = new(ProtocolReceiveKind.Ignored, false);
    private ProtocolQuotaWindow? Quota;
    /// <summary>仅构造线程读取。来自实际TakeQuota，筛选拒绝时为null，不用时间跨度猜测是否同一窗口。</summary>
    public ProtocolQuotaWindow? LastQuota { get { this.RequireOwner(); return this.Quota; } }
    public IReadOnlyList<ProtocolPeerStatus> ReadPeerStatus()
    {
        this.RequireOwner();
        return this.Peers.Values.Select(p => new ProtocolPeerStatus(p.Snapshot.PlayerId, p.Snapshot.ConnectionId,
            p.Snapshot.IsHost, p.Ready, p.Challenge)).ToArray();
    }

    public ReadOnlyProtocolSession(long localPlayerId, bool isHost, string modId, Guid hostSessionId)
    {
        if (localPlayerId == 0 || string.IsNullOrWhiteSpace(modId) || hostSessionId == Guid.Empty)
            throw new ArgumentException("Invalid local read-only protocol context.");
        this.LocalPlayerId = localPlayerId;
        this.IsHost = isHost;
        this.ModId = modId;
        this.HostSessionId = hostSessionId;
    }

    /// <summary>
    /// 每帧先刷新实际连接表，断开/角色/版本变化删除旧握手和限流状态。客户端只接受唯一远端主机。
    /// 返回 hello 仅表示应尝试发送；SendMessage 返回 void，并不保证送达，未握手时5秒重试。
    /// 毫秒值来自单调时钟；倒退或负值不会触发读取、发包或重新授予额度。
    /// </summary>
    public IReadOnlyList<OutboundProtocolMessage> SynchronizePeers(IEnumerable<ProtocolPeer> snapshots, long now)
    {
        this.RequireOwner();
        if (!this.AdvanceClock(now)) return Array.Empty<OutboundProtocolMessage>();
        var eligible = snapshots.Where(p => p.PlayerId != 0 && p.PlayerId != this.LocalPlayerId
                && p.HasMatchingMod && !p.IsSplitScreen && (this.IsHost ? !p.IsHost : p.IsHost))
            .GroupBy(p => p.PlayerId).Where(g => g.Count() == 1).Select(g => g.Single())
            .OrderBy(p => p.PlayerId).Take(MaxPeers).ToDictionary(p => p.PlayerId);
        if (!this.IsHost && eligible.Count != 1) eligible.Clear();
        foreach (long id in this.Peers.Keys.Where(id => !eligible.TryGetValue(id, out var peer)
                     || peer != this.Peers[id].Snapshot).ToArray())
        {
            this.Peers.Remove(id);
            if (!this.IsHost) { this.RemoteSessionId = null; this.LastRejection = null; }
        }
        var outbound = new List<OutboundProtocolMessage>();
        foreach (var peer in eligible.Values)
        {
            if (!this.Peers.TryGetValue(peer.PlayerId, out var state))
                this.Peers.Add(peer.PlayerId, state = new PeerState(peer, now));
            if (!this.IsHost && !state.Ready && now - state.LastHello >= HelloRetryMilliseconds)
            {
                // Keep the same pending challenge on retry so a delayed matching reply is still usable.
                state.LastHello = now;
                outbound.Add(new(peer.PlayerId, ReadOnlyProtocolCodec.HelloType,
                    ReadOnlyProtocolCodec.Write(new ProtocolHello(1, state.Challenge, this.LocalPlayerId))));
            }
        }
        return outbound;
    }

    /// <summary>
    /// 只消费已同步的 peer、本模组和角色允许的消息。readBody 在通过筛选/额度后才执行，
    /// 所有权属于调用线程；读取失败由适配器转换为 null。未知来源/类型/超额输入静默丢弃以免放大流量。
    /// 响应 RequestId 只用于相关性，不是成功账本；不创建 lease，也不调用准入/提交。
    /// </summary>
    public OutboundProtocolMessage? Receive(long fromPlayerId, string fromModId, string messageType,
        Func<string?> readBody, long now)
    {
        this.RequireOwner();
        this.LastObservation = new(ProtocolReceiveKind.Ignored, false);
        this.Quota = null;
        if (!this.AdvanceClock(now) || fromModId != this.ModId || !this.Peers.TryGetValue(fromPlayerId, out var peer)
            || !(this.IsHost ? messageType is ReadOnlyProtocolCodec.HelloType or ReadOnlyProtocolCodec.RequestType
                : messageType is ReadOnlyProtocolCodec.WelcomeType or ReadOnlyProtocolCodec.RejectedType)) return null;
        this.Quota = peer.TakeQuota(now);
        if (!this.Quota.Allowed) { this.LastObservation = new(ProtocolReceiveKind.RateLimited, false); return null; }
        this.LastObservation = new(ProtocolReceiveKind.InvalidMessage, true);
        string? json = readBody();
        if (this.IsHost)
        {
            if (messageType == ReadOnlyProtocolCodec.HelloType)
            {
                if (!ReadOnlyProtocolCodec.TryReadHello(json, out var hello) || hello!.ActorId != fromPlayerId) return null;
                peer.Ready = true;
                this.LastObservation = new(ProtocolReceiveKind.HelloAccepted, true, Challenge: hello.Challenge);
                return new(fromPlayerId, ReadOnlyProtocolCodec.WelcomeType,
                    ReadOnlyProtocolCodec.Write(new ProtocolWelcome(1, MachineRequestEnvelope.CurrentProtocol,
                        hello.Challenge, this.HostSessionId, this.LocalPlayerId, false)));
            }
            if (!MachineRequestCodec.TryRead(json, out var request))
            {
                this.LastObservation = new(ProtocolReceiveKind.RejectionProduced, true, Reason: ProtocolRejection.InvalidRequest);
                return this.Reject(fromPlayerId, null, ProtocolRejection.InvalidRequest);
            }
            var reason = request!.ActorId != fromPlayerId ? ProtocolRejection.ActorMismatch
                : request.SessionId != this.HostSessionId ? ProtocolRejection.WrongSession
                : !peer.Ready ? ProtocolRejection.HandshakeRequired : ProtocolRejection.WritesDisabled;
            this.LastObservation = new(ProtocolReceiveKind.RejectionProduced, true, request.RequestId, reason);
            return this.Reject(fromPlayerId, request.RequestId, reason);
        }
        if (messageType == ReadOnlyProtocolCodec.WelcomeType)
        {
            if (ReadOnlyProtocolCodec.TryReadWelcome(json, out var welcome))
            {
                // 即使当前已Ready也记录实际读入的合法旧challenge；只改变诊断，不放宽准入。
                // 未Ready的旧challenge与Ready后的重复Welcome均不得覆盖当前会话/拒绝状态。
                this.LastObservation = new(ProtocolReceiveKind.WelcomeRejected, true, Challenge: welcome!.Challenge);
                if (!peer.Ready && welcome.HostPlayerId == fromPlayerId && welcome.Challenge == peer.Challenge)
                {
                    peer.Ready = true;
                    this.RemoteSessionId = welcome.SessionId;
                    this.LastRejection = null;
                    this.LastObservation = new(ProtocolReceiveKind.WelcomeAccepted, true, Challenge: welcome.Challenge);
                }
            }
        }
        else if (peer.Ready && ReadOnlyProtocolCodec.TryReadRejected(json, out var rejected)
                 && rejected!.SessionId == this.RemoteSessionId && rejected.ActorId == this.LocalPlayerId)
        {
            this.LastRejection = rejected;
            this.LastObservation = new(ProtocolReceiveKind.RejectionAccepted, true, rejected.RequestId, rejected.Reason);
        }
        return null;
    }

    private OutboundProtocolMessage Reject(long actorId, Guid? requestId, ProtocolRejection reason) =>
        new(actorId, ReadOnlyProtocolCodec.RejectedType,
            ReadOnlyProtocolCodec.Write(new ProtocolRejected(1, this.HostSessionId, requestId, actorId, reason)));

    private bool AdvanceClock(long now)
    {
        if (now < 0 || now < this.LastClock) return false;
        this.LastClock = now;
        return true;
    }

    private void RequireOwner()
    {
        if (!ReferenceEquals(Thread.CurrentThread, this.Owner))
            throw new InvalidOperationException("Read-only protocol state belongs to its construction thread.");
    }

    private sealed class PeerState
    {
        public ProtocolPeer Snapshot { get; }
        public Guid Challenge { get; } = Guid.NewGuid();
        public bool Ready { get; set; }
        public long LastHello { get; set; }
        private long WindowStarted;
        private int Count;

        public PeerState(ProtocolPeer snapshot, long now)
        {
            this.Snapshot = snapshot;
            this.WindowStarted = now;
            this.LastHello = now - HelloRetryMilliseconds;
        }

        public ProtocolQuotaWindow TakeQuota(long now)
        {
            if (now - this.WindowStarted >= WindowMilliseconds) { this.WindowStarted = now; this.Count = 0; }
            if (this.Count == MessagesPerWindow) return new(this.WindowStarted, this.Count, false);
            this.Count++;
            return new(this.WindowStarted, this.Count, true);
        }
    }
}
