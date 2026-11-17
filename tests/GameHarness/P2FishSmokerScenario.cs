using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Locations;
using XiangyeShiling.GameIntegration;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Proves the installed CP extension through native loading, machine time and collection.</summary>
internal sealed class P2FishSmokerScenario
{
    internal const string Scenario = "P2FishSmoker", FarmName = "XYSP2Smoker";
    internal const ulong FixtureId = 620261129;
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private const string Prefix = MachineService.Prefix + "_", Id = "(O)" + Prefix;
    private const string Smoker = "(BC)FishSmoker", Coal = "(O)382", Wood = "(O)388", Sunfish = "(O)145";
    private const string VanillaRule = "SmokedFish", VanillaOutput = "(O)SmokedFish";
    private const string VanillaQuery = "FLAVORED_ITEM SmokedFish DROP_IN_ID DROP_IN_QUALITY";
    private static readonly Vector2 Below = Vector2.UnitY;

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<string> Evidence = new();
    private readonly List<RunningBatch> Batches = new();
    private readonly List<SObject> Fixtures = new();
    private readonly List<ClockSample> Samples = new();
    private FarmHouse? House;
    private Item[]? Original;
    private Vector2 OriginalTile;
    private int OriginalToolIndex, OriginalFacing;
    private bool OriginalPause;
    private int RemainingLoadTicks = -1, LoadCount, SavingEvents, StartDay, StartTime, LastTime;
    private bool Finished;
    private string? Failure;
    private static Farmer Player => Game1.player;

    internal P2FishSmokerScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "Bind the Fish Smoker test to its exact disposable farm.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.RemainingLoadTicks = 180;
    internal void OnLoaded() => this.LoadCount++;
    internal void OnSaving() { this.SavingEvents++; this.Failure = "Unexpected native save during the Fish Smoker test."; }
    internal void OnReturnedToTitle() { if (this.LoadCount > 0 && !this.Finished) this.Failure = "Returned to title before the Fish Smoker test finished."; }

    internal void Tick()
    {
        if (this.Finished) { Game1.game1.Exit(); return; }
        try
        {
            if (this.RemainingLoadTicks > 0 && --this.RemainingLoadTicks == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "Missing Fish Smoker fixture or another world is already loaded.");
                Game1.activeClickableMenu = null;
                this.Watch.Start();
                SaveGame.Load(SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "The Fish Smoker test failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(6), "Timed out waiting for the bound farm or original machine clock.");
            if (this.LoadCount == 0 || !Context.IsWorldReady) return;
            Require(this.LoadCount == 1 && MatchesWorld(), "The active world or farmer differs from the smoker fixture.");
            if (!Context.IsPlayerFree || Game1.eventUp || Game1.isWarping || Game1.fadeToBlack) return;
            if (this.Original is null) this.Prepare();
            else this.Advance();
        }
        catch (Exception error) { this.Finish(error.ToString()); }
    }

    private static bool MatchesWorld() => Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen
        && Constants.SaveFolderName == SaveName && Game1.uniqueIDForThisGame == FixtureId && Player.farmName.Value == FarmName && Player.Name == FarmName;

    private void Prepare()
    {
        this.House = Player.currentLocation as FarmHouse ?? throw new InvalidOperationException("Load the fixture inside the native farmhouse.");
        Require(Game1.timeOfDay is >= 600 and <= 900 && Player.MaxItems >= 12, "Use an untouched morning fixture with a twelve-slot toolbar.");
        var data = this.Helper.GameContent.Load<Dictionary<string, MachineData>>("Data/Machines")[Smoker];
        var vanilla = data.OutputRules.Single(rule => rule.Id == VanillaRule);
        Require(data.OutputRules[0] == vanilla && vanilla.Triggers.Single() is { Trigger: MachineOutputTrigger.ItemPlacedInMachine,
                RequiredItemId: null, RequiredCount: 1 } fish && fish.RequiredTags?.SequenceEqual(new[] { "category_fish" }) == true
            && vanilla.OutputItem.Single().ItemId == VanillaQuery && vanilla.MinutesUntilReady == 50 && vanilla.DaysUntilReady == -1
            && data.AdditionalConsumedItems.Single() is { ItemId: Coal, RequiredCount: 1 },
            "The original first Fish Smoker rule or the single machine-level coal cost was changed.");
        var recipes = new[]
        {
            Describe(data, "SmokedPork", Id + "Pork"), Describe(data, "SmokedTofu", Id + "DriedTofu"),
            new SmokerRecipe(VanillaRule, Sunfish, 1, VanillaOutput, 1, 1, 50)
        };
        this.Record("Effective Data/Machines kept the original first fish rule and one machine-level coal; both exact custom rules followed it.");

        this.Original = Player.Items.ToArray();
        this.OriginalTile = Player.Tile;
        this.OriginalToolIndex = Player.CurrentToolIndex;
        this.OriginalFacing = Player.FacingDirection;
        this.OriginalPause = Game1.options.pauseWhenOutOfFocus;
        Game1.options.pauseWhenOutOfFocus = false;
        var slots = new Item[Player.MaxItems];
        for (int index = 0; index < recipes.Length; index++)
        {
            SmokerRecipe recipe = recipes[index];
            slots[index] = ItemRegistry.Create(recipe.Input, 2 * recipe.InputCount + 1, quality: 1);
            Require(slots[index].GetContextTags().Contains("category_fish") == (recipe.Rule == VanillaRule),
                "The vanilla fish category must match only the fish fixture input.");
        }
        slots[3] = ItemRegistry.Create(Wood, 2);
        Player.Items.OverwriteWith(slots);
        foreach (SmokerRecipe recipe in recipes)
        {
            SObject machine = this.Place();
            this.Batches.Add(new(machine, recipe));
            this.Reject(machine, recipe.Input, "without coal");
        }

        this.StartDay = Game1.Date.TotalDays;
        this.StartTime = this.LastTime = Game1.timeOfDay;
        foreach (RunningBatch batch in this.Batches)
        {
            var recipe = batch.Recipe;
            if (Count(Coal) == 0) Require(Player.addItemToInventory(ItemRegistry.Create(Coal, 1)) is null, "Fixture coal did not fit.");
            Require(Count(Coal) == 1, "Begin the native placement attempt with exactly one coal.");
            this.Reject(batch.Device, Wood, "with a wrong drop-in and coal");

            var before = Snapshot();
            Require(this.Drop(batch.Device, recipe.Input, probe: true) && before.SequenceEqual(Snapshot()) && batch.Device.heldObject.Value is null,
                $"The native probe for {recipe.Rule} rejected the input or changed state.");
            int inputBefore = Count(recipe.Input);
            Require(this.Drop(batch.Device, recipe.Input) && Count(recipe.Input) == inputBefore - recipe.InputCount
                && Count(Coal) == 0 && Count(recipe.Output) == 0 && UnrelatedUnchanged(before, recipe.Input, Coal)
                && batch.Device.lastOutputRuleId.Value == recipe.Rule && batch.Device.minutesUntilReady.Value == recipe.Minutes
                && !batch.Device.readyForHarvest.Value && MatchesOutput(batch.Device.heldObject.Value, recipe),
                $"Native start of {recipe.Rule} did not take exactly the configured ingredient and one coal for its expected output.");
            this.NoteNoCollection(batch);
            Require(Player.addItemToInventory(ItemRegistry.Create(Coal, 1)) is null, "Fixture busy-test coal did not fit.");
            this.Reject(batch.Device, recipe.Input, "while busy with another affordable batch");
            this.Record($"{recipe.Rule} probe and rejected inputs were read-only; native start used {recipe.InputCount} {recipe.Input} and one coal.");
        }
    }

    private static SmokerRecipe Describe(MachineData data, string suffix, string input)
    {
        string ruleId = Prefix + "Recipe_" + suffix, outputId = Id + suffix;
        var rule = data.OutputRules.Single(entry => entry.Id == ruleId);
        var trigger = rule.Triggers.Single();
        var output = rule.OutputItem.Single();
        Require(trigger.Trigger == MachineOutputTrigger.ItemPlacedInMachine && trigger.RequiredItemId == input
            && trigger.RequiredCount is >= 1 and <= 499 && trigger.RequiredTags is null && trigger.Condition is null
            && rule.MinutesUntilReady is >= 1 and <= 420 && rule.DaysUntilReady == -1
            && output.ItemId == outputId && output.Quality == 0 && !output.CopyQuality
            && output.MinStack is >= 1 and <= 999 && output.MinStack == output.MaxStack && output.OutputMethod is null,
            $"The effective CP rule {ruleId} does not declare an exact drop-in, fixed output and native minutes.");
        return new(ruleId, input, trigger.RequiredCount, outputId, output.MinStack, 0, rule.MinutesUntilReady);
    }

    private void Advance()
    {
        Require(ReferenceEquals(Player.currentLocation, this.House) && Game1.Date.TotalDays == this.StartDay, "The native farmhouse or test day changed.");
        int now = Game1.timeOfDay;
        if (now == this.LastTime) return;
        Require(Minutes(now) - Minutes(this.LastTime) == 10, "The original clock skipped or repeated a ten-minute step.");
        int elapsed = Minutes(now) - Minutes(this.StartTime);
        this.LastTime = now;
        foreach (RunningBatch batch in this.Batches.Where(batch => !batch.Collected))
        {
            var recipe = batch.Recipe;
            Require(batch.Device.readyForHarvest.Value == (elapsed >= recipe.Minutes) && MatchesOutput(batch.Device.heldObject.Value, recipe),
                $"{recipe.Rule} reported the wrong native readiness or held output at minute {elapsed}.");
            if (elapsed < recipe.Minutes) continue;
            this.StandAt(batch.Device);
            var before = Snapshot();
            Require(batch.Device.checkForAction(Player) && batch.Device.heldObject.Value is null && !batch.Device.readyForHarvest.Value
                && Count(recipe.Output) == recipe.OutputCount && UnrelatedUnchanged(before, recipe.Output)
                && MatchesOutput(Player.Items.OfType<SObject>().SingleOrDefault(item => item.QualifiedItemId == recipe.Output), recipe),
                $"Native collection of {recipe.Rule} yielded the wrong inventory, flavor, quantity or quality.");
            this.NoteNoCollection(batch);
            batch.Collected = true;
            this.Record($"{recipe.Rule} collected {recipe.OutputCount} {recipe.Output} at the first native ready tick after {elapsed} minutes, without duplicate collection.");
        }
        this.Samples.Add(new(now, elapsed, this.Batches.Where(batch => batch.Collected).Select(batch => batch.Recipe.Rule).ToArray()));
        if (this.Batches.All(batch => batch.Collected)) this.Finish(null);
    }

    private bool Drop(SObject smoker, string item, bool probe = false)
    {
        this.StandAt(smoker);
        int index = Player.Items.ToList().FindIndex(entry => entry?.QualifiedItemId == item);
        Require(index is >= 0 and < 12, $"The fixture drop-in {item} is not in the toolbar.");
        Player.CurrentToolIndex = index;
        var held = Player.ActiveObject ?? throw new InvalidOperationException($"Missing active drop-in {item}.");
        return smoker.performObjectDropInAction(held, probe, Player, returnFalseIfItemConsumed: false);
    }

    private void Reject(SObject smoker, string item, string reason)
    {
        var before = Snapshot();
        SObject? original = smoker.heldObject.Value;
        int minutes = smoker.minutesUntilReady.Value;
        Require(!this.Drop(smoker, item) && before.SequenceEqual(Snapshot())
            && ReferenceEquals(smoker.heldObject.Value, original) && smoker.minutesUntilReady.Value == minutes,
            $"The native smoker changed its state or inventory {reason}.");
    }

    private void NoteNoCollection(RunningBatch batch)
    {
        this.StandAt(batch.Device);
        var before = Snapshot();
        SObject? held = batch.Device.heldObject.Value;
        _ = batch.Device.checkForAction(Player);
        Require(Snapshot().SequenceEqual(before) && ReferenceEquals(batch.Device.heldObject.Value, held),
            $"Busy or empty {batch.Recipe.Rule} granted another output.");
    }

    private void StandAt(SObject smoker)
    {
        Require(this.House!.objects.TryGetValue(smoker.TileLocation, out SObject found) && ReferenceEquals(found, smoker),
            "The native smoker no longer occupies its fixture tile.");
        Player.setTileLocation(smoker.TileLocation + Below);
        Player.faceDirection(0);
    }

    private SObject Place()
    {
        var house = this.House!;
        SObject carried = ItemRegistry.Create<SObject>(Smoker);
        Require(Player.addItemToInventory(carried) is null, "A fixture smoker did not fit.");
        Player.CurrentToolIndex = Player.Items.ToList().FindIndex(item => ReferenceEquals(item, carried));
        var map = house.Map.Layers[0];
        for (int y = 1; y + 2 < map.LayerHeight; y++)
        for (int x = 1; x + 1 < map.LayerWidth; x++)
        {
            Vector2 tile = new(x, y);
            if (house.isTileOnWall(x, y) || house.isTileOnWall(x, y + 1) || !house.CanItemBePlacedHere(tile)
                || !house.CanItemBePlacedHere(tile + Below, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
                || this.Fixtures.Any(machine => tile == machine.TileLocation + Below)
                || house.doesTileHaveProperty(x, y + 1, "TouchAction", "Back") is not null) continue;
            Player.setTileLocation(tile + Below);
            Player.faceDirection(0);
            if (!Utility.playerCanPlaceItemHere(house, carried, x * 64 + 32, y * 64 + 32, Player)) continue;
            Require(Utility.tryToPlaceItem(house, carried, x * 64 + 32, y * 64 + 32) && Count(Smoker) == 0
                && house.objects[tile].QualifiedItemId == Smoker, "The original placement path did not place the fixture smoker.");
            SObject placed = house.objects[tile];
            this.Fixtures.Add(placed);
            return placed;
        }
        throw new InvalidOperationException("No accessible free tile for a native Fish Smoker.");
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        this.Watch.Stop();
        if (this.Original is not null)
        {
            Game1.options.pauseWhenOutOfFocus = this.OriginalPause;
            if (MatchesWorld())
            {
                foreach (var machine in this.Fixtures)
                    if (this.House!.objects.TryGetValue(machine.TileLocation, out SObject found) && ReferenceEquals(found, machine))
                        this.House.removeObject(machine.TileLocation, false);
                Player.Items.OverwriteWith(this.Original);
                Player.CurrentToolIndex = this.OriginalToolIndex;
                Player.setTileLocation(this.OriginalTile);
                Player.faceDirection(this.OriginalFacing);
            }
        }
        this.Helper.Data.WriteJsonFile("p2-fish-smoker-result.json", new
        {
            SchemaVersion = 1, Scenario, FixtureId, ProcessId = Environment.ProcessId, Completed = failure is null,
            Failure = failure, this.LoadCount, this.SavingEvents, this.StartDay, this.StartTime, this.Samples, this.Evidence,
            Recipes = this.Batches.Select(batch => batch.Recipe).ToArray(),
            Preparation = "The fixture supplies machines and inventory and sets positions. Original placement, drop-in, native clock and collection run without saving."
        });
        this.Monitor.Log(failure ?? "P2 native Fish Smoker and vanilla fish smoke tests passed.", failure is null ? LogLevel.Info : LogLevel.Error);
    }

    private static bool MatchesOutput(SObject? item, SmokerRecipe recipe) => item is not null && item.QualifiedItemId == recipe.Output
        && item.Stack == recipe.OutputCount && item.Quality == recipe.Quality
        && (recipe.Rule != VanillaRule || (item.preserve.Value == SObject.PreserveType.SmokedFish
            && ItemRegistry.QualifyItemId(item.preservedParentSheetIndex.Value) == Sunfish));
    private static StackValue[] Snapshot() => Player.Items.Select(item => new StackValue(item?.QualifiedItemId, item?.Stack ?? 0, item?.Quality ?? 0)).ToArray();
    private static bool UnrelatedUnchanged(StackValue[] before, params string[] allowed) => before.Zip(Snapshot()).All(pair =>
        pair.First == pair.Second || ((pair.First.Id is null || allowed.Contains(pair.First.Id))
            && (pair.Second.Id is null || allowed.Contains(pair.Second.Id))));
    private static int Count(string id) => Player.Items.CountId(id);
    private static int Minutes(int hhmm) => hhmm / 100 * 60 + hhmm % 100;
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private void Record(string evidence) { this.Evidence.Add(evidence); this.Monitor.Log(evidence, LogLevel.Info); }

    private sealed record SmokerRecipe(string Rule, string Input, int InputCount, string Output, int OutputCount, int Quality, int Minutes);
    private sealed record ClockSample(int Clock, int ElapsedMinutes, string[] CollectedRules);
    private sealed record StackValue(string? Id, int Count, int Quality);
    private sealed record RunningBatch(SObject Device, SmokerRecipe Recipe)
    {
        internal bool Collected { get; set; }
    }
}
