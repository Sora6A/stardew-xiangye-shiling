using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using XiangyeShiling.Networking;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// P1-NET-02 的 SMAPI 只读桥。消息回调只保留事件引用并排队；UpdateTicked 才刷新连接表、
/// 限流、读取正文和定向发包。它不持有 MachineService，不构造 HostActorSnapshot，也不开放菜单/放置门禁。
/// </summary>
/// <remarks>
/// FromPlayerID/FromModID 是 SMAPI 消息元数据，不能单独证明远端 Farmer/背包可写权。
/// 队列有界仅限制待处理事件数，无法限制 SMAPI 在回调之前反序列化的内存；P1-NET-03/04 仍待实测。
/// 本地分屏不运行此桥；普通单人没有会话或发包。返回标题/读档/角色变化丢弃旧队列，不写存档。
/// </remarks>
internal sealed class ReadOnlyProtocolService
{
    private const int MaximumWorkPerTick = 16;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly string ModId;
    private readonly string ModVersion;
    private readonly NetworkDiagnosticOptions Options;
    private readonly Thread Owner = Thread.CurrentThread;
    private readonly Guid RunId = Guid.NewGuid();
    private readonly ProtocolTraceBuffer Trace = new();
    private readonly ProtocolInbox<PendingMessage> Inbox = new();
    private readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly Dictionary<long, PeerConnection> Connections = new();
    // Replaced atomically by the owner and never mutated after publication; callbacks need no SDK/game lookup.
    private IReadOnlyDictionary<long, Guid> PublishedConnections = new Dictionary<long, Guid>();
    private ReadOnlyProtocolSession? Session;
    private WelcomeReplayBuffer? ReplayBuffer;
    private long Generation;
    private long ConnectionGeneration;
    private long LocalPlayerId;
    private bool IsHost;
    private bool Faulted;
    private long QueueRejected;
    private long ProbeWindow;
    private int ProbeCount;

    public ReadOnlyProtocolService(IModHelper helper, IMonitor monitor, IManifest manifest, NetworkDiagnosticOptions options)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        this.ModId = manifest.UniqueID;
        this.ModVersion = manifest.Version.ToString();
        this.Options = options;
        helper.Events.Multiplayer.ModMessageReceived += this.OnMessageReceived;
        helper.Events.Multiplayer.PeerDisconnected += (_, _) => Interlocked.Increment(ref this.ConnectionGeneration);
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.Reset();
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.Reset();
        // Poll GetConnectedPlayers every frame: client-side PeerConnected is not a reliable handshake trigger.
        // That table also drops disconnected peers before any lingering Farmer is removed from the world.
    }

    private void OnMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        // Do not touch Context/Game1 or ReadAs in this callback; even local SendMessage can be reentrant.
        // Explicit outbound recipients exclude ourselves. Unknown sources are filtered later on the owner thread.
        if (e.FromModID != this.ModId || e.Type is not (ReadOnlyProtocolCodec.HelloType
                or ReadOnlyProtocolCodec.WelcomeType or ReadOnlyProtocolCodec.RequestType or ReadOnlyProtocolCodec.RejectedType)) return;
        if (!Volatile.Read(ref this.PublishedConnections).TryGetValue(e.FromPlayerID, out Guid connectionId)) return;
        if (!this.Inbox.TryEnqueue(new PendingMessage(Volatile.Read(ref this.Generation),
            Volatile.Read(ref this.ConnectionGeneration), connectionId, e))) Interlocked.Increment(ref this.QueueRejected);
    }

    private void Reset()
    {
        Interlocked.Increment(ref this.Generation);
        this.Inbox.Clear();
        this.Session = null;
        this.ReplayBuffer = null;
        this.Connections.Clear();
        Volatile.Write(ref this.PublishedConnections, new Dictionary<long, Guid>());
        this.LocalPlayerId = 0;
        this.Faulted = false;
        this.Record(ProtocolTraceKind.Reset);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // A diagnostic fault stays closed until SaveLoaded/ReturnedToTitle; do not recreate/log every frame.
        if (this.Faulted) { this.Inbox.Clear(); return; }
        if (!Context.IsWorldReady || !Context.IsMultiplayer || Context.IsSplitScreen || Context.ScreenId != 0)
        {
            if (this.Session is not null || this.Inbox.Count > 0) this.Reset();
            return;
        }
        long playerId = Game1.player.UniqueMultiplayerID;
        if (playerId == 0) { this.Reset(); return; }
        if (this.Session is null || this.LocalPlayerId != playerId || this.IsHost != Context.IsMainPlayer)
        {
            this.Reset();
            this.LocalPlayerId = playerId;
            this.IsHost = Context.IsMainPlayer;
            this.Session = new ReadOnlyProtocolSession(playerId, this.IsHost, this.ModId, Guid.NewGuid());
            if (this.IsHost && this.ProbesAllowed())
                this.ReplayBuffer = new(playerId, this.Session.ActiveSessionId!.Value);
        }
        long now = this.Clock.ElapsedMilliseconds;
        try
        {
            foreach (var message in this.Session.SynchronizePeers(this.CurrentPeers(), now))
                this.Send(message);
            for (int count = 0; count < MaximumWorkPerTick && this.Inbox.TryDequeue(out var pending); count++)
            {
                // Any disconnect invalidates already queued work conservatively, without rotating the host world ID.
                if (pending!.Generation != Volatile.Read(ref this.Generation)
                    || pending.ConnectionGeneration != Volatile.Read(ref this.ConnectionGeneration)
                    || !this.Connections.TryGetValue(pending.Args.FromPlayerID, out var connection)
                    || pending.ConnectionId != connection.Id)
                {
                    this.Record(ProtocolTraceKind.StaleQueue, pending.Args.FromPlayerID, pending.Args.Type);
                    continue;
                }
                var args = pending.Args;
                OutboundProtocolMessage? response = this.Session.Receive(args.FromPlayerID, args.FromModID,
                    args.Type, () => ReadBody(args), now);
                this.Record(ProtocolTraceKind.Processed, args.FromPlayerID, args.Type, this.Session.LastObservation,
                    this.Session.LastQuota, pending.ConnectionId);
                if (response is not null && this.Send(response) && this.ProbesAllowed())
                    this.ReplayBuffer?.ObserveSent(response, pending.ConnectionId);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Isolate only this diagnostic session; gameplay gates stay closed. No raw payload/peer names in logs.
            this.Reset();
            this.Faulted = true;
            this.Record(ProtocolTraceKind.Faulted);
            this.Monitor.Log($"Read-only multiplayer protocol paused after {ex.GetType().Name}; no machine transaction was executed.", LogLevel.Warn);
        }
    }

    private ProtocolPeer Snapshot(IMultiplayerPeer peer, Guid connectionId = default) => new(peer.PlayerID, peer.IsHost,
        peer.HasSmapi && peer.GetMod(this.ModId)?.Version.ToString() == this.ModVersion, peer.IsSplitScreen, connectionId);

    private IReadOnlyList<ProtocolPeer> CurrentPeers()
    {
        var peers = this.Helper.Multiplayer.GetConnectedPlayers()
            .Where(p => p.PlayerID != this.LocalPlayerId && this.Snapshot(p).HasMatchingMod && !p.IsSplitScreen
                && (this.IsHost ? !p.IsHost : p.IsHost))
            .OrderBy(p => p.PlayerID).Take(ReadOnlyProtocolSession.MaxPeers).ToArray();
        foreach (long id in this.Connections.Keys.Where(id => !peers.Any(p => p.PlayerID == id)).ToArray())
            this.Connections.Remove(id);
        var result = new List<ProtocolPeer>();
        foreach (var peer in peers)
        {
            // SMAPI returns its stored connection object. A reconnect creates a new reference even for the same ID.
            // This catches disconnect/reconnect entirely between polls, while preserving other peers' handshakes.
            if (!this.Connections.TryGetValue(peer.PlayerID, out var connection)
                || !ReferenceEquals(peer, connection.Peer))
                this.Connections[peer.PlayerID] = connection = new(peer, Guid.NewGuid());
            result.Add(this.Snapshot(peer, connection.Id));
        }
        Volatile.Write(ref this.PublishedConnections, this.Connections.ToDictionary(p => p.Key, p => p.Value.Id));
        return result;
    }

    private bool Send(OutboundProtocolMessage message)
    {
        // Recheck the live recipient after planning. Void SendMessage is an attempt, never an acknowledgement.
        var peer = this.Helper.Multiplayer.GetConnectedPlayer(message.PlayerId);
        if (peer is null || message.PlayerId == this.LocalPlayerId || !this.Snapshot(peer).HasMatchingMod
            || peer.IsSplitScreen || (this.IsHost ? peer.IsHost : !peer.IsHost))
        { this.Record(ProtocolTraceKind.SendSuppressed, message.PlayerId, message.MessageType); return false; }
        // Only report a connection token if the actual SDK recipient is the same object we published.
        // A missing token is incomplete evidence, not permission to relabel a replacement connection.
        Guid? connectionId = this.Connections.TryGetValue(message.PlayerId, out var connection)
            && ReferenceEquals(peer, connection.Peer) ? connection.Id : null;
        this.Record(ProtocolTraceKind.SendAttempt, message.PlayerId, message.MessageType, connectionId: connectionId);
        this.Helper.Multiplayer.SendMessage(message.Json, message.MessageType,
            new[] { this.ModId }, new[] { message.PlayerId });
        return true;
    }

    public ProtocolDiagnosticSnapshot ReadDiagnostics()
    {
        this.RequireOwner();
        return new(this.RunId, this.Options.EnableReadOnlyNetworkProbes, this.ProbesAllowed(),
            this.Session is not null, this.IsHost, this.Faulted, false, this.LocalPlayerId,
            this.Session?.ActiveSessionId, this.Generation, Volatile.Read(ref this.ConnectionGeneration),
            this.Inbox.Count, Interlocked.Read(ref this.QueueRejected), this.Trace.LastSequence, this.Trace.Evicted,
            this.Session?.ReadPeerStatus() ?? Array.Empty<ProtocolPeerStatus>(), this.Trace.Snapshot());
    }

    /// <summary>默认关闭的固定合成意图测试；无任意JSON/目标/actor入口，主机不能发，16次/秒上限。true仅表示发送尝试。</summary>
    public bool TrySendProbe(ProtocolProbeKind kind, Guid requestId)
    {
        this.RequireOwner();
        if (!this.ProbesAllowed() || this.IsHost || this.Session?.ActiveSessionId is not Guid sessionId) return false;
        var peer = this.Session.ReadPeerStatus().SingleOrDefault(p => p.IsHost && p.Ready);
        if (peer is null) return false;
        long now = this.Clock.ElapsedMilliseconds;
        if (now - this.ProbeWindow >= 1000) { this.ProbeWindow = now; this.ProbeCount = 0; }
        if (this.ProbeCount == 16) return false;
        string json = ProtocolProbePlan.Create(kind, sessionId, requestId, this.LocalPlayerId);
        this.ProbeCount++;
        this.Record(ProtocolTraceKind.ProbeAttempt, peer.PlayerId, ReadOnlyProtocolCodec.RequestType,
            connectionId: peer.ConnectionId);
        return this.Send(new(peer.PlayerId, ReadOnlyProtocolCodec.RequestType, json));
    }

    private bool ProbesAllowed() => this.Options.EnableReadOnlyNetworkProbes && !this.Faulted
        && Context.IsWorldReady && Context.IsMultiplayer && !Context.IsSplitScreen && Context.ScreenId == 0
        && this.Options.NetworkProbeSaveFixtureId != 0 && Game1.uniqueIDForThisGame == this.Options.NetworkProbeSaveFixtureId
        && this.Options.NetworkProbeFarmName is { Length: >= 6 and <= 20 }
        && this.Options.NetworkProbeFarmName.StartsWith("XYSNET", StringComparison.Ordinal)
        && Game1.player.farmName.Value == this.Options.NetworkProbeFarmName;

    /// <summary>
    /// 独立NET02重放夹具入口：仅专用世界主机、唯一当前Ready客户端、相邻旧连接一次；
    /// 不接收JSON/peer/actor/session，旧正文仅取自本进程真实已发送Welcome内存。
    /// 与其他探针共享16次/秒预算；true只表示尝试发送，实际拒绝必须由客户端原报告证明。
    /// </summary>
    public bool TryReplayPreviousWelcome(Guid expectedConnectionId)
    {
        this.RequireOwner();
        if (!this.ProbesAllowed() || !this.IsHost || this.Session is null || this.ReplayBuffer is null) return false;
        var peers = this.Session.ReadPeerStatus();
        if (peers.Count != 1) return false;
        long now = this.Clock.ElapsedMilliseconds;
        if (now - this.ProbeWindow >= 1000) { this.ProbeWindow = now; this.ProbeCount = 0; }
        if (this.ProbeCount == 16 || !this.ReplayBuffer.TryTake(peers[0], expectedConnectionId, out var replay)) return false;
        this.ProbeCount++;
        this.Record(ProtocolTraceKind.WelcomeReplayAttempt, peers[0].PlayerId, ReadOnlyProtocolCodec.WelcomeType,
            new(ProtocolReceiveKind.Ignored, false, Challenge: replay!.PreviousChallenge), connectionId: expectedConnectionId);
        return this.Send(replay.Message);
    }

    private void Record(ProtocolTraceKind kind, long? peerId = null, string? type = null,
        ProtocolObservation? observation = null, ProtocolQuotaWindow? quota = null, Guid? connectionId = null)
    {
        if (this.Options.EnableReadOnlyNetworkProbes)
            this.Trace.Record(this.Clock.ElapsedMilliseconds, this.Generation, Volatile.Read(ref this.ConnectionGeneration),
                kind, peerId, type, observation, quota, connectionId);
    }

    private void RequireOwner()
    {
        if (!ReferenceEquals(Thread.CurrentThread, this.Owner)) throw new InvalidOperationException("Protocol diagnostic API is game-thread only.");
    }

    private static string? ReadBody(ModMessageReceivedEventArgs args)
    {
        try { return args.ReadAs<string>(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    private sealed record PendingMessage(long Generation, long ConnectionGeneration, Guid ConnectionId, ModMessageReceivedEventArgs Args);
    private sealed record PeerConnection(IMultiplayerPeer Peer, Guid Id);
}
