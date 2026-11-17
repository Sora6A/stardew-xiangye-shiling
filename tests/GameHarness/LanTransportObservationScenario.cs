using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>
/// 专用 XYSNET02 的原生环回观测。使用生产只读探针和原版断线，分别记录主机来源核对与农工实际收到的拒绝。
/// </summary>
internal sealed class LanTransportObservationScenario
{
    private const string HarnessId = "Sora6A.XiangyeShiling.GameHarness";
    private const string LeaveType = "LanTransportObservationLeave";
    private readonly NativeNetworkBootstrap Bootstrap;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly bool Host;
    private readonly Stopwatch Clock = new();
    private readonly Guid RequestId = Guid.NewGuid();
    private MachineService? Service;
    private LanTransportDiagnosticSnapshot? Accepted, Disconnected;
    private ProtocolTrace? Rejection;
    private long? DisconnectedGeneration;
    private Guid? LeaveRequest;
    private bool ProbeSent, Leaving, Completed, SavingObserved;
    private string? Failure;

    internal LanTransportObservationScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        this.Host = config.NetworkRole == "Host";
        this.Bootstrap = new(monitor, config);
    }

    internal void QueueStart()
    {
        this.Clock.Start();
        this.Bootstrap.QueueStart();
    }

    internal void Tick()
    {
        if (!this.Clock.IsRunning || this.Completed || this.Failure is not null) return;
        try
        {
            this.Bootstrap.Tick();
            Require(this.Bootstrap.Failure is null, this.Bootstrap.Failure ?? "Native network bootstrap failed.");
            Require(this.Clock.Elapsed < TimeSpan.FromMinutes(2), "Timed out waiting for real LAN provenance and disconnect evidence.");
            if (!this.Bootstrap.Ready || this.Leaving) return;
            Require(Context.IsWorldReady && Context.IsMainPlayer == this.Host && !Context.IsSplitScreen && Context.ScreenId == 0
                && Game1.player.farmName.Value == NativeNetworkBootstrap.FarmName
                && Game1.uniqueIDForThisGame == NativeNetworkBootstrap.FixtureId, "The dedicated LAN observation world changed.");
            this.Service ??= this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
                ?? throw new InvalidOperationException("The production module diagnostic API is unavailable.");
            var protocol = this.Service.ReadNetworkDiagnostics();
            Require(!protocol.Faulted && !protocol.WritesEnabled && protocol.ProbeConfigured,
                "Read-only probe configuration or production write gate does not match.");
            if (this.Host) this.ObserveHost(protocol);
            else this.ObserveClient(protocol);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            this.Fail(ex.Message);
        }
    }

    private void ObserveHost(ProtocolDiagnosticSnapshot protocol)
    {
        var lan = this.Service!.ReadLanTransportDiagnostics();
        Require(lan.Configured && lan.HookInstalled, "Pinned LAN receive hook is unavailable for this native build.");
        if (this.Accepted is { LastConfirmed: { } accepted })
        {
            if (protocol.DisconnectGeneration <= accepted.ConnectionGeneration
                || this.Helper.Multiplayer.GetConnectedPlayer(NativeNetworkBootstrap.ClientId) is not null) return;
            Require(lan.LastConfirmed is null && lan.Confirmed >= this.Accepted.Confirmed,
                "A disconnected LAN observation remained current.");
            this.Disconnected = lan;
            this.DisconnectedGeneration = protocol.DisconnectGeneration;
            this.Finish();
            return;
        }

        if (!protocol.Active || lan.LastConfirmed is not { MessageType: ReadOnlyProtocolCodec.RequestType } evidence) return;
        Require(lan.Allowed && evidence.ActorId == NativeNetworkBootstrap.ClientId
            && evidence.WorldId == NativeNetworkBootstrap.FixtureId
            && evidence.WorldGeneration == protocol.WorldGeneration && evidence.ConnectionGeneration == protocol.DisconnectGeneration
            && protocol.Peers.Any(peer => peer.Ready && !peer.IsHost && peer.PlayerId == evidence.ActorId
                && peer.ConnectionId == evidence.ConnectionId), "Observed native actor is not the current dedicated LAN farmhand.");
        this.Rejection = FindRejection(protocol, ProtocolReceiveKind.RejectionProduced, evidence.ActorId, null);
        if (this.Rejection?.Observation?.RequestId is not Guid requestId) return;
        Require(this.Rejection.ConnectionId == evidence.ConnectionId && this.Rejection.WorldGeneration == evidence.WorldGeneration
            && this.Rejection.DisconnectGeneration == evidence.ConnectionGeneration
            && this.Rejection.ThreadId == Environment.CurrentManagedThreadId, "The rejection is not from the observed host receive session.");
        this.Accepted = lan;
        this.Report();
        // 等待主机先落下来源证据，避免农工过早退出造成两个事件无法配对。
        this.Helper.Multiplayer.SendMessage(requestId, LeaveType, new[] { HarnessId }, new[] { evidence.ActorId });
    }

    private void ObserveClient(ProtocolDiagnosticSnapshot protocol)
    {
        if (!this.ProbeSent)
        {
            if (!protocol.Peers.Any(peer => peer.Ready && peer.IsHost && peer.PlayerId == NativeNetworkBootstrap.HostId)) return;
            this.ProbeSent = this.Service!.TrySendNetworkProbe(ProtocolProbeKind.Start, this.RequestId);
            Require(this.ProbeSent, "Could not send the stock read-only probe from the real farmhand.");
            return;
        }
        this.Rejection ??= FindRejection(protocol, ProtocolReceiveKind.RejectionAccepted, NativeNetworkBootstrap.HostId, this.RequestId);
        if (this.Rejection is null || this.LeaveRequest != this.RequestId) return;
        this.Leaving = true;
        this.Report();
        this.Bootstrap.RestoreOptions();
        Game1.ExitToTitle();
    }

    private static ProtocolTrace? FindRejection(ProtocolDiagnosticSnapshot protocol, ProtocolReceiveKind kind,
        long peerId, Guid? requestId) => protocol.Trace.LastOrDefault(trace => trace.Kind == ProtocolTraceKind.Processed
            && trace.PeerId == peerId && trace.Observation is { Reason: ProtocolRejection.WritesDisabled, RequestId: not null } observation
            && observation.Kind == kind && (requestId is null || observation.RequestId == requestId));

    internal void OnMessage(ModMessageReceivedEventArgs args)
    {
        if (this.Host || !this.ProbeSent || this.Leaving || this.Failure is not null
            || args.FromModID != HarnessId || args.Type != LeaveType || args.FromPlayerID != NativeNetworkBootstrap.HostId) return;
        try
        {
            if (args.ReadAs<Guid>() == this.RequestId) this.LeaveRequest = this.RequestId;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            this.Fail("Invalid coordination message for the dedicated LAN observation.");
        }
    }

    internal void OnReturnedToTitle()
    {
        if (this.Completed || this.Failure is not null || !this.Clock.IsRunning) return;
        if (!this.Host && this.Leaving) this.Finish();
        else this.Fail("Unexpected title transition during the LAN observation.");
    }

    internal void OnSaving()
    {
        this.SavingObserved = true;
        this.Fail("Unexpected save during the no-save LAN observation.");
    }

    private void Finish()
    {
        this.Completed = true;
        this.Bootstrap.RestoreOptions();
        this.Report();
        this.Monitor.Log("Native LAN provenance observation completed.", LogLevel.Info);
    }

    private void Fail(string reason)
    {
        if (this.Failure is not null) return;
        this.Failure = reason;
        this.Completed = false;
        this.Bootstrap.RestoreOptions();
        this.Report();
        this.Monitor.Log(reason, LogLevel.Warn);
    }

    private void Report() => this.Helper.Data.WriteJsonFile("lan-transport-observation-result.json", new
    {
        SchemaVersion = 1, ProcessId = Environment.ProcessId, FixtureId = NativeNetworkBootstrap.FixtureId,
        NetworkRole = this.Host ? "Host" : "Client", this.Completed, this.Failure, this.SavingObserved,
        this.Accepted, this.Disconnected, this.DisconnectedGeneration, this.Rejection, this.Leaving
    });

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
