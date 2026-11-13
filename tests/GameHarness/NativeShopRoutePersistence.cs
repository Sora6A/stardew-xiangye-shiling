using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Checks the actual end-of-route objects through one original bedtime and a separate process load.</summary>
internal sealed class NativeShopRoutePersistence
{
    internal const string Scenario = "NativeShopWalkPersist";
    internal const string Farm = "XYSP1WalkPersist";
    internal const ulong FixtureId = 620261121;
    private const string WriteReport = "native-shop-walk-persist-write-result.json";
    private const string VerifyReport = "native-shop-walk-persist-verify-result.json";
    private const string Id = MachineService.Prefix;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly List<string> Steps = new();
    private NativeBedtimeInteraction? Bedtime;
    private (SObject Bean, SObject Crock) Machines;
    private Checkpoint? BeforeSleep;
    private Checkpoint? AfterLoad;
    private int SavingEvents, SavedEvents;
    private int? WriteProcessId;
    internal bool Verifying { get; }
    internal bool SleepStarted => this.Bedtime is not null;

    internal NativeShopRoutePersistence(IModHelper helper, IMonitor monitor, bool verify)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        this.Verifying = verify;
    }

    internal static bool IsBound(Config config) => config.Scenario == Scenario && config.FarmName == Farm
        && config.SaveFixtureId == FixtureId && config.NativeRecoveryPhase is "Write" or "Verify";

    internal void BeginBedtime((SObject Bean, SObject Crock) machines)
    {
        Require(!this.Verifying && !this.SleepStarted, "Bedtime is only available after the disposable route.");
        this.Machines = machines;
        this.BeforeSleep = Capture(machines);
        this.Bedtime = new NativeBedtimeInteraction();
        this.Record("Captured the original collected machines and inventory before native bedtime.");
    }

    internal bool TickBedtime()
    {
        if (this.Bedtime is null) throw new InvalidOperationException("Native bedtime has not started.");
        if (this.SavedEvents == 0)
        {
            this.Bedtime.Tick(this.Record);
            return false;
        }
        this.AfterLoad = Capture(this.Machines);
        Require(this.AfterLoad.TotalDays == this.Bedtime.StartTotalDays + 1, "Native bedtime must advance exactly one day.");
        CheckUnchanged(this.BeforeSleep!, this.AfterLoad);
        this.Record("The saved morning retains the original devices and inventory.");
        return true;
    }

    internal void OnSaving()
    {
        Require(++this.SavingEvents == 1 && this.Bedtime?.AnswerSent == true && !this.Verifying,
            "Saving did not follow the single authorized native bedtime answer.");
        this.Record("Observed SMAPI Saving.");
    }

    internal void OnSaved()
    {
        Require(++this.SavedEvents == 1 && this.SavingEvents == 1 && !this.Verifying,
            "Saved did not follow the single authorized native save.");
        this.Record("Observed SMAPI Saved.");
    }

    internal void VerifyColdLoad()
    {
        var report = this.Helper.Data.ReadJsonFile<WriteResult>(WriteReport);
        if (report is not { Completed: true, FixtureId: FixtureId, Persistence: { SavingEvents: 1, SavedEvents: 1, AfterLoad: { } saved } }
            || report.ProcessId == Environment.ProcessId)
            throw new InvalidOperationException("A completed native bedtime report from a separate process is required.");

        this.WriteProcessId = report.ProcessId;
        var house = Game1.player.currentLocation as FarmHouse ?? throw new InvalidOperationException("Cold load did not start in the farmhouse.");
        var machines = (Bean: house.objects[saved.Bean.Tile], Crock: house.objects[saved.Crock.Tile]);
        this.AfterLoad = Capture(machines);
        Require(this.AfterLoad.TotalDays == saved.TotalDays && Game1.timeOfDay == 600, "The cold load did not start the saved morning.");
        CheckUnchanged(saved, this.AfterLoad);
        this.Record("Cold load preserved both machine identities, their raw state, and exact route inventory and recipes.");

        var service = this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
            ?? throw new InvalidOperationException("Production MachineService is unavailable.");
        Farmer farmer = Game1.player;
        Item[] slots = farmer.Items.ToArray();
        foreach (SObject machine in new[] { machines.Bean, machines.Crock })
        {
            var state = ReadCollected(machine);
            farmer.setTileLocation(machine.TileLocation + Vector2.UnitY);
            var replay = new Request(state.LastRequestId!.Value, state.InstanceId, state.Revision - 1, farmer.UniqueMultiplayerID);
            var fresh = new Request(Guid.NewGuid(), state.InstanceId, state.Revision, farmer.UniqueMultiplayerID);
            OperationResult replayResult = service.Execute(machine, farmer, replay, null);
            OperationResult freshResult = service.Execute(machine, farmer, fresh, null);
            Require(!replayResult.Applied && !freshResult.Applied && replayResult.Message != freshResult.Message,
                "Production did not distinguish the replay from a new attempt to collect an empty machine.");
            this.Record($"{state.Kind}: replay refused ({replayResult.Message}); empty collection refused ({freshResult.Message}).");
        }
        CheckUnchanged(saved, Capture(machines));
        Require(slots.SequenceEqual(farmer.Items, ReferenceEqualityComparer.Instance), "A refused collection replaced native inventory items.");
    }

    internal void Report(string? failure, object? walk, object? homeEntry, object? processing) => this.Helper.Data.WriteJsonFile(
        this.Verifying ? VerifyReport : WriteReport, new
        {
            SchemaVersion = 1, ProcessId = Environment.ProcessId, FixtureId, Phase = this.Verifying ? "Verify" : "Write",
            Completed = failure is null, Failure = failure, Walk = walk, HomeEntry = homeEntry, Processing = processing,
            Persistence = new { this.WriteProcessId, this.SavingEvents, this.SavedEvents, this.BeforeSleep, this.AfterLoad, this.Steps }
        });

    private void Record(string step)
    {
        this.Steps.Add(step);
        this.Monitor.Log(step, LogLevel.Info);
    }

    private static Checkpoint Capture((SObject Bean, SObject Crock) machines)
    {
        Farmer farmer = Game1.player;
        var house = farmer.currentLocation as FarmHouse ?? throw new InvalidOperationException("The route is no longer in its farmhouse.");
        Require(house.objects.Values.Count(machine => MachineService.TryGetKind(machine, out _)) == 2,
            "Expected exactly the two devices placed during the route.");
        var inventory = farmer.Items.Select(item => item is null ? null : new Slot(item.QualifiedItemId, item.Stack, item.Quality)).ToArray();
        Require(inventory.Count(item => item is not null) == 2 && inventory.Contains(new Slot("(O)" + Id + "_Tofu", 1, 0))
            && inventory.Contains(new Slot("(O)" + Id + "_SoySauce", 4, 0)), "Wrong final route inventory.");
        var recipes = new Recipes(farmer.craftingRecipes.GetValueOrDefault(Id + "_Recipe_BeanWorkshop", -1),
            farmer.craftingRecipes.GetValueOrDefault(Id + "_Recipe_FermentationCrock", -1),
            farmer.cookingRecipes.GetValueOrDefault(Id + "_Recipe_ToonTofu", -1), farmer.recipesCooked.GetValueOrDefault(Id + "_ToonTofu"));
        Require(recipes is { Workshop: >= 0, Crock: >= 0, Dish: >= 0, Cooked: 1 }, "The three purchased recipes or cooked count differ.");
        return new(Game1.Date.TotalDays, farmer.UniqueMultiplayerID, farmer.Money, inventory, recipes,
            ReadAt(machines.Bean, MachineKind.BeanWorkshop), ReadAt(machines.Crock, MachineKind.FermentationCrock));

        SavedMachine ReadAt(SObject machine, MachineKind kind)
        {
            var state = ReadCollected(machine);
            Require(state.Kind == kind && house.objects.TryGetValue(machine.TileLocation, out var placed) && ReferenceEquals(machine, placed),
                "A placed device moved or was replaced before the checkpoint.");
            return new((int)machine.TileLocation.X, (int)machine.TileLocation.Y, state.InstanceId, machine.modData[MachineService.StateKey]);
        }
    }

    private static MachineState ReadCollected(SObject machine) => MachineService.TryRead(machine, out var state)
        && state is { Revision: 2, Batch: null, LastRequestId: not null } && state.LastActorId == Game1.player.UniqueMultiplayerID
        ? state : throw new InvalidOperationException("A device is not idle with the original collection receipt.");

    private static void CheckUnchanged(Checkpoint before, Checkpoint after) => Require(before.Bean == after.Bean && before.Crock == after.Crock
        && before.Inventory.SequenceEqual(after.Inventory) && before.Recipes == after.Recipes && before.Money == after.Money && before.FarmerId == after.FarmerId,
        "A machine, inventory slot, recipe, balance or farmer identity changed across the checkpoint.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record WriteResult(bool Completed, int ProcessId, ulong FixtureId, WritePersistence Persistence);
    private sealed record WritePersistence(int SavingEvents, int SavedEvents, Checkpoint AfterLoad);
    private sealed record Checkpoint(int TotalDays, long FarmerId, int Money, Slot?[] Inventory, Recipes Recipes, SavedMachine Bean, SavedMachine Crock);
    private sealed record Slot(string ItemId, int Stack, int Quality);
    private sealed record Recipes(int Workshop, int Crock, int Dish, int Cooked);
    private sealed record SavedMachine(int X, int Y, Guid Instance, string Raw)
    {
        internal Vector2 Tile => new(this.X, this.Y);
    }
}
