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
            if (this.Host && this.PreparedMachine is null) this.PreparedMachine = PrepareMachine(host);
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

    internal static SObject PrepareMachine(Farmer host)
    {
        GameLocation farm = Game1.getFarm();
        Require(!farm.objects.Values.Any(item => MachineService.TryGetKind(item, out _))
            && !host.modData.ContainsKey(ReservationKey) && !host.modData.ContainsKey(ReceiptKey),
            "The dedicated farm already has a machine transaction record or test device.");
        Vector2 tile = (from y in Enumerable.Range(16, 12)
                        from x in Enumerable.Range(45, 23)
                        let candidate = new Vector2(x, y)
                        where farm.CanItemBePlacedHere(candidate) && farm.CanItemBePlacedHere(candidate + Below)
                        select candidate).First();
        var machine = ItemRegistry.Create<SObject>("(BC)" + Id + "_BeanWorkshop");
        machine.TileLocation = tile;
        machine.modData[MachineService.StateKey] = MachineStateCodec.Write(MachineState.Empty(Guid.NewGuid(), MachineKind.BeanWorkshop));
        farm.objects.Add(tile, machine);
        return machine;
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
            this.CollectRequest = new Request(Guid.NewGuid(), this.InstanceId, 1, owner.UniqueMultiplayerID);
            this.SendTwice(machine, this.CollectRequest, null);
            return;
        }
        if (this.CollectRequest is { } collected && state.Revision == 2 && state.LastRequestId == collected.Id
            && IsSettled(host, owner, collected.Id))
        {
            this.VerifyCollected(owner, state);
            this.Finish(null);
        }
        Require(state.Revision is >= 0 and <= 2, "Unexpected extra machine transaction on the farmhand.");
    }

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
}
