namespace XiangyeShiling.Networking;

/// <summary>受控重放使用的本进程旧Welcome；Json只在桥内部使用，不写入报告或日志。</summary>
public sealed record PreviousWelcomeReplay(OutboundProtocolMessage Message, Guid PreviousConnectionId,
    Guid CurrentConnectionId, Guid PreviousChallenge, Guid CurrentChallenge);

/// <summary>
/// P1-NET-02默认禁用诊断的有界、主线程内存。调用者必须只在真实SendMessage成功返回后ObserveSent；
/// 本层不发送消息。每peer只保留相邻两次连接的本机Welcome，最多32项，每新连接最多取用一次。
/// 世界/标题/故障Reset由桥丢弃整个实例；不能从磁盘、调用参数或其他进程装入旧载荷。
/// </summary>
public sealed class WelcomeReplayBuffer
{
    private readonly Thread Owner = Thread.CurrentThread;
    private readonly long HostId;
    private readonly Guid SessionId;
    private readonly Dictionary<long, Entry> Entries = new();
    public int Count { get { this.RequireOwner(); return this.Entries.Count; } }

    public WelcomeReplayBuffer(long hostId, Guid sessionId)
    {
        if (hostId == 0 || sessionId == Guid.Empty) throw new ArgumentException("Invalid replay host context.");
        this.HostId = hostId; this.SessionId = sessionId;
    }

    public bool ObserveSent(OutboundProtocolMessage message, Guid connectionId)
    {
        this.RequireOwner();
        if (connectionId == Guid.Empty || message.PlayerId == 0 || message.PlayerId == this.HostId
            || message.MessageType != ReadOnlyProtocolCodec.WelcomeType
            || !ReadOnlyProtocolCodec.TryReadWelcome(message.Json, out var welcome)
            || welcome!.HostPlayerId != this.HostId || welcome.SessionId != this.SessionId) return false;
        var current = new Sent(message, connectionId, welcome.Challenge);
        if (!this.Entries.TryGetValue(message.PlayerId, out var entry))
        {
            if (this.Entries.Count == ReadOnlyProtocolSession.MaxPeers) return false;
            this.Entries.Add(message.PlayerId, new(current, null, false));
        }
        else if (entry.Current.ConnectionId != connectionId)
            this.Entries[message.PlayerId] = new(current, entry.Current, false);
        else if (entry.Current.Message.Json != message.Json)
            // 同一连接发生另一个challenge时丢弃来源链，不能借重试凑出“旧连接”证据。
            this.Entries[message.PlayerId] = new(current, null, true);
        return true;
    }

    /// <summary>
    /// peer由桥的当前实际会话读取；expectedConnectionId只防止调用者持有过时快照，不选目标/正文。
    /// 取用即消耗，即使后续网络调用抛错也不重试；true不代表网络送达或接收方拒绝。
    /// </summary>
    public bool TryTake(ProtocolPeerStatus peer, Guid expectedConnectionId, out PreviousWelcomeReplay? replay)
    {
        this.RequireOwner(); replay = null;
        if (peer.IsHost || !peer.Ready || peer.PlayerId == 0 || peer.PlayerId == this.HostId
            || expectedConnectionId == Guid.Empty || peer.ConnectionId != expectedConnectionId
            || !this.Entries.TryGetValue(peer.PlayerId, out var entry) || entry.Used
            || entry.Previous is not { } previous || entry.Current.ConnectionId != expectedConnectionId
            || previous.ConnectionId == expectedConnectionId || previous.Challenge == entry.Current.Challenge) return false;
        this.Entries[peer.PlayerId] = entry with { Used = true };
        replay = new(previous.Message, previous.ConnectionId, expectedConnectionId,
            previous.Challenge, entry.Current.Challenge);
        return true;
    }

    private void RequireOwner()
    {
        if (!ReferenceEquals(this.Owner, Thread.CurrentThread)) throw new InvalidOperationException("Replay memory belongs to the game thread.");
    }
    private sealed record Sent(OutboundProtocolMessage Message, Guid ConnectionId, Guid Challenge);
    private sealed record Entry(Sent Current, Sent? Previous, bool Used);
}
