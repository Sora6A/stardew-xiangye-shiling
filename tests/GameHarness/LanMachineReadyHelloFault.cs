using System.Diagnostics;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>Suppresses the first native capability observation without changing the original Hello or Welcome.</summary>
internal sealed class LanMachineReadyHelloFault
{
    internal const string Phase = "SuppressFirstCapability";
    private static LanMachineReadyHelloFault? Active;
    private readonly IModHelper Helper;
    private readonly bool Host;
    private readonly Harmony Patch;
    private long FirstSentAt;
    private Guid FirstChallenge, ReadyChallenge;
    private Guid? ReadySession;
    private double? RetryMilliseconds;
    private int AcceptedHellos, SentHellos;
    private bool ReadyWithoutCapability, Finished;

    internal LanMachineReadyHelloFault(IModHelper helper, Config config)
    {
        if (!config.Enabled || config.OpenPreview || config.NativeRecoveryPhase != Phase
            || !NativeNetworkBootstrap.IsTransactionFixture(config) || config.SaveFixtureId != NativeNetworkBootstrap.NormalLanFixtureId
            || !config.VerifyLanFarmhandMenu || config.VerifyLanLiveReconnect || config.VerifyLanHostCompetition)
            throw new InvalidOperationException("The capability fault requires the isolated ordinary farmhand-menu fixture.");
        this.Helper = helper;
        this.Host = config.NetworkRole == "Host";
        this.Patch = new(MachineService.Prefix + ".GameHarness.ReadyHello");
    }

    internal void Install()
    {
        if (Active is not null) throw new InvalidOperationException("Only one capability fault may be armed.");
        var bridge = typeof(MachineService).Assembly.GetType("XiangyeShiling.GameIntegration.ReadOnlyProtocolService", throwOnError: true)!;
        string method = this.Host ? "ObserveReadOnlyProvenance" : "Send";
        var target = AccessTools.Method(bridge, method) ?? throw new MissingMethodException(bridge.Name, method);
        this.Patch.Patch(target,
            prefix: this.Host ? new HarmonyMethod(typeof(LanMachineReadyHelloFault), nameof(BeforeCapability)) : null,
            postfix: this.Host ? null : new HarmonyMethod(typeof(LanMachineReadyHelloFault), nameof(ObserveSentHello)));
        Active = this;
    }

    internal void Tick()
    {
        if (this.Finished || !IsFixtureWorld() || Context.IsMainPlayer != this.Host) return;
        if (this.Helper.ModRegistry.GetApi(MachineService.Prefix) is not MachineService service) return;
        var protocol = service.ReadNetworkDiagnostics();
        long actor = this.Host ? NativeNetworkBootstrap.ClientId : Game1.player.UniqueMultiplayerID;
        long hostId = Game1.MasterPlayer.UniqueMultiplayerID;
        if (hostId != NativeNetworkBootstrap.HostId || actor != NativeNetworkBootstrap.ClientId
            || protocol.Faulted || protocol.ProbeConfigured || protocol.SessionId is not Guid session) return;
        var peer = protocol.Peers.SingleOrDefault(peer => peer.PlayerId == (this.Host ? actor : hostId) && peer.Ready);
        if (peer is null) return;
        string key = FormattableString.Invariant($"{MachineService.Prefix}/LanMachineCapability/{actor}");
        string? marker = Game1.MasterPlayer.modData.GetValueOrDefault(key);
        if (!this.Host && !this.ReadyWithoutCapability && marker is null)
        {
            var allowMenu = AccessTools.Method(typeof(MachineService), "AllowsFarmhandMenu")
                ?? throw new MissingMethodException(nameof(MachineService), "AllowsFarmhandMenu");
            if (allowMenu.Invoke(service, null) is not false)
                throw new InvalidOperationException("A read-only Ready farmhand without a native marker was allowed to use the menu.");
            this.ReadyWithoutCapability = true;
            this.ReadyChallenge = peer.Challenge;
            this.ReadySession = session;
        }

        Guid challenge = this.Host ? this.FirstChallenge : this.ReadyChallenge;
        string expected = FormattableString.Invariant($"1|{NativeNetworkBootstrap.NormalLanFixtureId}|{hostId}|{actor}|{session:D}|{challenge:D}");
        if (challenge == Guid.Empty || marker != expected) return;
        // Compare send times on the farmhand. Host acceptance includes time spent in the receive queue.
        var verified = this.Host ? service.ReadLanTransportDiagnostics().LastConfirmed : null;
        bool completed = this.Host
            ? this.AcceptedHellos == 2 && verified is { MessageType: ReadOnlyProtocolCodec.HelloType }
                && verified.ActorId == actor && verified.ConnectionId == peer.ConnectionId
            : this.ReadyWithoutCapability && session == this.ReadySession && peer.Challenge == this.ReadyChallenge
                && this.SentHellos == 2 && this.RetryMilliseconds is >= ReadOnlyProtocolSession.HelloRetryMilliseconds - 100;
        this.Helper.Data.WriteJsonFile("lan-ready-hello-fault-result.json", new
        {
            Process = Environment.ProcessId, this.Host, Completed = completed, Fixture = NativeNetworkBootstrap.NormalLanFixtureId,
            Session = session, Challenge = challenge, Connection = peer.ConnectionId, Marker = marker,
            this.AcceptedHellos, this.SentHellos, this.RetryMilliseconds, this.ReadyWithoutCapability, Verified = verified
        });
        this.Finished = true;
    }

    internal void Remove()
    {
        if (Active != this) return;
        this.Patch.UnpatchAll(this.Patch.Id);
        Active = null;
    }

    private static bool BeforeCapability(ProtocolObservation observation)
    {
        if (Active is not { } active || !Context.IsMainPlayer || !IsFixtureWorld()
            || observation is not { Kind: ProtocolReceiveKind.HelloAccepted, Challenge: Guid challenge }) return true;
        if (++active.AcceptedHellos == 1)
        {
            active.FirstChallenge = challenge;
            return false;
        }
        if (active.AcceptedHellos == 2 && challenge != active.FirstChallenge)
            throw new InvalidOperationException("The refresh replaced the original Hello challenge.");
        return true;
    }

    private static void ObserveSentHello(OutboundProtocolMessage message, bool __result)
    {
        if (Active is not { } active || Context.IsMainPlayer || !IsFixtureWorld() || !__result
            || message.MessageType != ReadOnlyProtocolCodec.HelloType || message.PlayerId != NativeNetworkBootstrap.HostId) return;
        long now = Stopwatch.GetTimestamp();
        if (++active.SentHellos == 1) active.FirstSentAt = now;
        if (active.SentHellos == 2)
            active.RetryMilliseconds = (now - active.FirstSentAt) * 1000d / Stopwatch.Frequency;
    }

    private static bool IsFixtureWorld() => Context.IsWorldReady && Context.IsMultiplayer && !Context.IsSplitScreen
        && Game1.uniqueIDForThisGame == NativeNetworkBootstrap.NormalLanFixtureId
        && Game1.player.farmName.Value == NativeNetworkBootstrap.NormalLanFarmName;
}
