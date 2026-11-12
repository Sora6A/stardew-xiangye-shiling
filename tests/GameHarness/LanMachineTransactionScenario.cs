using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// Disposable loopback farm. The harness prepares one idle device and a farmhand's test stack, then observes
/// the production request, reservation and native replication path. The harness never commits a transaction.
/// </summary>
internal sealed class LanMachineTransactionScenario
{
    private const string Id = MachineService.Prefix;
    private const string HarnessId = Id + ".GameHarness";
    private const string ReadyType = "LanMachineTransactionFixtureReady";
    internal const string ReservationKey = Id + "/HostReservation";
    internal const string JournalKey = Id + "/OwnerCommitJournal";
    internal const string ReceiptKey = Id + "/OwnerReceipt/-5889529773947908937";
    private const string RefusalKey = Id + "/OwnerRefusal/-5889529773947908937";
    internal const string Soybean = "(O)" + Id + "_Soybean";
    internal const string Tofu = "(O)" + Id + "_Tofu";
    internal const string Recipe = Id + "_Recipe_Tofu";
    internal const int InputSlot = 11;
    internal static readonly Vector2 Below = new(0, 1);

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly NativeNetworkBootstrap Bootstrap;
    private readonly bool Host;
    private readonly bool VerifyHostCompetition;
    private readonly bool VerifyFarmhandMenu;
    private readonly Stopwatch Watch = new();
    private readonly List<Evidence> Stages = new();
    private MachineService? Service;
    private SObject? PreparedMachine;
    private Slot?[]? HostInventory, Before, Started, Collected;
    private Request? RefusedRequest, ProposedCancellation, CancellationRequest, ProposedStart, StartRequest, CollectRequest;
    private string? Failure;
    private ReadySignal? Ready;
    private Evidence? LastEvidence;
    private int PreparedRevision = -1;
    private int? ReadyClock;
    private bool Positioned, Completed, SavingObserved, RefusalObserved, CancellationSettled;
    private bool HostPositioned;
    private HostCompetitionEvidence? HostCompetition;
    private CompetitionSetupEvidence? CompetitionSetup;
    private string? SkippedHostArrivalEvent;
    private MachineMenu? ActiveFarmhandMenu;
    private FarmhandMenuEvidence? PendingMenuEvidence;
    private readonly List<FarmhandMenuEvidence> FarmhandMenus = new();
    private Guid? CancelledPlan;
    private Guid InstanceId;
    private ProtocolDiagnosticSnapshot? Protocol;
    private LanTransportDiagnosticSnapshot? Lan;

    internal LanMachineTransactionScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && config.Scenario == "LanMachineTransaction" && config.FarmName == NativeNetworkBootstrap.TransactionFarmName
            && config.SaveFixtureId == NativeNetworkBootstrap.TransactionFixtureId && !config.OpenPreview,
            "Bind the transaction harness to the exact disposable loopback farm.");
        this.Helper = helper;
        this.Monitor = monitor;
        this.Host = config.NetworkRole == "Host";
        this.VerifyHostCompetition = config.VerifyLanHostCompetition;
        this.VerifyFarmhandMenu = config.VerifyLanFarmhandMenu;
        Require(!this.VerifyHostCompetition || !this.VerifyFarmhandMenu, "Run the two native menu fixtures independently.");
        this.Bootstrap = new(monitor, config);
    }

    internal void QueueStart()
    {
        this.Watch.Start();
        this.Bootstrap.QueueStart();
    }

    internal void Tick()
    {
        if (!this.Watch.IsRunning || this.Completed || this.Failure is not null) return;
        try
        {
            this.Bootstrap.Tick();
            Require(this.Bootstrap.Failure is null, this.Bootstrap.Failure ?? "Native loopback setup failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(3), "Timed out waiting for production transaction or receipt settlement.");
            if (!this.Bootstrap.Ready) return;
            Require(Context.IsWorldReady && Context.IsMultiplayer && Context.IsMainPlayer == this.Host
                && !Context.IsSplitScreen && Context.ScreenId == 0 && Game1.uniqueIDForThisGame == NativeNetworkBootstrap.TransactionFixtureId
                && Game1.player.farmName.Value == NativeNetworkBootstrap.TransactionFarmName
                && Game1.player.UniqueMultiplayerID == (this.Host ? NativeNetworkBootstrap.HostId : NativeNetworkBootstrap.ClientId),
                "The active native world or role differs from the transaction fixture.");
            this.Service ??= this.Helper.ModRegistry.GetApi(Id) as MachineService
                ?? throw new InvalidOperationException("The production machine service is unavailable.");
            this.Protocol = this.Service.ReadNetworkDiagnostics();
            this.Lan = this.Service.ReadLanTransportDiagnostics();
            Require(!this.Protocol.Faulted && this.Lan.Configured && this.Lan.HookInstalled,
                "The production LAN provenance hook or transaction bridge is unavailable.");

            Farmer host = Game1.MasterPlayer;
            Require(host.UniqueMultiplayerID == NativeNetworkBootstrap.HostId, "Unexpected native host.");
            if (this.Host && this.PreparedMachine is null)
                this.PreparedMachine = PrepareMachine(host, requireHostStanding: this.VerifyHostCompetition);
            if (this.VerifyHostCompetition && this.Before is null) this.ObserveCompetitionSetup(host);
            if (this.Host && this.VerifyHostCompetition && !this.PrepareCompetingHost(host, this.PreparedMachine!)) return;
            long expectedPeer = this.Host ? NativeNetworkBootstrap.ClientId : NativeNetworkBootstrap.HostId;
            if (!this.Protocol.Peers.Any(peer => peer.PlayerId == expectedPeer && peer.IsHost != this.Host && peer.Ready)) return;
            Farmer? owner = this.Host ? Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: true) : Game1.player;
            if (owner is null) return;
            Require(owner.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId && !ReferenceEquals(owner, host),
                "The native inventory owner is not the dedicated farmhand.");
            GameLocation farm = Game1.getFarm();
            var machine = farm.objects.Values.SingleOrDefault(item => MachineService.TryGetKind(item, out _));
            if (machine is null) return;
            Require(MachineService.TryRead(machine, out var state) && state is { Kind: MachineKind.BeanWorkshop },
                "The replicated device does not have a readable bean workshop state.");
            if (this.InstanceId == Guid.Empty) this.InstanceId = state!.InstanceId;
            Require(state!.InstanceId == this.InstanceId, "The native machine instance changed.");
            if (!this.Host && !this.Positioned) this.Positioned = PrepareOwner(owner, farm, machine);
            if (!ReferenceEquals(owner.currentLocation, farm) || owner.Tile != machine.TileLocation + Below) return;
            // The host puts its stack in place before publishing Ready; the client still has to see the native replica.
            if (this.VerifyHostCompetition && (!ReferenceEquals(host.currentLocation, farm) || host.Tile != machine.TileLocation - Below
                || host.Items[InputSlot] is not { QualifiedItemId: Soybean, Stack: 3, Quality: 0 })) return;

            if (this.HostInventory is null) this.HostInventory = Snapshot(host);
            Require(Snapshot(host).SequenceEqual(this.HostInventory), "The production transaction changed host inventory.");
            if (this.Before is null)
            {
                if (state.Revision != 0 || owner.Items[InputSlot] is not Item input || input.QualifiedItemId != Soybean || input.Stack != 6) return;
                this.CaptureExpectedInventory(owner);
            }
            this.Record(host, owner, state);
            if (this.Host) this.AdvanceHost(host, owner, state);
            else this.AdvanceClient(host, owner, machine, state);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Finish(error is TargetInvocationException { InnerException: { } inner } ? inner.Message : error.Message);
        }
    }

    internal static SObject PrepareMachine(Farmer host, bool requireHostStanding = false)
    {
        GameLocation farm = Game1.getFarm();
        Require(!farm.objects.Values.Any(item => MachineService.TryGetKind(item, out _))
            && !host.modData.ContainsKey(ReservationKey) && !host.modData.ContainsKey(ReceiptKey),
            "The dedicated farm already has a machine transaction record or test device.");
        Vector2 tile = (from y in Enumerable.Range(16, 12)
                        from x in Enumerable.Range(45, 23)
                        let candidate = new Vector2(x, y)
                        where farm.CanItemBePlacedHere(candidate) && farm.CanItemBePlacedHere(candidate + Below)
                            && (!requireHostStanding || farm.CanItemBePlacedHere(candidate - Below))
                        select candidate).First();
        var machine = ItemRegistry.Create<SObject>("(BC)" + Id + "_BeanWorkshop");
        machine.TileLocation = tile;
        machine.modData[MachineService.StateKey] = MachineStateCodec.Write(MachineState.Empty(Guid.NewGuid(), MachineKind.BeanWorkshop));
        farm.objects.Add(tile, machine);
        return machine;
    }

    private bool PrepareCompetingHost(Farmer host, SObject machine)
    {
        Require(ReferenceEquals(host, Game1.player), "Only the dedicated local host can prepare its competition input.");
        var farm = Game1.getFarm();
        Vector2 standing = machine.TileLocation - Below;
        if (!ReferenceEquals(host.currentLocation, farm))
        {
            if (!Game1.isWarping && !Game1.fadeToBlack)
                Game1.warpFarmer(farm.NameOrUniqueName, (int)standing.X, (int)standing.Y, 2);
            return false;
        }
        if (this.Before is null && this.SkippedHostArrivalEvent is null && Game1.eventUp && !Game1.isQuestion
            && Game1.activeClickableMenu is StardewValley.Menus.DialogueBox { isQuestion: false }
            && Game1.CurrentEvent is { skippable: true } arrival)
        {
            // The first Farm arrival can move the host to a story tile after the fixture initially positions it.
            this.SkippedHostArrivalEvent = arrival.id;
            arrival.skipEvent();
            this.Report();
            return false;
        }
        if (Game1.isWarping || Game1.fadeToBlack || Game1.eventUp || !Context.IsPlayerFree) return false;
        if (host.Tile != standing)
        {
            host.setTileLocation(standing);
            host.faceDirection(2);
        }
        if (!this.HostPositioned)
        {
            Require(host.Items.Count > InputSlot && host.Items[InputSlot] is null && host.CurrentToolIndex != InputSlot
                && host.Items.CountId(Soybean) == 0, "The dedicated host must start with an empty competition slot and no soybeans.");
            host.Items[InputSlot] = ItemRegistry.Create(Soybean, 3);
            this.HostPositioned = true;
        }
        return true;
    }

    private void ObserveCompetitionSetup(Farmer host)
    {
        var owner = this.Host ? Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: true) : Game1.player;
        var observed = new CompetitionSetupEvidence(
            host.currentLocation?.NameOrUniqueName, host.Tile.ToString(), host.Items.CountId(Soybean), this.HostPositioned,
            owner?.currentLocation?.NameOrUniqueName, owner?.Tile.ToString(), owner?.Items.CountId(Soybean), this.Positioned,
            this.Protocol!.Peers.Any(peer => peer.Ready), Context.IsPlayerFree, Game1.isWarping, Game1.fadeToBlack,
            Game1.eventUp, Game1.dialogueUp, Game1.CurrentEvent?.id, Game1.CurrentEvent?.skippable, Game1.activeClickableMenu?.GetType().Name);
        if (observed == this.CompetitionSetup) return;
        this.CompetitionSetup = observed;
        this.Report();
    }

    // The real grant is created by the normal production tick. On the next native tick observe it before
    // production can finalize the machine, and run the entire menu action within that one callback.
    [EventPriority(EventPriority.High)]
    internal void OnHostCompetitionTick(object? sender, UpdateTickedEventArgs e)
    {
        if (!this.Host || !this.VerifyHostCompetition || !this.CancellationSettled || this.Completed || this.Failure is not null
            || this.HostCompetition is not null || !this.Bootstrap.Ready || this.PreparedMachine is not { } machine
            || !Context.IsWorldReady || !Context.IsMainPlayer || Game1.uniqueIDForThisGame != NativeNetworkBootstrap.TransactionFixtureId)
            return;
        Farmer host = Game1.player;
        if (!host.modData.TryGetValue(ReservationKey, out string reservedRaw)) return;
        try
        {
            using var document = JsonDocument.Parse(reservedRaw);
            JsonElement reservation = document.RootElement;
            if (reservation.GetProperty("Status").GetString() != "Reserved") return;
            bool readRequest = MachineRequestCodec.TryRead(reservation.GetProperty("RequestRaw").GetString(), out var request);
            Require(readRequest && request is { ActorId: NativeNetworkBootstrap.ClientId, Action: MachineRequestAction.Start, ExpectedRevision: 0 }
                && request.RequestId != this.CancelledPlan && request.InstanceId == this.InstanceId && request.RecipeId == Recipe,
                "The active reservation did not match the competing farmhand's real Start.");
            Farmer owner = Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: true)
                ?? throw new InvalidOperationException("The reserved farmhand must still be connected.");
            var farm = Game1.getFarm();
            Require(ReferenceEquals(host.currentLocation, farm) && host.Tile == machine.TileLocation - Below
                && ReferenceEquals(owner.currentLocation, farm) && owner.Tile == machine.TileLocation + Below
                && !Game1.eventUp && !host.UsingTool && Game1.activeClickableMenu is null && host.Items.CountId(Soybean) == 3,
                "Both actors must stand at the same real device and the host must be able to start the recipe.");
            Require(farm.objects.TryGetValue(machine.TileLocation, out var placed) && ReferenceEquals(placed, machine)
                && MachineService.TryRead(machine, out var state) && state is { Revision: 0, Batch: null },
                "The exact placed device must still be idle before the competing menu action.");
            string machineRaw = machine.modData[MachineService.StateKey];
            Require(machineRaw == reservation.GetProperty("BeforeRaw").GetString(), "The device and live reservation do not match.");
            Item[] hostReferences = host.Items.ToArray(), ownerReferences = owner.Items.ToArray();
            var hostBefore = Snapshot(host);
            var ownerBefore = Snapshot(owner);
            string? ownerJournal = owner.modData.GetValueOrDefault(JournalKey);
            if (!machine.checkForAction(host) || Game1.activeClickableMenu is not MachineMenu menu)
                throw new InvalidOperationException("Native checkForAction did not open the real host machine menu.");
            string feedback;
            try
            {
                Point action = this.Helper.Reflection.GetMethod(menu, "ActionBounds").Invoke<Rectangle>(0).Center;
                menu.receiveLeftClick(action.X, action.Y);
                feedback = this.Helper.Reflection.GetField<string>(menu, "Feedback").GetValue();
                var text = this.Helper.Reflection.GetField<ITranslationHelper>(this.Service!, "Text").GetValue();
                Require(feedback == text.Get("machine.invalid-state").ToString(), "The real host menu did not report the reservation denial.");
            }
            finally
            {
                if (ReferenceEquals(Game1.activeClickableMenu, menu)) Game1.exitActiveMenu();
            }
            Require(machine.modData.GetValueOrDefault(MachineService.StateKey) == machineRaw
                && host.modData.GetValueOrDefault(ReservationKey) == reservedRaw && owner.modData.GetValueOrDefault(JournalKey) == ownerJournal
                && hostReferences.SequenceEqual(host.Items, ReferenceEqualityComparer.Instance)
                && ownerReferences.SequenceEqual(owner.Items, ReferenceEqualityComparer.Instance)
                && hostBefore.SequenceEqual(Snapshot(host)) && ownerBefore.SequenceEqual(Snapshot(owner)),
                "The rejected host click changed the exact machine, reservation, journal or either inventory.");
            this.HostCompetition = new(request!.RequestId, 0, host.Items.CountId(Soybean), owner.Items.CountId(Soybean),
                machineRaw, reservedRaw, feedback);
            this.Monitor.Log("The native host menu refused to start a machine reserved by the connected farmhand.", LogLevel.Info);
            this.Report();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Finish(error is TargetInvocationException { InnerException: { } inner } ? inner.Message : error.Message);
        }
    }

    internal static bool PrepareOwner(Farmer owner, GameLocation farm, SObject machine)
    {
        Require(ReferenceEquals(owner, Game1.player) && owner.Items.Count > InputSlot && owner.Items[InputSlot] is null
            && owner.Items.CountId(Soybean) == 0 && owner.Items.CountId(Tofu) == 0 && !owner.modData.ContainsKey(JournalKey),
            "The dedicated farmhand must start with an empty test slot and no recipe items or journal.");
        var standing = machine.TileLocation + Below;
        if (!ReferenceEquals(owner.currentLocation, farm))
        {
            if (!Game1.isWarping && !Game1.fadeToBlack)
                Game1.warpFarmer(farm.NameOrUniqueName, (int)standing.X, (int)standing.Y, 0);
            return false;
        }
        if (Game1.isWarping || Game1.fadeToBlack || Game1.eventUp || !Context.IsPlayerFree) return false;
        owner.setTileLocation(standing);
        owner.Items[InputSlot] = ItemRegistry.Create(Soybean, 6);
        return true;
    }

    private void CaptureExpectedInventory(Farmer owner)
    {
        this.Before = Snapshot(owner);
        Require(this.Before[InputSlot] is { Id: Soybean, Stack: 6, Quality: 0 } && owner.Items.CountId(Soybean) == 6
            && owner.Items.CountId(Tofu) == 0 && !owner.modData.ContainsKey(JournalKey), "Invalid prepared farmhand inventory.");
        this.Started = this.Before.ToArray();
        this.Started[InputSlot] = this.Before[InputSlot]! with { Stack = 3 };
        this.Collected = this.Started.ToArray();
        int free = Array.FindIndex(this.Collected, slot => slot is null);
        Require(free >= 0, "The farmhand needs an empty slot for the collection.");
        this.Collected[free] = Describe(ItemRegistry.Create(Tofu, 2));
    }

    private void AdvanceHost(Farmer host, Farmer owner, MachineState state)
    {
        if (this.PreparedRevision == -1)
        {
            Require(state.Revision == 0 && state.Batch is null && Snapshot(owner).SequenceEqual(this.Before!), "Idle host state changed during preparation.");
            this.SendReady(0);
            return;
        }
        if (!this.RefusalObserved && state.Revision == 0 && !host.modData.ContainsKey(ReservationKey)
            && host.modData.TryGetValue(RefusalKey, out string rawRefusal) && this.ReadRefusedRequest(rawRefusal) is Guid refused)
        {
            Require(state.Batch is null && Snapshot(owner).SequenceEqual(this.Before!) && !owner.modData.ContainsKey(JournalKey),
                "The native refusal changed the initial device or inventory.");
            this.RefusedRequest = new Request(refused, this.InstanceId, 0, owner.UniqueMultiplayerID);
            this.RefusalObserved = true;
            this.Report();
        }
        if (!this.CancellationSettled && this.CancelledPlan is Guid cancelled && state.Revision == 0 && IsSettled(host, owner, cancelled))
        {
            Require(this.RefusalObserved && state.Batch is null && Snapshot(owner).SequenceEqual(this.Before!),
                "Live cancellation changed the initial device or inventory.");
            this.CancellationSettled = true;
            this.SendReady(0, afterCancellation: true);
            return;
        }
        if (state.Revision == 1 && this.PreparedRevision == 0 && IsSettled(host, owner, state.LastRequestId))
        {
            this.VerifyStarted(owner, state);
            // Prepare a ready batch by advancing only the host's clock. The production code still determines readiness.
            long delta = state.Batch!.ReadyAt - MachineService.Now();
            if (delta > 0)
            {
                int now = Game1.timeOfDay / 100 * 60 + Game1.timeOfDay % 100;
                int ready = checked(now + (int)delta);
                Require(ready < 1440, "Refuse to move the fixture clock across midnight.");
                Game1.timeOfDay = ready / 60 * 100 + ready % 60;
                Game1.netWorldState.Value.UpdateFromGame1();
            }
            this.ReadyClock = Game1.timeOfDay;
            this.SendReady(1);
            return;
        }
        if (state.Revision == 2 && this.PreparedRevision == 1 && IsSettled(host, owner, state.LastRequestId))
        {
            this.VerifyCollected(owner, state);
            Require(this.RefusalObserved && this.CancellationSettled,
                "The host did not observe a refusal and a settled cancellation before actual machine work.");
            Require(!this.VerifyHostCompetition || this.HostCompetition is { } competition
                && this.Stages.Any(evidence => evidence.Revision == 1 && evidence.Request == competition.FarmhandRequest),
                "The real farmhand Start did not follow the verified competing host refusal.");
            this.Finish(null);
        }
        Require(state.Revision is >= 0 and <= 2, "Unexpected extra machine transaction.");
    }

    private void AdvanceClient(Farmer host, Farmer owner, SObject machine, MachineState state)
    {
        if (this.StartRequest is null && this.Ready is { Revision: 0 } start && start.InstanceId == this.InstanceId)
        {
            Require(state.Revision == 0 && Snapshot(owner).SequenceEqual(this.Before!), "Native client state differed before sending Start.");
            if (this.RefusedRequest is null)
            {
                this.RefusedRequest = new Request(Guid.NewGuid(), this.InstanceId, 0, owner.UniqueMultiplayerID);
                Require(this.Send(machine, this.RefusedRequest, Recipe + "_Unknown"), "Production did not send the no-grant fixture intent.");
                this.Report();
                return;
            }
            if (!host.modData.TryGetValue(RefusalKey, out string raw) || this.ReadRefusedRequest(raw) != this.RefusedRequest.Id) return;
            if (this.CancellationRequest is null)
            {
                Require(!host.modData.ContainsKey(ReservationKey) && !owner.modData.ContainsKey(JournalKey),
                    "The native no-grant reply came with a reservation or owner journal.");
                this.ProposedCancellation ??= new Request(Guid.NewGuid(), this.InstanceId, 0, owner.UniqueMultiplayerID);
                if (!this.Send(machine, this.ProposedCancellation, Recipe)) return;
                this.RefusalObserved = true;
                this.CancellationRequest = this.ProposedCancellation;
                var cancel = typeof(MachineService).GetMethod("TryCancelLanFixtureOperation", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("The isolated production cancellation entry is unavailable.");
                Require(cancel.Invoke(this.Service, new object[] { this.CancellationRequest.Id }) is true,
                    "Production refused cancellation of the untouched pending intent.");
                this.Report();
                return;
            }
            if (!start.AfterCancellation || !IsSettled(host, owner, this.CancellationRequest.Id)) return;
            this.CancellationSettled = true;
            if (this.VerifyFarmhandMenu)
            {
                this.StartRequest = this.ClickFarmhandMenu(machine, owner, state, "Start");
                this.Report();
                return;
            }
            this.ProposedStart ??= new Request(Guid.NewGuid(), this.InstanceId, 0, owner.UniqueMultiplayerID);
            if (!this.Send(machine, this.ProposedStart, Recipe)) return;
            this.StartRequest = this.ProposedStart;
            Require(this.Send(machine, this.StartRequest, Recipe), "Production did not send the duplicate Start after a refusal.");
            this.Report();
            return;
        }
        if (this.StartRequest is { } started && this.CollectRequest is null && this.Ready is { Revision: 1 } collect
            && collect.InstanceId == this.InstanceId && state.Revision == 1 && state.LastRequestId == started.Id
            && IsSettled(host, owner, started.Id))
        {
            this.VerifyStarted(owner, state);
            if (this.VerifyFarmhandMenu)
            {
                if (state.PhaseAt(MachineService.Now()) != MachinePhase.Ready || !this.FinishFarmhandMenu(started, "Start")) return;
                this.CollectRequest = this.ClickFarmhandMenu(machine, owner, state, "Collect");
                this.Report();
                return;
            }
            this.CollectRequest = new Request(Guid.NewGuid(), this.InstanceId, 1, owner.UniqueMultiplayerID);
            this.SendTwice(machine, this.CollectRequest, null);
            return;
        }
        if (this.CollectRequest is { } collected && state.Revision == 2 && state.LastRequestId == collected.Id
            && IsSettled(host, owner, collected.Id))
        {
            this.VerifyCollected(owner, state);
            if (this.VerifyFarmhandMenu && !this.FinishFarmhandMenu(collected, "Collect")) return;
            this.Finish(null);
        }
        Require(state.Revision is >= 0 and <= 2, "Unexpected extra machine transaction on the farmhand.");
    }

    private Request ClickFarmhandMenu(SObject machine, Farmer owner, MachineState state, string action)
    {
        Require(ReferenceEquals(owner, Game1.player) && Game1.activeClickableMenu is null,
            "The local farmhand must be free to open its real machine menu.");
        if (!machine.checkForAction(owner) || Game1.activeClickableMenu is not MachineMenu menu)
            throw new InvalidOperationException("Native checkForAction did not open the real farmhand machine menu.");
        string raw = machine.modData[MachineService.StateKey];
        Slot?[] before = Snapshot(owner);
        var pending = this.Helper.Reflection.GetField<Request?>(menu, "FarmhandRequest");
        var feedback = this.Helper.Reflection.GetField<string>(menu, "Feedback");
        Point button = this.Helper.Reflection.GetMethod(menu, "ActionBounds").Invoke<Rectangle>(0).Center;
        menu.receiveLeftClick(button.X, button.Y);
        Request request = pending.GetValue() ?? throw new InvalidOperationException("The actual menu did not queue its request.");
        string waiting = feedback.GetValue();
        Require(request.ActorId == owner.UniqueMultiplayerID && request.InstanceId == state.InstanceId
            && request.ExpectedRevision == state.Revision && !menu.readyToClose()
            && waiting == this.Translate("machine.network-awaiting"),
            "The real menu did not show its bound asynchronous pending state.");
        menu.receiveLeftClick(button.X, button.Y);
        Require(ReferenceEquals(pending.GetValue(), request) && feedback.GetValue() == waiting
            && machine.modData.GetValueOrDefault(MachineService.StateKey) == raw && before.SequenceEqual(Snapshot(owner)),
            "A repeated pending click replaced the request or applied a local change before the host result.");
        this.ActiveFarmhandMenu = menu;
        this.PendingMenuEvidence = new(action, request.Id, state.Revision, waiting, null);
        return request;
    }

    private bool FinishFarmhandMenu(Request request, string action)
    {
        if (this.ActiveFarmhandMenu is not { } menu || !ReferenceEquals(Game1.activeClickableMenu, menu)
            || this.PendingMenuEvidence is not { } evidence || evidence.Request != request.Id || evidence.Action != action)
            throw new InvalidOperationException("The real farmhand menu did not remain bound through its result.");
        if (this.Helper.Reflection.GetField<Request?>(menu, "FarmhandRequest").GetValue() is not null) return false;

        bool starting = action == "Start";
        string completion = this.Helper.Reflection.GetField<string>(menu, "Feedback").GetValue();
        Require(menu.readyToClose() && this.Helper.Reflection.GetField<Color>(menu, "FeedbackColor").GetValue() == Color.DarkGreen
            && completion == this.Translate(starting ? "machine.started" : "machine.collected")
                + " " + this.Translate(starting ? "machine.close-to-process" : "machine.reopen"),
            "The real farmhand menu did not display the successful native host result.");
        this.FarmhandMenus.Add(evidence with { Completion = completion });
        this.PendingMenuEvidence = null;
        this.ActiveFarmhandMenu = null;
        menu.exitThisMenu();
        Require(Game1.activeClickableMenu is null, "The settled farmhand menu did not close through its original path.");
        this.Monitor.Log($"The real farmhand menu completed {action} after the host result.", LogLevel.Info);
        return true;
    }

    private string Translate(string key) =>
        this.Helper.Reflection.GetField<ITranslationHelper>(this.Service!, "Text").GetValue().Get(key).ToString();

    private void VerifyStarted(Farmer owner, MachineState state) => Require(state.Revision == 1
        && state.Batch is { RecipeId: Recipe, Output: { ItemId: Tofu, Quantity: 2 } }
        && state.Batch.Inputs.Sum(input => input.Quantity) == 3 && state.LastActorId == owner.UniqueMultiplayerID
        && Snapshot(owner).SequenceEqual(this.Started!), "Start did not debit exactly three soybeans and advance once.");

    private void VerifyCollected(Farmer owner, MachineState state) => Require(state.Revision == 2 && state.Batch is null
        && state.LastActorId == owner.UniqueMultiplayerID && Snapshot(owner).SequenceEqual(this.Collected!),
        "Collect did not credit exactly two tofu and clear the batch once.");

    private static bool IsSettled(Farmer host, Farmer owner, Guid? request) => request is Guid id
        && !host.modData.ContainsKey(ReservationKey) && !owner.modData.ContainsKey(JournalKey)
        && host.modData.TryGetValue(ReceiptKey, out string receipt)
        && receipt.StartsWith(FormattableString.Invariant($"1|{NativeNetworkBootstrap.TransactionFixtureId}|{NativeNetworkBootstrap.HostId}|{id:D}|"),
            StringComparison.Ordinal)
        && receipt.EndsWith(":Seen", StringComparison.Ordinal);

    private void SendReady(int revision, bool afterCancellation = false)
    {
        this.PreparedRevision = revision;
        this.Helper.Multiplayer.SendMessage(new ReadySignal(this.InstanceId, revision, afterCancellation), ReadyType, new[] { HarnessId },
            new[] { NativeNetworkBootstrap.ClientId });
        this.Report();
    }

    private void SendTwice(SObject machine, Request request, string? recipe)
    {
        Require(this.Send(machine, request, recipe) && this.Send(machine, request, recipe),
            "Production declined to send the exact duplicate transaction intent.");
        this.Monitor.Log($"Sent the same production revision {request.ExpectedRevision} intent twice.", LogLevel.Info);
        this.Report();
    }

    private bool Send(SObject machine, Request request, string? recipe)
    {
        var send = typeof(MachineService).GetMethod("TryQueueLanFixtureOperation", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The isolated production transaction entry is unavailable.");
        return send.Invoke(this.Service, new object?[] { machine, request, recipe }) is true;
    }

    private Guid? ReadRefusedRequest(string raw)
    {
        string prefix = FormattableString.Invariant($"1|{NativeNetworkBootstrap.TransactionFixtureId}|{NativeNetworkBootstrap.HostId}|{NativeNetworkBootstrap.ClientId}|");
        string[] fields = raw.Split('|');
        return raw.StartsWith(prefix, StringComparison.Ordinal) && fields.Length == 8
            && Guid.TryParseExact(fields[4], "D", out Guid session) && session == this.Protocol?.SessionId
            && Guid.TryParseExact(fields[5], "D", out Guid nonce) && nonce != Guid.Empty
            && Guid.TryParseExact(fields[6], "D", out Guid request) && request != Guid.Empty && fields[7].Length == 64
            ? request : null;
    }

    internal void OnMessage(ModMessageReceivedEventArgs args)
    {
        if (this.Host || this.Completed || this.Failure is not null || !this.Bootstrap.Ready || args.FromModID != HarnessId
            || args.FromPlayerID != NativeNetworkBootstrap.HostId || args.Type != ReadyType) return;
        try
        {
            var ready = args.ReadAs<ReadySignal>();
            if (ready is not null && ready.InstanceId == this.InstanceId && ready.Revision is 0 or 1
                && (this.Ready is null || ready.Revision > this.Ready.Revision
                    || ready.Revision == this.Ready.Revision && (!this.Ready.AfterCancellation || ready.AfterCancellation))) this.Ready = ready;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.Message); }
    }

    internal void OnSaving()
    {
        this.SavingObserved = true;
        this.Finish("Unexpected native save during the no-save transaction fixture.");
    }

    internal void OnReturnedToTitle()
    {
        if (this.Bootstrap.Ready && !this.Completed && this.Failure is null)
            this.Finish("Returned to title before the transaction settled.");
    }

    private void Record(Farmer host, Farmer owner, MachineState state)
    {
        string? status = null;
        if (host.modData.TryGetValue(ReservationKey, out string raw))
        {
            using var data = JsonDocument.Parse(raw);
            status = data.RootElement.GetProperty("Status").GetString();
            if (this.Host && status == "Cancelled")
            {
                Require(MachineRequestCodec.TryRead(data.RootElement.GetProperty("RequestRaw").GetString(), out var request)
                    && request!.ActorId == owner.UniqueMultiplayerID && state.Revision == 0 && state.Batch is null,
                    "Cancelled reservation does not match the real idle machine and owner.");
                this.CancelledPlan = request!.RequestId;
            }
        }
        string? journal = owner.modData.GetValueOrDefault(JournalKey);
        string? receipt = host.modData.GetValueOrDefault(ReceiptKey);
        var evidence = new Evidence(state.Revision, state.LastRequestId, status, journal, receipt,
            owner.Items.CountId(Soybean), owner.Items.CountId(Tofu));
        if (evidence == this.LastEvidence) return;
        Require(this.Stages.Count < 64, "Too many distinct native transaction observations.");
        this.LastEvidence = evidence;
        this.Stages.Add(evidence);
        this.Report();
    }

    private void Finish(string? failure)
    {
        this.Failure = failure;
        this.Completed = failure is null && !this.SavingObserved;
        this.Bootstrap.RestoreOptions();
        this.Report();
        this.Monitor.Log(failure ?? "Native production Start and Collect settled.", failure is null ? LogLevel.Info : LogLevel.Error);
    }

    private void Report() => this.Helper.Data.WriteJsonFile("lan-machine-transaction-result.json", new
    {
        SchemaVersion = 1, ProcessId = Environment.ProcessId, FixtureId = NativeNetworkBootstrap.TransactionFixtureId,
        NetworkRole = this.Host ? "Host" : "Client", this.Completed, this.Failure, this.SavingObserved, this.InstanceId,
        this.PreparedRevision, this.ReadyClock, this.Positioned, this.RefusalObserved, RefusedRequest = this.RefusedRequest?.Id,
        this.CancellationSettled, CancelledRequest = this.Host ? this.CancelledPlan : this.CancellationRequest?.Id,
        StartRequest = this.StartRequest?.Id, CollectRequest = this.CollectRequest?.Id,
        this.VerifyHostCompetition, this.HostCompetition, this.CompetitionSetup, this.SkippedHostArrivalEvent,
        this.VerifyFarmhandMenu, this.PendingMenuEvidence, this.FarmhandMenus,
        this.Stages, this.Protocol, this.Lan
    });

    private static Slot?[] Snapshot(Farmer farmer) => farmer.Items.Select(Describe).ToArray();
    private static Slot? Describe(Item? item) => item is null ? null : new(item.QualifiedItemId, item.Stack, item.Quality,
        JsonSerializer.Serialize(item.modData.Pairs.OrderBy(pair => pair.Key, StringComparer.Ordinal)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record ReadySignal(Guid InstanceId, int Revision, bool AfterCancellation);
    private sealed record Slot(string Id, int Stack, int Quality, string Metadata);
    private sealed record Evidence(long Revision, Guid? Request, string? Reservation, string? Journal, string? Receipt, int Soybeans, int Tofu);
    private sealed record HostCompetitionEvidence(Guid FarmhandRequest, long Revision, int HostSoybeans, int FarmhandSoybeans,
        string MachineRaw, string ReservationRaw, string Feedback);
    private sealed record CompetitionSetupEvidence(string? HostLocation, string HostTile, int HostSoybeans, bool HostPositioned,
        string? FarmhandLocation, string? FarmhandTile, int? FarmhandSoybeans, bool FarmhandPositioned, bool PeerReady,
        bool PlayerFree, bool Warping, bool FadeToBlack, bool EventUp, bool DialogueUp, string? EventId, bool? EventSkippable, string? Menu);
    private sealed record FarmhandMenuEvidence(string Action, Guid Request, long Revision, string Waiting, string? Completion);
}
