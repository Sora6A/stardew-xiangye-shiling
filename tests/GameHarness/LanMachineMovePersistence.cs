using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>Writes only the explicitly bound disposable farm, using the real host bedtime path.</summary>
internal sealed class LanMachineMovePersistence
{
    internal const string Scenario = "LanMachineMovePersistence";
    internal const string FarmName = "XYSNET09";
    internal const ulong FixtureId = 620261124;
    private const long FarmhandId = NativeNetworkBootstrap.ClientId;
    private const string HostWriteReport = "lan-machine-move-write-host.json";

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly NativeNetworkBootstrap Bootstrap;
    private readonly Stopwatch Watch = new();
    private readonly bool Host, Write;
    private readonly List<ToolRemovalScenarios.ReplacementEvidence> Replacements = new();
    private MachineService? Service;
    private SavedMovement? Expected;
    private NativeBedtimeInteraction? Bedtime;
    private int? BeforeDay;
    private bool Completed, Leaving, SkippedArrival, NativePeerDisconnected, HomeWarpRequested;
    private BedtimeCheckpoint? LastBedtimeCheckpoint;
    private int SavingEvents, SavedEvents;
    private string? Failure;

    internal static bool Matches(Config config) => config.Scenario == Scenario && config.FarmName == FarmName
        && config.SaveFixtureId == FixtureId && config.NativeRecoveryPhase is "Write" or "Verify";

    internal LanMachineMovePersistence(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && Matches(config), "Bind movement persistence to its disposable farm.");
        this.Helper = helper;
        this.Monitor = monitor;
        this.Bootstrap = new(monitor, config);
        this.Host = config.NetworkRole == "Host";
        this.Write = config.NativeRecoveryPhase == "Write";
        if (this.Host && this.Write) helper.Events.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
    }

    internal void QueueStart() { this.Watch.Start(); this.Bootstrap.QueueStart(); }

    internal void Tick()
    {
        if (!this.Watch.IsRunning || this.Completed || this.Failure is not null || this.Leaving) return;
        try
        {
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(3), "Timed out waiting for native movement, replication or bedtime."
                + (this.LastBedtimeCheckpoint is null ? "" : $" Last checkpoint: {this.LastBedtimeCheckpoint}"));
            this.Bootstrap.Tick();
            Require(this.Bootstrap.Failure is null, this.Bootstrap.Failure ?? "Native startup failed.");
            if (!this.Bootstrap.Ready || !Context.IsWorldReady) return;
            Farmer host = Game1.MasterPlayer;
            Require(Context.IsMultiplayer && Context.IsMainPlayer == this.Host && !Context.IsSplitScreen
                && Game1.uniqueIDForThisGame == FixtureId && host.UniqueMultiplayerID == NativeNetworkBootstrap.HostId
                && Game1.player.farmName.Value == FarmName, "The live game is not the bound movement farm.");
            this.Service ??= this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
                ?? throw new InvalidOperationException("The production machine service is missing.");
            var protocol = this.Service.ReadNetworkDiagnostics();
            Require(!protocol.Faulted && !protocol.ProbeConfigured && this.Service.ReadLanTransportDiagnostics().HookInstalled,
                "The ordinary direct-IP capability is required with diagnostic probes off.");
            if (!this.Write) { this.Verify(host, protocol); return; }
            if (this.Host) this.WriteHost(host, protocol);
            else if (this.ObserveClientFarm(protocol))
            {
                this.Replacements.AddRange(ReadDevices());
                this.Leaving = true;
                Game1.ExitToTitle();
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.Message); }
    }

    private void WriteHost(Farmer host, ProtocolDiagnosticSnapshot protocol)
    {
        if (this.BeforeDay is null)
        {
            if (!protocol.Peers.Any(peer => peer.PlayerId == FarmhandId && peer.Ready) || !this.HasCurrentCapability(host, protocol)
                || !this.EnterFarm()) return;
            Require(!Game1.getFarm().objects.Values.Any(item => MachineService.TryGetKind(item, out _))
                && !host.modData.ContainsKey(LanMachineTransactionScenario.ReservationKey),
                "The write seed already contains a device or reservation.");
            this.BeforeDay = Game1.Date.TotalDays;
            ToolRemovalScenarios.Run(host, Game1.getFarm(), new Vector2(54, 20), this.Service!, (name, check) =>
            {
                check();
                this.Monitor.Log(name, LogLevel.Info);
            }, requireHeldPlacement: true, preserveReplacement: this.Replacements.Add);
            Require(this.Replacements.Count == 2 && SameDevices(this.Replacements, ReadDevices()), "The two replacements did not survive fixture cleanup.");
        }
        var checkpoint = this.ObserveBedtime(host);
        // The native location can start its arrival event on the update after tool preparation.
        // Closing a known skippable dialogue lets the game process the farmhand's disconnect normally.
        if (this.TrySkipFarmArrival()) return;
        if (!checkpoint.NativePeerDisconnected || !checkpoint.OnlyHostOnline || checkpoint.SmapiPeers != 0) return;
        if (this.Bedtime is null)
        {
            Require(Constants.SaveFolderName == $"{FarmName}_{FixtureId}" && host.Name == FarmName,
                "Refuse to save an unexpected native host folder.");
            var home = Utility.getHomeOfFarmer(host);
            if (!ReferenceEquals(Game1.currentLocation, home) || !ReferenceEquals(host.currentLocation, home))
            {
                if (!this.HomeWarpRequested && !checkpoint.Warping && !checkpoint.FadeToBlack && checkpoint.PlayerFree)
                {
                    Point entry = home.getEntryLocation();
                    this.HomeWarpRequested = true;
                    this.Monitor.Log("HostRequestedNativeHomeWarp", LogLevel.Info);
                    Game1.warpFarmer(home.NameOrUniqueName, entry.X, entry.Y, 0, home.isStructure.Value);
                }
                return;
            }
            if (!checkpoint.PlayerFree || checkpoint.Warping || checkpoint.FadeToBlack) return;
            this.Bedtime = new NativeBedtimeInteraction();
            this.Monitor.Log("HostApproachedNativeBed", LogLevel.Info);
        }
        if (this.SavedEvents == 0) { this.Bedtime.Tick(step => this.Monitor.Log(step, LogLevel.Info)); return; }
        Require(Game1.Date.TotalDays == this.BeforeDay + 1 && SameDevices(this.Replacements, ReadDevices()),
            "Native bedtime did not retain the same two placements and batch state.");
        this.Finish(null);
    }

    private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
    {
        if (e.Peer.PlayerID != FarmhandId || this.BeforeDay is null || !Context.IsWorldReady
            || Game1.uniqueIDForThisGame != FixtureId || Game1.MasterPlayer.UniqueMultiplayerID != NativeNetworkBootstrap.HostId) return;
        this.NativePeerDisconnected = true;
        this.Monitor.Log("HostObservedNativeMovementPeerDisconnected", LogLevel.Info);
    }

    private BedtimeCheckpoint ObserveBedtime(Farmer host)
    {
        var online = Game1.getOnlineFarmers().ToArray();
        var checkpoint = new BedtimeCheckpoint(this.NativePeerDisconnected, online.Any(farmer => farmer.UniqueMultiplayerID == FarmhandId),
            online.Length == 1 && ReferenceEquals(online[0], host), this.Helper.Multiplayer.GetConnectedPlayers().Count(),
            Game1.currentLocation?.NameOrUniqueName, host.currentLocation?.NameOrUniqueName, Game1.isWarping, Game1.fadeToBlack,
            Context.IsPlayerFree, host.CanMove, Game1.eventUp, Game1.CurrentEvent?.id, Game1.CurrentEvent?.skippable, Game1.isQuestion,
            Game1.activeClickableMenu?.GetType().Name, this.HomeWarpRequested, this.Bedtime is not null);
        if (checkpoint != this.LastBedtimeCheckpoint)
        {
            this.LastBedtimeCheckpoint = checkpoint;
            this.Monitor.Log(checkpoint.ToString(), LogLevel.Info);
        }
        return checkpoint;
    }

    private void Verify(Farmer host, ProtocolDiagnosticSnapshot protocol)
    {
        var expected = this.Expected ??= this.Helper.Data.ReadJsonFile<SavedMovement>(HostWriteReport);
        if (expected is not { Completed: true, Fixture: FixtureId, SavingEvents: 1, SavedEvents: 1, Replacements.Length: 2 }
            || expected.Process == Environment.ProcessId || expected.BeforeDay is not int day || Game1.Date.TotalDays != day + 1)
            throw new InvalidOperationException("The cold process needs the completed write-host report from the previous day.");
        if (!this.HasCurrentCapability(host, protocol) || !this.EnterFarm()) return;
        var devices = ReadDevices();
        if (!this.Host && devices.Length < 2) return;
        Require(SameDevices(expected.Replacements, devices), "Cold replicated tiles, instance IDs or batch records differ from the write.");
        Require(devices.All(device => MachineStateCodec.TryRead(device.Raw, out var state)
            && state?.PhaseAt(MachineService.Now()) == MachinePhase.Ready), "Overnight moved batches are not ready.");
        this.Replacements.AddRange(devices);
        this.Finish(null);
    }

    private bool ObserveClientFarm(ProtocolDiagnosticSnapshot protocol)
        => this.HasCurrentCapability(Game1.MasterPlayer, protocol) && this.EnterFarm() && ReadDevices().Length == 2;

    private bool HasCurrentCapability(Farmer host, ProtocolDiagnosticSnapshot protocol)
    {
        var peer = protocol.Peers.SingleOrDefault(peer => peer.PlayerId == (this.Host ? FarmhandId : host.UniqueMultiplayerID) && peer.Ready);
        if (peer is null || protocol.SessionId is not Guid session) return false;
        string key = FormattableString.Invariant($"{MachineService.Prefix}/LanMachineCapability/{FarmhandId}");
        string prefix = FormattableString.Invariant($"1|{FixtureId}|{host.UniqueMultiplayerID}|{FarmhandId}|{session:D}|");
        string? marker = host.modData.GetValueOrDefault(key);
        return marker is not null && marker.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(marker[prefix.Length..], "D", out Guid challenge) && challenge != Guid.Empty
            && (this.Host || challenge == peer.Challenge);
    }

    private bool EnterFarm()
    {
        if (!ReferenceEquals(Game1.currentLocation, Game1.getFarm()))
        {
            if (!Game1.isWarping && !Game1.fadeToBlack) Game1.warpFarmer("Farm", 54, 22, 0);
            return false;
        }
        if (this.TrySkipFarmArrival()) return false;
        return !Game1.isWarping && !Game1.fadeToBlack && !Game1.eventUp && Context.IsPlayerFree;
    }

    private bool TrySkipFarmArrival()
    {
        if (this.SkippedArrival || !ReferenceEquals(Game1.currentLocation, Game1.getFarm()) || !Game1.eventUp || Game1.isQuestion
            || Game1.activeClickableMenu is not StardewValley.Menus.DialogueBox { isQuestion: false }
            || Game1.CurrentEvent is not { skippable: true } arrival) return false;
        this.SkippedArrival = true;
        this.Monitor.Log($"SkippedNativeFarmArrival {arrival.id}", LogLevel.Info);
        arrival.skipEvent();
        return true;
    }

    private static ToolRemovalScenarios.ReplacementEvidence[] ReadDevices()
    {
        var devices = new List<ToolRemovalScenarios.ReplacementEvidence>(2);
        foreach (var pair in Game1.getFarm().objects.Pairs)
        {
            if (!MachineService.TryGetKind(pair.Value, out _) || !MachineService.TryRead(pair.Value, out var state)
                || state?.Batch is null) continue;
            devices.Add(new(pair.Value.QualifiedItemId, (int)pair.Key.X, (int)pair.Key.Y, Guid.Empty,
                state.InstanceId, pair.Value.modData[MachineService.StateKey]));
        }
        return devices.ToArray();
    }

    internal void OnSaving()
    {
        if (!this.Host || !this.Write || this.Bedtime?.AnswerSent != true || ++this.SavingEvents != 1)
            this.Finish("Unexpected save.");
    }

    internal void OnSaved()
    {
        if (++this.SavedEvents != 1 || this.SavingEvents != 1) this.Finish("Unexpected save completion.");
    }

    internal void OnReturnedToTitle()
    {
        if (this.Leaving) this.Finish(null);
        else if (!this.Completed && this.Watch.IsRunning) this.Finish("Unexpected return to title.");
    }

    private void Finish(string? error)
    {
        if (this.Completed || this.Failure is not null) return;
        this.Failure = error;
        this.Completed = error is null;
        if (this.Host && this.Write) this.Helper.Events.Multiplayer.PeerDisconnected -= this.OnPeerDisconnected;
        this.Bootstrap.RestoreOptions();
        string phase = this.Write ? "write" : "verify";
        string role = this.Host ? "host" : "client";
        this.Helper.Data.WriteJsonFile($"lan-machine-move-{phase}-{role}.json", new SavedMovement(this.Completed, FixtureId,
            Environment.ProcessId, this.BeforeDay, this.SavingEvents, this.SavedEvents, this.Replacements.ToArray(), error,
            this.LastBedtimeCheckpoint));
        this.Monitor.Log(error ?? $"Native LAN movement {phase} {role} passed.", error is null ? LogLevel.Info : LogLevel.Error);
    }

    private static bool SameDevices(IEnumerable<ToolRemovalScenarios.ReplacementEvidence> expected,
        IEnumerable<ToolRemovalScenarios.ReplacementEvidence> actual) => expected.Select(device => device with { OldInstance = Guid.Empty })
            .OrderBy(device => device.ItemId, StringComparer.Ordinal).SequenceEqual(actual.OrderBy(device => device.ItemId, StringComparer.Ordinal));

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed record SavedMovement(bool Completed, ulong Fixture, int Process, int? BeforeDay, int SavingEvents,
        int SavedEvents, ToolRemovalScenarios.ReplacementEvidence[] Replacements, string? Failure, BedtimeCheckpoint? Bedtime);
    private sealed record BedtimeCheckpoint(bool NativePeerDisconnected, bool FarmhandOnline, bool OnlyHostOnline,
        int SmapiPeers, string? Location, string? FarmerLocation, bool Warping, bool FadeToBlack, bool PlayerFree, bool CanMove,
        bool EventUp, string? EventId, bool? EventSkippable, bool Question, string? Menu, bool HomeWarpRequested, bool BedtimeStarted);
}
