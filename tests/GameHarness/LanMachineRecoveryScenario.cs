using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;
using Fixture = GameHarness.LanMachineTransactionScenario;

namespace GameHarness;

/// <summary>Drives native save and cold recovery around a real owner debit on two dedicated fixtures.</summary>
internal sealed class LanMachineRecoveryScenario
{
    private const string HarnessId = MachineService.Prefix + ".GameHarness";
    private const string MessageType = "LanMachineRecoveryFixture";
    private const string ReportName = "lan-machine-recovery-result.json";
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly NativeNetworkBootstrap Bootstrap;
    private readonly bool Host;
    private readonly bool Write;
    private readonly bool PostDebit;
    private readonly string FarmName;
    private readonly ulong FixtureId;
    private readonly Stopwatch Clock = new();
    private readonly List<string> Stages = new();
    private MachineService? Service;
    private SObject? Machine;
    private Signal? Ready, Disconnect;
    private Request? Requested;
    private Plan? Reservation, Terminal;
    private NativeBedtimeInteraction? Bedtime;
    private string? OriginalInventory, OriginalMachine, OriginalHostInventory, CommittedInventory, Failure, Receipt;
    private bool Positioned, Leaving, NativePeerDisconnected, Completed;
    private int SavingEvents, SavedEvents;
    private int? SavedTotalDays;
    private Guid? NewSessionId;

    internal LanMachineRecoveryScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == "LanMachineRecovery"
            && config.NativeRecoveryPhase is "Write" or "Verify", "Machine recovery requires an explicit Write or Verify phase.");
        this.Helper = helper;
        this.Monitor = monitor;
        this.Bootstrap = new(monitor, config);
        this.Host = config.NetworkRole == "Host";
        this.Write = config.NativeRecoveryPhase == "Write";
        this.FarmName = config.FarmName;
        this.FixtureId = config.SaveFixtureId;
        this.PostDebit = this.FixtureId == NativeNetworkBootstrap.MachinePostDebitFixtureId;
    }

    internal void QueueStart()
    {
        this.Clock.Start();
        this.Bootstrap.QueueStart();
    }

    // A replicated grant can arrive inside the native update. Intercept it before production's normal-priority
    // UpdateTicked handler, then move out of its actual native range while the host observes the reservation.
    [EventPriority(EventPriority.High)]
    internal void Tick(object? sender, UpdateTickedEventArgs e)
    {
        if (!this.Clock.IsRunning || this.Completed || this.Failure is not null || this.Leaving) return;
        try
        {
            Require(this.Clock.Elapsed < TimeSpan.FromMinutes(3), "Timed out waiting for native machine recovery.");
            this.Bootstrap.Tick();
            Require(this.Bootstrap.Failure is null, this.Bootstrap.Failure ?? "Native network bootstrap failed.");
            if (!this.Bootstrap.Ready || !Context.IsWorldReady) return;
            Require(Context.IsMultiplayer && Context.IsMainPlayer == this.Host && !Context.IsSplitScreen && Context.ScreenId == 0
                && Game1.uniqueIDForThisGame == this.FixtureId && Game1.player.farmName.Value == this.FarmName
                && Game1.player.UniqueMultiplayerID == (this.Host ? NativeNetworkBootstrap.HostId : NativeNetworkBootstrap.ClientId),
                "The active world or actor differs from the machine recovery fixture.");
            this.Service ??= this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
                ?? throw new InvalidOperationException("Production MachineService is unavailable.");
            var protocol = this.Service.ReadNetworkDiagnostics();
            Require(!protocol.Faulted && this.Service.ReadLanTransportDiagnostics().HookInstalled, "Production LAN ingress is unavailable.");
            Farmer host = Game1.MasterPlayer;
            Require(host.UniqueMultiplayerID == NativeNetworkBootstrap.HostId, "The native host identity differs.");
            if (this.Write) this.TickWrite(host, protocol);
            else this.TickVerify(host, protocol);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Fail(error is TargetInvocationException { InnerException: { } inner } ? inner.Message : error.Message);
        }
    }

    private void TickWrite(Farmer host, ProtocolDiagnosticSnapshot protocol)
    {
        if (this.Host) this.Machine ??= Fixture.PrepareMachine(host);
        var farm = Game1.getFarm();
        this.Machine ??= farm.objects.Values.SingleOrDefault(machine => MachineService.TryGetKind(machine, out _));
        if (this.Machine is not { } machine) return;
        var state = this.VerifyIdle(machine);
        Farmer? owner = this.Host ? Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: false) : Game1.player;
        if (owner is null) return;
        if (this.Host && this.Reservation is not null && this.NativePeerDisconnected && !OwnerOnline()
            && !this.Helper.Multiplayer.GetConnectedPlayers().Any())
        {
            if (this.PostDebit) this.VerifyCommitted(host, owner, this.Reservation);
            else this.ReadUntouched(host, owner);
            Require(host.modData.GetValueOrDefault(Fixture.ReservationKey) == this.Reservation.Raw,
                "The host changed the reservation before the cold checkpoint.");
            if (this.Bedtime is null)
            {
                Require(Constants.SaveFolderName == $"{this.FarmName}_{this.FixtureId}" && host.Name == this.FarmName,
                    "The disposable host save folder must match exactly.");
                this.Record(this.PostDebit ? "HostObservedNativeDisconnectAfterDebit" : "HostObservedNativeDisconnectWithOriginalSlots");
                this.Bedtime = new NativeBedtimeInteraction();
                this.Record("HostApproachedNativeBed");
            }
            if (this.SavedEvents == 1)
            {
                this.SavedTotalDays = Game1.Date.TotalDays;
                Require(this.SavedTotalDays == this.Bedtime.StartTotalDays + 1, "Native bedtime must advance exactly one day.");
                this.Complete("HostFinishedNativeBedtimeWithReservedGrant");
            }
            else this.Bedtime.Tick(this.Record);
            return;
        }

        long peerId = this.Host ? NativeNetworkBootstrap.ClientId : NativeNetworkBootstrap.HostId;
        if (!protocol.Peers.Any(peer => peer.PlayerId == peerId && peer.Ready)) return;
        if (!this.Host && !this.Positioned) this.Positioned = Fixture.PrepareOwner(owner, farm, machine);
        if (!this.Positioned && !this.Host) return;
        if (this.OriginalInventory is null)
        {
            if (!ReferenceEquals(owner.currentLocation, farm) || owner.Tile != machine.TileLocation + Fixture.Below
                || owner.Items[Fixture.InputSlot] is not { QualifiedItemId: Fixture.Soybean, Stack: 6 }) return;
            this.ReadUntouched(host, owner);
            this.Record(this.Host ? "HostObservedPreparedOwner" : "ClientPreparedOwner");
            if (this.Host) this.Send(new("Ready", state.InstanceId, Guid.Empty, null), NativeNetworkBootstrap.ClientId);
        }

        if (!this.Host && this.Requested is null && this.Ready?.Instance == state.InstanceId)
        {
            var request = new Request(Guid.NewGuid(), state.InstanceId, state.Revision, owner.UniqueMultiplayerID);
            var queue = typeof(MachineService).GetMethod("TryQueueLanFixtureOperation", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(queue?.Invoke(this.Service, new object?[] { machine, request, Fixture.Recipe }) is true,
                "The production client declined its Start intent.");
            this.Requested = request;
            this.Record("ClientSentProductionStart");
        }

        Plan? plan = ReadPlan(host);
        if (plan is null) return;
        if (!this.PostDebit) this.ReadUntouched(host, owner);
        this.VerifyPlan(plan, machine, "Reserved");
        if (this.Reservation is null)
        {
            this.Reservation = plan;
            if (this.Host)
            {
                var evidence = this.Service!.ReadLanTransportDiagnostics().LastConfirmed;
                Require(OwnerOnline() && evidence is { ActorId: NativeNetworkBootstrap.ClientId, MessageType: "XYS/MachineIntent/2" },
                    "The host did not verify the original online native intent.");
                if (this.PostDebit)
                {
                    this.FreezeHostFinalization();
                    this.Record("HostFixtureHeldRealReservedGrant");
                }
                else
                {
                    this.Record("HostObservedProductionReservedBeforeOwnerDebit");
                    this.Send(new("Disconnect", state.InstanceId, plan.RequestId, plan.Hash), NativeNetworkBootstrap.ClientId);
                }
            }
            else
            {
                Require(this.Requested?.Id == plan.RequestId && ReferenceEquals(owner.currentLocation, farm),
                    "The native grant does not belong to the live farmhand intent.");
                if (this.PostDebit) this.Record("ClientObservedReservedBeforeProductionDebit");
                else
                {
                    owner.setTileLocation(machine.TileLocation + new Vector2(3, 1));
                    Require(Math.Abs(owner.Tile.X - machine.TileLocation.X) > 1, "The farmhand remained in production range.");
                    this.Record("ClientLeftRangeBeforeProductionApply");
                }
            }
        }
        else Require(this.Reservation.Raw == plan.Raw, "The live reservation changed before disconnect.");

        if (this.PostDebit && this.CommittedInventory is null)
        {
            if (owner.Items[Fixture.InputSlot]?.Stack != 3 || owner.modData.GetValueOrDefault(Fixture.JournalKey) != plan.OwnerJournal)
                return; // The slot and farmer journal can replicate on different updates.
            this.VerifyCommitted(host, owner, plan);
            this.Record(this.Host ? "HostObservedReplicatedProductionDebitAndJournal" : "ClientObservedProductionDebitAndJournal");
            if (this.Host) this.Send(new("Disconnect", state.InstanceId, plan.RequestId, plan.Hash), NativeNetworkBootstrap.ClientId);
        }
        if (!this.Host && this.Disconnect is { } release && release.Instance == state.InstanceId
            && release.Request == plan.RequestId && release.PlanHash == plan.Hash)
        {
            this.Leaving = true;
            this.Record(this.PostDebit ? "ClientRequestedNativeDisconnectAfterDebit" : "ClientRequestedNativeDisconnectWithOriginalSlots");
            Game1.ExitToTitle();
        }
    }

    private void TickVerify(Farmer host, ProtocolDiagnosticSnapshot protocol)
    {
        this.Machine ??= Game1.getFarm().objects.Values.SingleOrDefault(machine => MachineService.TryGetKind(machine, out _));
        if (this.Machine is not { } machine) return;
        if (this.PostDebit)
        {
            this.TickVerifyCommitted(host, protocol, machine);
            return;
        }
        this.VerifyIdle(machine);
        Farmer? owner = this.Host ? Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: false) : Game1.player;
        if (owner is null) return;
        this.ReadUntouched(host, owner, permitSeen: true);
        var plan = ReadPlan(host);
        if (this.Host && this.Reservation is null)
        {
            Require(!OwnerOnline() && !this.Helper.Multiplayer.GetConnectedPlayers().Any() && plan is not null,
                "The cold host must observe the loaded reservation before the owner reconnects.");
            this.VerifyPlan(plan!, machine, "Reserved");
            this.Reservation = plan;
            this.Record("ColdHostReadOfflineOriginalSlotsAndReservedGrant");
        }
        if (plan is { Status: "Cancelled" } && this.Terminal is null)
        {
            this.VerifyPlan(plan, machine, "Cancelled");
            if (this.Host)
            {
                Require(!OwnerOnline() && plan.Identity == this.Reservation!.Identity && !owner.modData.ContainsKey(Fixture.JournalKey),
                    "Production must cancel the same reservation with the offline owner still untouched.");
            }
            this.Terminal = plan;
            this.NewSessionId = protocol.SessionId;
            this.Record(this.Host ? "ColdHostCancelledOfflineGrant" : "ColdClientRejoinedWithCancelledGrant");
        }
        this.CompleteTerminal(host, owner, protocol, plan);
    }

    private void TickVerifyCommitted(Farmer host, ProtocolDiagnosticSnapshot protocol, SObject machine)
    {
        Farmer? owner = this.Host ? Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: false) : Game1.player;
        if (owner is null) return;
        Plan? plan = ReadPlan(host);
        if (this.Host && this.Reservation is null)
        {
            Require(!OwnerOnline() && !this.Helper.Multiplayer.GetConnectedPlayers().Any() && plan is not null,
                "The cold host must read the persisted debit while its owner is offline.");
            this.VerifyIdle(machine);
            this.VerifyPlan(plan!, machine, "Reserved");
            this.VerifyCommitted(host, owner, plan!);
            this.Reservation = plan;
            this.Record("ColdHostReadReservedWithOfflineDebit");
            return;
        }
        if (plan is { Status: "Applied" } && this.Terminal is null)
        {
            this.OriginalMachine ??= plan.Before;
            this.VerifyPlan(plan, machine, "Applied");
            Require(!this.Host || (!OwnerOnline() && plan.Identity == this.Reservation!.Identity),
                "Production must apply the same loaded plan while its owner is offline.");
            this.VerifyCommitted(host, owner, plan);
            this.Terminal = plan;
            this.NewSessionId = protocol.SessionId;
            this.Record(this.Host ? "ColdHostAppliedSameGrantOffline" : "ColdClientRejoinedWithAppliedGrant");
        }
        if (this.Terminal is { } terminal)
        {
            Require(MachineService.TryRead(machine, out var state) && state is { Kind: MachineKind.BeanWorkshop, Revision: 1, Batch: not null }
                && machine.modData.GetValueOrDefault(MachineService.StateKey) == terminal.After,
                "Cold production finalization must set the real machine to the exact planned revision one.");
            this.VerifyCommitted(host, owner, terminal, permitSettlement: true);
            this.CompleteTerminal(host, owner, protocol, plan);
        }
    }

    private void CompleteTerminal(Farmer host, Farmer owner, ProtocolDiagnosticSnapshot protocol, Plan? livePlan)
    {
        if (this.Terminal is { } terminal && livePlan is null && !owner.modData.ContainsKey(Fixture.JournalKey)
            && host.modData.TryGetValue(Fixture.ReceiptKey, out string receipt)
            && receipt.StartsWith(FormattableString.Invariant(
                $"{1}|{this.FixtureId}|{NativeNetworkBootstrap.HostId}|{terminal.RequestId:D}|"),
                StringComparison.Ordinal) && receipt.EndsWith(":Seen", StringComparison.Ordinal))
        {
            Require(this.Host ? OwnerOnline() : protocol.Peers.Any(peer => peer.IsHost && peer.Ready),
                "The terminal grant settled without the expected real LAN rejoin.");
            this.NewSessionId = protocol.SessionId;
            this.Receipt = receipt;
            this.Complete(this.PostDebit
                ? (this.Host ? "ColdHostSettledAppliedGrantAfterActualRejoin" : "ColdClientSettledAppliedGrantWithoutSecondDebit")
                : (this.Host ? "ColdHostSettledCancellationAfterActualRejoin" : "ColdClientSettledCancellationWithoutDebit"));
        }
    }

    internal void OnMessage(ModMessageReceivedEventArgs args)
    {
        if (this.Host || !this.Write || !this.Bootstrap.Ready || this.Completed || this.Failure is not null
            || args.FromPlayerID != NativeNetworkBootstrap.HostId || args.FromModID != HarnessId || args.Type != MessageType) return;
        try
        {
            var signal = args.ReadAs<Signal>();
            if (signal?.Kind == "Ready") this.Ready = signal;
            if (signal?.Kind == "Disconnect") this.Disconnect = signal;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Fail(error.Message); }
    }

    internal void OnPeerDisconnected(long playerId)
    {
        if (this.Host && this.Write && playerId == NativeNetworkBootstrap.ClientId) this.NativePeerDisconnected = true;
    }

    internal void OnSaving()
    {
        this.SavingEvents++;
        if (!this.Host || !this.Write || this.Bedtime?.AnswerSent != true || this.SavingEvents != 1)
            this.Fail("Saving occurred outside the authorized disposable native bedtime.");
        else this.Record("HostObservedSmapiSaving");
    }

    internal void OnSaved()
    {
        this.SavedEvents++;
        if (this.SavingEvents != 1 || this.SavedEvents != 1 || this.Failure is not null)
            this.Fail("Saved occurred outside the one authorized native save.");
        else this.Record("HostObservedSmapiSaved");
    }

    internal void OnReturnedToTitle()
    {
        if (this.Completed || this.Failure is not null || !this.Bootstrap.Ready) return;
        if (!this.Host && this.Write && this.Leaving)
            this.Complete(this.PostDebit ? "ClientReturnedToTitleAfterDebitBeforeAcknowledgement" : "ClientReturnedToTitleWithoutDebit");
        else this.Fail("The bound world returned to title before its recovery checkpoint.");
    }

    private MachineState VerifyIdle(SObject machine)
    {
        Require(MachineService.TryRead(machine, out var state) && state is { Kind: MachineKind.BeanWorkshop, Revision: 0, Batch: null },
            "An untouched/cancelled grant must leave the real machine idle at revision zero.");
        string raw = machine.modData[MachineService.StateKey];
        this.OriginalMachine ??= raw;
        Require(raw == this.OriginalMachine, "The original machine state bytes changed.");
        return state!;
    }

    private void ReadUntouched(Farmer host, Farmer owner, bool permitSeen = false)
    {
        Require(owner.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId && owner.Items.Count > Fixture.InputSlot
            && owner.Items[Fixture.InputSlot] is { QualifiedItemId: Fixture.Soybean, Stack: 6, Quality: 0 }
            && owner.Items.CountId(Fixture.Soybean) == 6 && owner.Items.CountId(Fixture.Tofu) == 0,
            "The dedicated farmhand did not retain its original six soybeans and zero tofu.");
        string? journal = owner.modData.GetValueOrDefault(Fixture.JournalKey);
        Require(journal is null || (permitSeen && journal.EndsWith(":Seen", StringComparison.Ordinal)),
            "The owner journal shows a debit before or after the cancelled grant.");
        string hostInventory = InventoryHash(host);
        string ownerInventory = InventoryHash(owner);
        this.OriginalHostInventory ??= hostInventory;
        this.OriginalInventory ??= ownerInventory;
        Require(hostInventory == this.OriginalHostInventory && ownerInventory == this.OriginalInventory,
            "Native owner or host inventory changed.");
    }

    private void VerifyCommitted(Farmer host, Farmer owner, Plan plan, bool permitSettlement = false)
    {
        Require(owner.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId && owner.Items.Count > Fixture.InputSlot
            && owner.Items[Fixture.InputSlot] is { QualifiedItemId: Fixture.Soybean, Stack: 3, Quality: 0 }
            && owner.Items.CountId(Fixture.Soybean) == 3 && owner.Items.CountId(Fixture.Tofu) == 0,
            "The production farmhand debit must consume exactly three soybeans once and grant no tofu.");
        string? journal = owner.modData.GetValueOrDefault(Fixture.JournalKey);
        Require(plan.OwnerJournal is not null && (journal == plan.OwnerJournal
            || (permitSettlement && (journal is null || journal == plan.OwnerJournal + ":Seen"))),
            "The owner journal does not match the exact production grant or its settlement.");
        string hostInventory = InventoryHash(host);
        string beforeDebit = InventoryHash(owner, originalStack: 6);
        string committed = InventoryHash(owner);
        this.OriginalHostInventory ??= hostInventory;
        this.OriginalInventory ??= beforeDebit;
        this.CommittedInventory ??= committed;
        Require(hostInventory == this.OriginalHostInventory && beforeDebit == this.OriginalInventory && committed == this.CommittedInventory,
            "Inventory outside the planned three-item debit changed.");
    }

    private void FreezeHostFinalization()
    {
        // Fixture-only fault at the real host save gate. The native Saving/Saved handlers still run later.
        // Neither machine state nor the owner's inventory or journal is assigned by this injection.
        var store = typeof(MachineService).GetProperty("Reservations", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(this.Service) ?? throw new InvalidOperationException("The production host reservation store is unavailable.");
        var gate = store.GetType().GetMethod("OnSaving", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The production host save gate is unavailable.");
        gate.Invoke(store, null);
    }

    private void VerifyPlan(Plan plan, SObject machine, string status)
    {
        bool parsedAfter = MachineStateCodec.TryRead(plan.After, out var after);
        Require(plan.Status == status && plan.World == this.FixtureId
            && plan.Host == NativeNetworkBootstrap.HostId && plan.Request.ActorId == NativeNetworkBootstrap.ClientId
            && plan.Request.LocationName == Game1.getFarm().NameOrUniqueName && plan.Request.TileX == machine.TileLocation.X
            && plan.Request.TileY == machine.TileLocation.Y && plan.Request.ExpectedRevision == 0
            && plan.Request.Action == MachineRequestAction.Start && plan.Request.RecipeId == Fixture.Recipe
            && plan.Before == this.OriginalMachine && parsedAfter
            && after is { Kind: MachineKind.BeanWorkshop, Revision: 1, Batch: { RecipeId: Fixture.Recipe } }
            && after.LastRequestId == plan.RequestId && after.InstanceId == plan.Request.InstanceId && plan.ChangeIndex == Fixture.InputSlot,
            "The production reservation did not contain the exact BeanWorkshop and inventory plan.");
        Require(after!.Batch is { Output: { ItemId: Fixture.Tofu, Quantity: 2 } } prepared && prepared.Inputs.Count == 1
            && prepared.Inputs[0] is { Slot: Fixture.InputSlot, ItemId: Fixture.Soybean, Quantity: 3, Quality: 0 },
            "The saved grant does not freeze three soybeans and two tofu in the real recipe.");
    }

    private static Plan? ReadPlan(Farmer host)
    {
        if (!host.modData.TryGetValue(Fixture.ReservationKey, out string raw)) return null;
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        Require(MachineRequestCodec.TryRead(root.GetProperty("RequestRaw").GetString(), out var request)
            && request is not null, "Production reservation is missing its original request.");
        var change = root.GetProperty("Changes").EnumerateArray().Single();
        Require(change.GetProperty("Before").GetString() is { Length: 64 } && change.GetProperty("After").GetString() is { Length: 64 },
            "Production reservation did not describe the expected partially consumed stack.");
        string identity = Digest(string.Join("\n", root.EnumerateObject().Where(field => field.Name != "Status").Select(field => field.ToString())));
        ulong world = root.GetProperty("WorldId").GetUInt64();
        return new(raw, Digest(raw), identity, root.GetProperty("Status").GetString()!, request!, world,
            root.GetProperty("HostId").GetInt64(), root.GetProperty("BeforeRaw").GetString()!, root.GetProperty("AfterRaw").GetString()!,
            change.GetProperty("Index").GetInt32(), world == NativeNetworkBootstrap.MachinePostDebitFixtureId ? ReadOwnerJournal(raw) : null);
    }

    private static string ReadOwnerJournal(string raw)
    {
        var codec = typeof(MachineService).Assembly.GetType("XiangyeShiling.Networking.OwnerCommitReservationCodec", throwOnError: true)!;
        object?[] args = { raw, null };
        Require(codec.GetMethod("TryRead")?.Invoke(null, args) is true && args[1] is not null,
            "The production codec refused its own saved grant.");
        return args[1]!.GetType().GetProperty("OwnerJournal")?.GetValue(args[1]) as string
            ?? throw new InvalidOperationException("The production journal identity is unavailable.");
    }

    private static string InventoryHash(Farmer farmer, int? originalStack = null) => Digest(JsonSerializer.Serialize(
        farmer.Items.Select((item, slot) => item is null ? null : new
        {
            item.QualifiedItemId, Stack = slot == Fixture.InputSlot ? originalStack ?? item.Stack : item.Stack, item.Quality,
            Metadata = item.modData.Pairs.OrderBy(pair => pair.Key).ToArray()
        })));

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool OwnerOnline() => Game1.getOnlineFarmers().Any(farmer => farmer.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId);
    private void Send(Signal message, long recipient) =>
        this.Helper.Multiplayer.SendMessage(message, MessageType, new[] { HarnessId }, new[] { recipient });

    private void Complete(string stage)
    {
        this.Completed = true;
        this.Bootstrap.RestoreOptions();
        this.Record(stage);
    }

    private void Fail(string failure)
    {
        if (this.Failure is not null) return;
        this.Failure = failure;
        this.Bootstrap.RestoreOptions();
        this.Monitor.Log(failure, LogLevel.Error);
        this.Report();
    }

    private void Record(string stage)
    {
        this.Stages.Add(stage);
        this.Monitor.Log(stage, LogLevel.Info);
        this.Report();
    }

    private void Report() => this.Helper.Data.WriteJsonFile(ReportName, new
    {
        SchemaVersion = 1, ProcessId = Environment.ProcessId, this.FixtureId, this.PostDebit,
        NetworkRole = this.Host ? "Host" : "Client", Phase = this.Write ? "Write" : "Verify", this.Completed, this.Failure,
        this.Stages, this.OriginalInventory, this.OriginalHostInventory, this.OriginalMachine, this.Receipt, this.CommittedInventory,
        this.SavingEvents, this.SavedEvents, this.NativePeerDisconnected, StartTotalDays = this.Bedtime?.StartTotalDays, this.SavedTotalDays,
        ReservedRaw = this.Reservation?.Raw, ReservedHash = this.Reservation?.Hash, ReservedIdentity = this.Reservation?.Identity,
        CancelledIdentity = this.Terminal?.Status == "Cancelled" ? this.Terminal.Identity : null,
        TerminalIdentity = this.Terminal?.Identity, TerminalStatus = this.Terminal?.Status,
        FinalMachine = this.Terminal?.Status == "Applied" ? this.Terminal.After : null,
        OwnerJournal = (this.Reservation ?? this.Terminal)?.OwnerJournal,
        RequestId = (this.Reservation ?? this.Terminal)?.RequestId ?? this.Requested?.Id,
        OriginalSessionId = (this.Reservation ?? this.Terminal)?.Request.SessionId, this.NewSessionId
    });

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public sealed record Signal(string Kind, Guid Instance, Guid Request, string? PlanHash);
    private sealed record Plan(string Raw, string Hash, string Identity, string Status, MachineRequestEnvelope Request,
        ulong World, long Host, string Before, string After, int ChangeIndex, string? OwnerJournal)
    {
        internal Guid RequestId => this.Request.RequestId;
    }
}
