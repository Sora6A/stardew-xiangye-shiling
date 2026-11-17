using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// P1 的 SMAPI 会话桥。原协议保持只读，独立的设备请求通过原生直连或专用验收入口准入。
/// 消息回调保留连接证据并排队。游戏线程刷新 peer、验证来源、限流并处理意图。
/// </summary>
/// <remarks>
/// FromPlayerID/FromModID 是 SMAPI 消息元数据，不能单独证明远端 Farmer/背包可写权。
/// 队列有界仅限制待处理事件数，无法限制 SMAPI 在回调之前反序列化的内存；P1-NET-03/04 仍待实测。
/// 本地分屏不运行此桥；普通单人没有会话或发包。返回标题/读档/角色变化丢弃旧队列，不写存档。
/// </remarks>
internal sealed class ReadOnlyProtocolService
{
    private const int MaximumWorkPerTick = 16;
    private const string MachineIntentType = "XYS/MachineIntent/2";
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly string ModId;
    private readonly string ModVersion;
    private readonly NetworkDiagnosticOptions Options;
    private readonly LanTransportActorObservation LanTransport;
    private readonly HostOwnerTransaction? HostTransactions;
    private readonly FarmhandReservationClient? OwnerTransactions;
    private readonly Thread Owner = Thread.CurrentThread;
    private readonly Guid RunId = Guid.NewGuid();
    private readonly ProtocolTraceBuffer Trace = new();
    private readonly ProtocolInbox<PendingMessage> Inbox = new();
    private readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly Dictionary<long, PeerConnection> Connections = new();
    private readonly Dictionary<long, VerifiedCapability> Capabilities = new();
    private object? ClientNativeConnection;
    private (Guid Connection, Guid Challenge, Guid Nonce, ulong Sequence) ClientIntent;
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

    public ReadOnlyProtocolService(IModHelper helper, IMonitor monitor, IManifest manifest, NetworkDiagnosticOptions options,
        MachineService machines)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        this.ModId = manifest.UniqueID;
        this.ModVersion = manifest.Version.ToString();
        this.Options = options;
        this.LanTransport = new(options.EnableReadOnlyNetworkProbes || options.EnableLanTransactionFixture
            || options.EnableLanMachineOperations, monitor, this.ModId);
        if (options.EnableLanTransactionFixture || options.EnableLanMachineOperations)
        {
            this.HostTransactions = new(machines);
            this.OwnerTransactions = new(monitor);
        }
        helper.Events.Multiplayer.ModMessageReceived += this.OnMessageReceived;
        helper.Events.Multiplayer.PeerDisconnected += (_, _) => Interlocked.Increment(ref this.ConnectionGeneration);
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.ResetWorld();
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.ResetWorld();
        helper.Events.GameLoop.Saving += (_, _) => this.OwnerTransactions?.OnSaving();
        helper.Events.GameLoop.Saved += (_, _) => this.OwnerTransactions?.OnSaved();
        // Poll GetConnectedPlayers every frame: client-side PeerConnected is not a reliable handshake trigger.
        // That table also drops disconnected peers before any lingering Farmer is removed from the world.
    }

    private void OnMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        // Never read the body here. Existing read-only handling is independent of the opt-in host provenance sample.
        if (e.FromModID != this.ModId || e.Type is not (ReadOnlyProtocolCodec.HelloType
                or ReadOnlyProtocolCodec.WelcomeType or ReadOnlyProtocolCodec.RequestType or ReadOnlyProtocolCodec.RejectedType
                or MachineIntentType or DirectIpMachineIntent.MessageType)) return;
        bool machineIntent = e.Type is MachineIntentType or DirectIpMachineIntent.MessageType;
        bool ownerHost = ReferenceEquals(Thread.CurrentThread, this.Owner) && Context.IsMainPlayer;
        if (machineIntent && (!ownerHost || !this.MachineHostAllowed()))
            return;
        if (!Volatile.Read(ref this.PublishedConnections).TryGetValue(e.FromPlayerID, out Guid connectionId)) return;
        long worldGeneration = Volatile.Read(ref this.Generation);
        long connectionGeneration = Volatile.Read(ref this.ConnectionGeneration);
        bool sampleHello = e.Type == ReadOnlyProtocolCodec.HelloType && ownerHost && this.DirectIpWorldAllowed();
        var provenance = machineIntent || sampleHello || (ownerHost && this.ProbesAllowed())
            ? this.LanTransport.Capture(e.FromPlayerID, worldGeneration, connectionGeneration) : null;
        if (machineIntent && provenance is null) return;
        if (!this.Inbox.TryEnqueue(new PendingMessage(worldGeneration, connectionGeneration, connectionId, e, provenance)))
            Interlocked.Increment(ref this.QueueRejected);
    }

    private void Reset()
    {
        Interlocked.Increment(ref this.Generation);
        this.Inbox.Clear();
        this.Session = null;
        this.ReplayBuffer = null;
        this.Connections.Clear();
        this.Capabilities.Clear();
        this.ClientNativeConnection = null;
        this.ClientIntent = default;
        Volatile.Write(ref this.PublishedConnections, new Dictionary<long, Guid>());
        this.LocalPlayerId = 0;
        this.Faulted = false;
        this.OwnerTransactions?.FenceIntent();
        this.Record(ProtocolTraceKind.Reset);
    }

    private void ResetWorld()
    {
        this.Reset();
        this.HostTransactions?.Reset();
        this.OwnerTransactions?.ResetWorld();
    }

    internal void CaptureLoadedReservation()
    {
        if (Context.IsMainPlayer && this.MachineHostAllowed()) this.HostTransactions?.CaptureLoadedRecord();
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
            if (!this.IsHost && this.DirectIpWorldAllowed())
            {
                object? currentNative = this.LanTransport.ReadDirectIpHostConnection();
                if (this.ClientNativeConnection is not null && !ReferenceEquals(currentNative, this.ClientNativeConnection))
                {
                    this.Reset();
                    return;
                }
                this.ClientNativeConnection = currentNative;
            }
            foreach (var message in this.Session.SynchronizePeers(this.CurrentPeers(), now))
                this.Send(message);
            if (this.Session.ActiveSessionId is Guid sessionId)
            {
                if (this.IsHost && this.MachineHostAllowed()) this.HostTransactions!.Advance(sessionId);
                else if (!this.IsHost && this.CurrentMachineHost() is { } host)
                    this.OwnerTransactions!.Advance(host.PlayerId, sessionId, host.ConnectionId);
                else if (!this.IsHost)
                {
                    this.OwnerTransactions?.FenceIntent();
                    this.RefreshDirectIpHello(now);
                }
            }
            else if (!this.IsHost && this.Session.ActiveSessionId is null)
                this.OwnerTransactions?.FenceIntent();
            for (int count = 0; count < MaximumWorkPerTick && this.Inbox.TryDequeue(out var pending); count++)
            {
                // Any disconnect invalidates already queued work conservatively, without rotating the host world ID.
                if (pending!.Generation != Volatile.Read(ref this.Generation)
                    || pending.ConnectionGeneration != Volatile.Read(ref this.ConnectionGeneration)
                    || !this.Connections.TryGetValue(pending.Args.FromPlayerID, out var connection)
                    || pending.ConnectionId != connection.Id)
                {
                    this.LanTransport.Discard(pending.LanProvenance);
                    this.Record(ProtocolTraceKind.StaleQueue, pending.Args.FromPlayerID, pending.Args.Type);
                    continue;
                }
                var args = pending.Args;
                if (args.Type is MachineIntentType or DirectIpMachineIntent.MessageType)
                {
                    this.ReceiveMachineIntent(pending, connection, now);
                    continue;
                }
                OutboundProtocolMessage? response = this.Session.Receive(args.FromPlayerID, args.FromModID,
                    args.Type, () => ReadBody(args), now);
                this.ObserveReadOnlyProvenance(pending, connection, this.Session.LastObservation);
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
            this.Monitor.Log($"Multiplayer protocol paused after {ex.GetType().Name}.", LogLevel.Warn);
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
        {
            this.Connections.Remove(id);
            this.Capabilities.Remove(id);
        }
        var result = new List<ProtocolPeer>();
        foreach (var peer in peers)
        {
            // SMAPI returns its stored connection object. A reconnect creates a new reference even for the same ID.
            // This catches disconnect/reconnect entirely between polls, while preserving other peers' handshakes.
            if (!this.Connections.TryGetValue(peer.PlayerID, out var connection)
                || !ReferenceEquals(peer, connection.Peer))
            {
                this.Capabilities.Remove(peer.PlayerID);
                this.Connections[peer.PlayerID] = connection = new(peer, Guid.NewGuid());
            }
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

    public LanTransportDiagnosticSnapshot ReadLanTransportDiagnostics()
    {
        this.RequireOwner();
        return this.LanTransport.Read(this.IsHost && (this.ProbesAllowed() || this.MachineHostAllowed()), this.Generation,
            Volatile.Read(ref this.ConnectionGeneration), Volatile.Read(ref this.PublishedConnections));
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

    private bool MachineFixtureAllowed() => this.Options.EnableLanTransactionFixture && this.LanTransport.Available && !this.Faulted
        && Context.IsWorldReady && Context.IsMultiplayer && !Context.IsSplitScreen && Context.ScreenId == 0
        && (Game1.player.farmName.Value, Game1.uniqueIDForThisGame) is
            ("XYSNET05", 620261116UL) or ("XYSNET06", 620261118UL) or ("XYSNET07", 620261120UL);

    private bool DirectIpWorldAllowed() => this.Options.EnableLanMachineOperations && this.LanTransport.Available && !this.Faulted
        && Context.IsWorldReady && Context.IsMultiplayer && !Context.IsSplitScreen && Context.ScreenId == 0
        && Game1.uniqueIDForThisGame != 0;

    private bool MachineHostAllowed() => this.MachineFixtureAllowed()
        || (this.DirectIpWorldAllowed() && this.LanTransport.HasDirectIpHost());

    private void ReceiveMachineIntent(PendingMessage pending, PeerConnection connection, long now)
    {
        var args = pending.Args;
        if (!this.IsHost || !this.MachineHostAllowed() || this.Session?.ActiveSessionId is not Guid session
            || !ReferenceEquals(connection.Peer, this.Helper.Multiplayer.GetConnectedPlayer(args.FromPlayerID))) return;

        bool legacy = args.Type == MachineIntentType;
        var capability = legacy ? null : this.ReadIntentCapability(args.FromPlayerID, connection.Id, pending.LanProvenance);
        if ((legacy ? !this.MachineFixtureAllowed() : capability is null)
            || !this.LanTransport.TryConfirm(pending.LanProvenance, args.FromPlayerID, args.Type,
                this.Generation, Volatile.Read(ref this.ConnectionGeneration), pending.ConnectionId)) return;

        Guid nonce = default;
        DirectIpMachineIntent? message = null;
        if (!this.Session.TryReadMachineIntent(args.FromPlayerID, pending.ConnectionId, ReadRequest, now, out string? raw, out var intent))
            return;
        if (legacy) this.HostTransactions!.ReceiveIntent(session, args.FromPlayerID, nonce, raw!, intent!);
        else this.HostTransactions!.ReceiveDirectIpIntent(session, args.FromPlayerID, message!, capability!.Sequence);

        // The shared session consumes one peer quota before reading either wrapper. The saved inner JSON stays exact.
        string? ReadRequest() => legacy
            ? OwnerIntentRefusal.TryReadIntent(ReadBody(args), out nonce, out string? requestRaw) ? requestRaw : null
            : DirectIpMachineIntent.TryRead(ReadBody(args), out message) ? message.RequestRaw : null;
    }

    private void ObserveReadOnlyProvenance(PendingMessage pending, PeerConnection connection, ProtocolObservation observation)
    {
        if (pending.LanProvenance is not { } provenance) return;
        var args = pending.Args;
        bool capabilityHello = this.IsHost && this.DirectIpWorldAllowed()
            && observation is { Kind: ProtocolReceiveKind.HelloAccepted, Challenge: not null };
        if ((!capabilityHello && !(this.IsHost && this.ProbesAllowed()))
            || !ReferenceEquals(connection.Peer, this.Helper.Multiplayer.GetConnectedPlayer(args.FromPlayerID)))
        {
            this.LanTransport.Discard(provenance);
            return;
        }
        if (!this.LanTransport.TryConfirm(provenance, args.FromPlayerID, args.Type, this.Generation,
            Volatile.Read(ref this.ConnectionGeneration), pending.ConnectionId) || !capabilityHello
            || this.Session?.ActiveSessionId is not Guid session || !this.LanTransport.MatchesCurrentActor(provenance))
            return;

        // One accepted challenge per live SDK/native pair. A second Hello may still get a read-only Welcome.
        var capability = new LanMachineCapability(Game1.uniqueIDForThisGame, this.LocalPlayerId,
            args.FromPlayerID, session, observation.Challenge!.Value);
        if (this.Capabilities.TryGetValue(args.FromPlayerID, out var previous))
        {
            if (ReferenceEquals(previous.Provenance.Scope.Connection, provenance.Scope.Connection))
            {
                if (previous.Value == capability)
                    this.Capabilities[args.FromPlayerID] = previous with { Provenance = provenance };
                return;
            }
            // Require a new SDK connection before trusting a replacement native connection.
            return;
        }

        if (Game1.player.modData.TryGetValue(LanMachineCapability.Key(this.ModId, capability.ActorId), out string raw)
            && (!LanMachineCapability.TryRead(raw, out var stored)
                || !stored.MatchesWorld(capability.WorldId, capability.HostId, capability.ActorId))) return;
        string key = LanMachineCapability.Key(this.ModId, capability.ActorId);
        string value = capability.Encode();
        Game1.player.modData[key] = value;
        if (Game1.player.modData.GetValueOrDefault(key) == value)
            this.Capabilities[args.FromPlayerID] = new(connection.Id, capability, provenance, new(capability.Challenge));
    }

    private VerifiedCapability? ReadIntentCapability(long actor, Guid connection, LanTransportActorObservation.IngressTicket? incoming)
        => incoming is not null && this.Capabilities.TryGetValue(actor, out var capability) && capability.ConnectionId == connection
            && capability.Value.SessionId == this.Session?.ActiveSessionId
            && ReferenceEquals(incoming.Scope.Connection, capability.Provenance.Scope.Connection)
            && this.LanTransport.MatchesCurrentActor(capability.Provenance)
            && Game1.player.modData.GetValueOrDefault(LanMachineCapability.Key(this.ModId, actor)) == capability.Value.Encode()
            ? capability : null;

    private ProtocolPeerStatus? CurrentMachineHost()
    {
        if (this.CurrentReadyHost() is not { } host) return null;
        if (this.MachineFixtureAllowed()) return host;
        if (!this.DirectIpWorldAllowed() || this.Session?.ActiveSessionId is not Guid session
            || this.ClientNativeConnection is null || !ReferenceEquals(this.ClientNativeConnection, this.LanTransport.ReadDirectIpHostConnection()))
            return null;

        Farmer master = Game1.MasterPlayer;
        long actor = Game1.player.UniqueMultiplayerID;
        return master.modData.TryGetValue(LanMachineCapability.Key(this.ModId, actor), out string raw)
            && LanMachineCapability.TryRead(raw, out var capability)
            && capability.MatchesWorld(Game1.uniqueIDForThisGame, host.PlayerId, actor)
            && capability.SessionId == session && capability.Challenge == host.Challenge ? host : null;
    }

    private void RefreshDirectIpHello(long now)
    {
        // A read-only Welcome does not prove that the host observed this Hello on its native connection.
        // Retry only while that same SDK/native host is ready and the transaction capability is still missing.
        if (!this.DirectIpWorldAllowed() || this.ClientNativeConnection is null
            || !ReferenceEquals(this.ClientNativeConnection, this.LanTransport.ReadDirectIpHostConnection())
            || this.CurrentReadyHost() is not { } host) return;
        if (this.Session!.TryRefreshReadyHello(host.PlayerId, host.ConnectionId, now) is { } refresh)
            this.Send(refresh);
    }

    internal bool AllowsHostOperations() => ReferenceEquals(Thread.CurrentThread, this.Owner)
        && Context.IsMainPlayer && this.IsHost && !Game1.newDay && this.Session?.ActiveSessionId is not null
        && this.MachineHostAllowed();

    internal bool AllowsFarmhandOperations() => ReferenceEquals(Thread.CurrentThread, this.Owner)
        && !Context.IsMainPlayer && !this.IsHost && !Game1.newDay && this.CurrentMachineHost() is not null;

    private ProtocolPeerStatus? CurrentReadyHost()
    {
        if (this.Session?.ActiveSessionId is null) return null;
        var host = this.Session.ReadPeerStatus().SingleOrDefault(peer => peer.IsHost && peer.Ready);
        return host is not null && Game1.MasterPlayer?.UniqueMultiplayerID == host.PlayerId
            && this.Connections.TryGetValue(host.PlayerId, out var connection) && connection.Id == host.ConnectionId
            && ReferenceEquals(connection.Peer, this.Helper.Multiplayer.GetConnectedPlayer(host.PlayerId)) ? host : null;
    }

    private sealed record VerifiedCapability(Guid ConnectionId, LanMachineCapability Value,
        LanTransportActorObservation.IngressTicket Provenance, DirectIpIntentSequence Sequence);

    internal FarmhandRequestStatus ReadFarmhandStatus(Guid requestId)
    {
        this.RequireOwner();
        return this.OwnerTransactions?.ReadStatus(requestId) ?? FarmhandRequestStatus.Unconfirmed;
    }

    internal bool TryQueueFixtureOperation(SObject machine, Request request, string? recipeId, Func<bool>? interaction = null)
    {
        this.RequireOwner();
        if (this.IsHost || (interaction is null && !this.MachineFixtureAllowed()) || Game1.newDay
            || this.Session?.ActiveSessionId is not Guid session || this.CurrentMachineHost() is not { } host
            || Game1.player.UniqueMultiplayerID != request.ActorId
            || Game1.eventUp || Game1.player.UsingTool || Game1.player.currentLocation is not { } location
            || !location.objects.TryGetValue(machine.TileLocation, out SObject placed) || !ReferenceEquals(machine, placed)
            || !MachineService.TryRead(machine, out var state) || state!.InstanceId != request.InstanceId
            || state.Revision != request.ExpectedRevision
            || Math.Abs(Game1.player.Tile.X - machine.TileLocation.X) > 1 || Math.Abs(Game1.player.Tile.Y - machine.TileLocation.Y) > 1)
            return false;

        var intent = new MachineRequestEnvelope(1, session, request.Id, request.ActorId, location.NameOrUniqueName,
            (int)machine.TileLocation.X, (int)machine.TileLocation.Y, request.InstanceId, request.ExpectedRevision,
            recipeId is null ? MachineRequestAction.Collect : MachineRequestAction.Start, recipeId);
        if (!MachineRequestCodec.IsValid(intent)) return false;
        string json = MachineRequestCodec.Write(intent);
        bool legacy = this.MachineFixtureAllowed();
        if (!legacy && (this.ClientIntent.Connection != host.ConnectionId || this.ClientIntent.Challenge != host.Challenge))
            this.ClientIntent = (host.ConnectionId, host.Challenge, Guid.Empty, 0);
        bool allowNew = legacy || this.ClientIntent.Sequence < ulong.MaxValue;
        if (!this.OwnerTransactions!.TryRemember(intent, host.ConnectionId, interaction, out Guid nonce, allowNew)) return false;
        if (!legacy && this.ClientIntent.Nonce != nonce)
            this.ClientIntent = (host.ConnectionId, host.Challenge, nonce, checked(this.ClientIntent.Sequence + 1));

        var message = legacy
            ? new OutboundProtocolMessage(host.PlayerId, MachineIntentType, OwnerIntentRefusal.WriteIntent(nonce, json))
            : new OutboundProtocolMessage(host.PlayerId, DirectIpMachineIntent.MessageType,
                DirectIpMachineIntent.Write(host.Challenge, this.ClientIntent.Sequence, nonce, json));
        bool sent = this.Send(message);
        // An interactive request stays pending if dispatch could not be confirmed, so it can retry the same nonce.
        return interaction is not null || sent;
    }

    internal bool TryCancelFixtureOperation(Guid requestId)
    {
        this.RequireOwner();
        return !this.IsHost && this.CurrentMachineHost() is not null
            && this.OwnerTransactions!.RequestCancellation(requestId);
    }

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

    private sealed record PendingMessage(long Generation, long ConnectionGeneration, Guid ConnectionId,
        ModMessageReceivedEventArgs Args, LanTransportActorObservation.IngressTicket? LanProvenance);
    private sealed record PeerConnection(IMultiplayerPeer Peer, Guid Id);
}
