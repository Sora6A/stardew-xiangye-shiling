using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Runs two native bedtimes and two distinct process reloads on one exact disposable farm.</summary>
internal sealed class P3SeasonPersistenceScenario
{
    internal const string Scenario = "P3SeasonPersistence", FarmName = "XYSP3Season", LegacyFarm = "XYSP3Legacy";
    internal const ulong FixtureId = 620261132, LegacyId = 620261133;
    private const string Id = MachineService.Prefix + "_";
    private const string Crock = "(BC)" + Id + "FermentationCrock", Recipe = Id + "Recipe_SichuanDouban";
    private const string BroadBeans = "(O)" + Id + "BroadBeans", Peppers = "(O)260", Product = "(O)" + Id + "SichuanDouban";
    private const string SoySeed = "(O)" + Id + "SoybeanSeeds", NapaSeed = "(O)" + Id + "NapaCabbageSeeds", WheatSeed = "(O)483";
    private const int FarmYear = 1;
    private static readonly Vector2 Below = Vector2.UnitY;

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly string Phase, Name, SaveName;
    private readonly ulong WorldId;
    private readonly Stopwatch Watch = new();
    private readonly List<string> Steps = new();
    private readonly List<Plot> Plots = new();
    private MachineService Service = null!;
    private SObject? Device;
    private NativeBedtimeInteraction? Bedtime;
    private Checkpoint? BeforeSleep, AfterLoad;
    private LegacyDevice[]? LegacyDevices;
    private Options? Options;
    private TimeSpan BedApproachedAt, SummerPreparedAt;
    private int LoadDelay = -1, LoadCount, SavingEvents, SavedEvents, BedApproaches;
    private int? PreviousProcessId;
    private bool Finished, FarmRequested, CropsPrepared, HomeRequested, SkippedArrival, PreviousPause;
    private GameCheckpoint? LastGame;
    private string? Failure;

    internal P3SeasonPersistenceScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        this.Phase = config.NativeRecoveryPhase;
        bool legacy = this.Phase == "Legacy";
        this.Name = legacy ? LegacyFarm : FarmName;
        this.WorldId = legacy ? LegacyId : FixtureId;
        this.SaveName = $"{this.Name}_{this.WorldId}";
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario
            && this.Phase is "Write" or "Verify" or "VerifyCollected" or "Legacy"
            && config.FarmName == this.Name && config.SaveFixtureId == this.WorldId, "Bind seasonal persistence to its exact disposable farm and phase.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.LoadCount++;
    internal void OnReturnedToTitle() { if (this.LoadCount > 0 && !this.Finished) this.Failure = "Returned to title before seasonal verification completed."; }

    internal void OnSaving()
    {
        if (this.Finished) return;
        if (++this.SavingEvents != 1 || this.Bedtime?.AnswerSent != true || this.Phase is not ("Write" or "Verify"))
            this.Failure = "Native Saving did not follow the one authorized sleep answer.";
    }

    internal void OnSaved()
    {
        if (this.Finished) return;
        if (++this.SavedEvents != 1 || this.SavingEvents != 1)
            this.Failure = "Native Saved did not follow the one authorized Saving event.";
    }

    internal void Tick()
    {
        if (this.Finished)
        {
            if (!Context.IsWorldReady || this.IsBoundWorld(Game1.player)) Game1.game1.Exit();
            return;
        }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, this.SaveName, this.SaveName)),
                    "The exact seasonal save is absent or another world is already loaded.");
                this.Watch.Start();
                Game1.activeClickableMenu = null;
                SaveGame.Load(this.SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "Seasonal persistence failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(3), $"Timed out during {this.Phase} after {this.Steps.LastOrDefault()}.");
            if (this.LoadCount == 0 || !Context.IsWorldReady) return;
            Farmer player = Game1.player;
            Require(this.LoadCount == 1 && this.IsBoundWorld(player), "The loaded world or farmer differs from the bound fixture.");
            if (this.Options is null)
            {
                this.Options = Game1.options;
                this.PreviousPause = this.Options.pauseWhenOutOfFocus;
                this.Options.pauseWhenOutOfFocus = false;
            }
            this.LastGame = new(Game1.currentLocation.NameOrUniqueName, player.TilePoint.X, player.TilePoint.Y,
                Context.IsPlayerFree, Game1.isWarping, Game1.fadeToBlack, Game1.shouldTimePass(), Game1.timeOfDay,
                Game1.CurrentEvent?.id, Game1.activeClickableMenu?.GetType().Name, Game1.currentLocation.lastQuestionKey,
                this.Bedtime is not null, this.Bedtime?.AnswerSent == true, this.BedApproaches);
            if (this.TrySkipKnownArrival()) return;
            Require(this.Phase != "Write" || this.SummerPreparedAt == default || this.Bedtime?.AnswerSent == true
                || this.Watch.Elapsed - this.SummerPreparedAt < TimeSpan.FromSeconds(35),
                "The native sleep question did not complete promptly enough before the late Summer night.");
            if (this.Bedtime is { } sleep)
            {
                if (this.SavedEvents == 0)
                {
                    sleep.Tick(this.Record);
                    // A return warp can leave a touch tile recorded during a fade. Reapproach only if
                    // the real game is free and never produced a question or received our answer.
                    if (!sleep.AnswerSent && Game1.activeClickableMenu is null && Context.IsPlayerFree
                        && Game1.shouldTimePass() && this.Watch.Elapsed - this.BedApproachedAt >= TimeSpan.FromSeconds(8))
                    {
                        Require(this.BedApproaches == 1, "The native bed never opened a sleep question after two approaches.");
                        this.ApproachBed();
                    }
                }
                else if (Context.IsPlayerFree && !Game1.fadeToBlack && !Game1.newDay) this.VerifyMorning(player, sleep);
                return;
            }
            if (Game1.fadeToBlack || Game1.isWarping || !Context.IsPlayerFree) return;
            this.Service ??= this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
                ?? throw new InvalidOperationException("The production machine API is unavailable.");
            if (this.Phase == "Legacy") { this.VerifyLegacy(player); return; }
            if (this.Phase == "VerifyCollected") { this.VerifyNonRepeat(player); return; }

            if (!this.FarmRequested)
            {
                this.VerifyEffectiveContent();
                if (this.Phase == "Write") this.BeginSummer(player);
                else this.LoadReadyAndCollect(player);
                this.EnterFarm();
                return;
            }
            if (!this.CropsPrepared)
            {
                Require(ReferenceEquals(Game1.currentLocation, Game1.getFarm()), "The test farmer did not arrive at the original farm.");
                this.PlantAndWater(player);
                this.CropsPrepared = true;
                return;
            }
            if (Game1.currentLocation is not FarmHouse)
            {
                if (!this.HomeRequested)
                {
                    var house = Utility.getHomeOfFarmer(player);
                    Point entry = house.getEntryLocation();
                    this.HomeRequested = true;
                    Game1.warpFarmer(house.NameOrUniqueName, entry.X, entry.Y, 0, house.isStructure.Value);
                }
                return;
            }
            if (this.Phase == "Verify" && Game1.timeOfDay < 610) return;
            this.BeforeSleep = this.Capture(player);
            Require(this.BeforeSleep.Machine.Phase == (this.Phase == "Write" ? "Processing" : "Idle")
                && this.BeforeSleep.Crops.All(crop => crop.Water == HoeDirt.watered), "Before bed the expected phase or watered crops differ.");
            if (Game1.shouldTimePass()) this.ApproachBed();
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.ToString()); }
    }

    private void BeginSummer(Farmer player)
    {
        Require(Game1.currentLocation is FarmHouse && Game1.year == FarmYear && Game1.season == Season.Spring && Game1.dayOfMonth == 1,
            "Use the untouched first-morning recipe seed.");
        var home = (FarmHouse)Game1.currentLocation;
        Require(!home.objects.Values.Any(item => MachineService.TryGetKind(item, out _)), "The seed already has a custom machine.");
        Game1.season = Season.Summer;
        Game1.dayOfMonth = 28;
        Game1.timeOfDay = 2300;
        Game1.stats.DaysPlayed = (uint)(Game1.Date.TotalDays + 1);
        Game1.netWorldState.Value.UpdateFromGame1();
        this.SummerPreparedAt = this.Watch.Elapsed;
        this.VerifyNativeWheatStock();

        MachineRecipe rule = this.Service.Recipes.Single(recipe => recipe.Id == Recipe);
        Require(rule.Machine == MachineKind.FermentationCrock && rule.Minutes is > 60 and <= 400
            && rule.Inputs.Select(input => input.ItemId).SequenceEqual(new[] { BroadBeans, Peppers })
            && rule.Inputs.All(input => input.Quantity >= 2) && rule.Output.ItemId == Product && rule.Output.Quantity > 0,
            "The later-page multi-input rule cannot complete across the selected native bedtime.");
        int beans = rule.Inputs[0].Quantity, peppers = rule.Inputs[1].Quantity;
        var stock = new Item[player.MaxItems];
        stock[0] = ItemRegistry.Create(Crock);
        stock[1] = ItemRegistry.Create(BroadBeans, 1);
        stock[2] = ItemRegistry.Create(BroadBeans, beans, 2);
        stock[3] = ItemRegistry.Create(Peppers, 1);
        if (peppers > 2) stock[4] = ItemRegistry.Create(Peppers, peppers - 2, 2);
        stock[5] = ItemRegistry.Create(SoySeed, 1);
        stock[6] = ItemRegistry.Create(WheatSeed, 1);
        stock[7] = ItemRegistry.Create(NapaSeed, 1);
        player.Items.OverwriteWith(stock);
        this.Device = PlaceDevice(home, player);

        var menu = OpenMenu(player, this.Device);
        Require(!menu.allClickableComponents.Any(component => component.name == Recipe), "The later recipe appeared on the first page.");
        Click(menu, "Next");
        Require(menu.allClickableComponents.Any(component => component.name == Id + "Recipe_SourCabbage"),
            "The real page control did not expose the last crock page.");
        var before = Inventory(player);
        string empty = Raw(this.Device);
        Click(menu, Recipe);
        Require(Raw(this.Device) == empty && Inventory(player).SequenceEqual(before),
            "An insufficient multi-input start changed the machine or exact inventory slots.");
        player.Items[4] = ItemRegistry.Create(Peppers, peppers - 1, 2);
        Click(menu, Recipe);
        var state = State(this.Device);
        Batch batch = state.Batch ?? throw new InvalidOperationException("The later-page menu did not start the batch.");
        Require(state.Revision == 1 && batch.RecipeId == Recipe && batch.Output == rule.Output && batch.ReadyAt - batch.StartedAt == rule.Minutes
            && state.PhaseAt(MachineService.Now()) == MachinePhase.Processing
            && player.Items[1] is null && player.Items[2] is { Stack: 1, Quality: 2 } && player.Items[3] is null && player.Items[4] is null
            && batch.Inputs.Select(input => (input.ItemId, input.Quality, input.Quantity)).SequenceEqual(new[]
            {
                (BroadBeans, 0, 1), (BroadBeans, 2, beans - 1), (Peppers, 0, 1), (Peppers, 2, peppers - 1)
            }) && player.Items.CountId(Product) == 0, "The frozen batch, cross-quality debits or output differ.");
        Close(menu);
        this.Record("Native placement and last-page start committed both ingredients from two qualities after an atomic insufficient-input rejection.");
    }

    private void LoadReadyAndCollect(Farmer player)
    {
        Checkpoint saved = this.ReadPrevious("Write");
        this.BindCheckpoint(player, saved, "Ready", 1);
        this.VerifyNativeWheatStock();
        var machine = this.Device!;
        var state = State(machine);
        var batch = state.Batch!;
        var menu = OpenMenu(player, machine);
        Require(menu.allClickableComponents.Count(component => component.name == "Collect") == 1
            && menu.allClickableComponents.All(component => component.name != "Next"), "Cold loaded ready batch did not open its collect panel.");
        int before = player.Items.CountId(Product);
        Click(menu, "Collect");
        var idle = State(machine);
        Require(idle.InstanceId == state.InstanceId && idle.Batch is null && idle.Revision == state.Revision + 1
            && idle.LastActorId == player.UniqueMultiplayerID && idle.LastRequestId.HasValue
            && player.Items.CountId(Product) == before + batch.Output.Quantity
            && player.Items.Any(item => item is not null && item.QualifiedItemId == Product && item.Quality == batch.Output.Quality),
            "The original collected batch did not yield its exact frozen product.");
        Close(menu);
        this.Record("Separate cold boot recovered the exact ready batch and delivered its frozen output through the original menu.");
    }

    private void VerifyNonRepeat(Farmer player)
    {
        this.VerifyEffectiveContent();
        Checkpoint saved = this.ReadPrevious("Verify");
        this.BindCheckpoint(player, saved, "Idle", 2);
        var machine = this.Device!;
        var cleared = State(machine);
        Require(cleared.LastRequestId.HasValue && player.Items.CountId(Product) > 0 && this.Plots.Count == 3,
            "The second cold boot lacks the original collection receipt, product or three plots.");
        player.setTileLocation(machine.TileLocation + Below);
        var replay = new Request(cleared.LastRequestId!.Value, cleared.InstanceId, cleared.Revision - 1, player.UniqueMultiplayerID);
        var empty = new Request(Guid.NewGuid(), cleared.InstanceId, cleared.Revision, player.UniqueMultiplayerID);
        OperationResult duplicate = this.Service.Execute(machine, player, replay, null);
        OperationResult fresh = this.Service.Execute(machine, player, empty, null);
        Require(!duplicate.Applied && !fresh.Applied && duplicate.Message != fresh.Message, "The old receipt or fresh empty collection was accepted.");
        this.AfterLoad = this.Capture(player);
        Require(Same(saved, this.AfterLoad), "The cold replay checks changed exact inventory, crops or machine state.");
        this.Record("Second cold boot preserved the receipt and all crop phases. Replay and fresh empty collection changed nothing.");
        this.Finish(null);
    }

    private void VerifyLegacy(Farmer player)
    {
        this.VerifyEffectiveContent();
        var house = player.currentLocation as FarmHouse ?? throw new InvalidOperationException("Old P1 source did not boot in its original farmhouse.");
        Require(Game1.year == 4 && Game1.season == Season.Spring && Game1.dayOfMonth == 5
            && player.Items.CountId("(O)" + Id + "Tofu") == 1 && player.Items.CountId("(O)" + Id + "SoySauce") == 4
            && player.craftingRecipes.ContainsKey(Id + "Recipe_BeanWorkshop") && player.craftingRecipes.ContainsKey(Id + "Recipe_FermentationCrock")
            && player.cookingRecipes.ContainsKey(Id + "Recipe_ToonTofu"), "The old P1 route inventory, recipes or date changed.");
        this.LegacyDevices = new[] { new Vector2(18, 14), new Vector2(19, 14) }.Select(tile =>
        {
            SObject machine = house.objects[tile];
            MachineState state = State(machine);
            Require(state.Revision == 2 && state.Batch is null && state.LastRequestId.HasValue && state.LastActorId == player.UniqueMultiplayerID,
                "An existing P1 machine did not retain a readable native collection receipt.");
            return new LegacyDevice((int)tile.X, (int)tile.Y, state.Kind.ToString(), state.InstanceId, Raw(machine));
        }).ToArray();
        Require(this.LegacyDevices.Select(device => device.Kind).SequenceEqual(new[] { "BeanWorkshop", "FermentationCrock" }), "The old devices moved or changed kind.");
        this.Record("Loaded the independent old P1 route copy with two original machines, two products and three learned recipes.");
        this.Finish(null);
    }

    private void PlantAndWater(Farmer player)
    {
        Farm farm = Game1.getFarm();
        if (this.Phase == "Write")
        {
            this.Plots.Add(Plant(farm, player, 5, SoySeed));
            this.Plots.Add(Plant(farm, player, 6, WheatSeed));
            var (tile, dirt) = Till(farm);
            Require(!UseHeld(farm, player, tile, 7) && dirt.crop is null && player.Items.CountId(NapaSeed) == 1,
                "The native path accepted Fall-only napa in Summer or consumed its seed.");
            this.Record("Native planting accepted soybean and vanilla wheat on Summer 28 and refused Fall-only napa without debit.");
        }
        else
        {
            this.Plots.Add(Plant(farm, player, 7, NapaSeed));
            this.Record("Native Fall 1 planting accepted the same napa seed rejected on Summer 28.");
        }

        var wateringCan = new WateringCan();
        foreach (var plot in this.Plots)
        {
            Vector2 tile = new(plot.X, plot.Y);
            HoeDirt dirt = farm.GetHoeDirtAtTile(tile);
            _ = dirt.performToolAction(wateringCan, 0, tile);
            Require(dirt.state.Value == HoeDirt.watered, $"The original watering path did not water {plot.Seed}.");
        }
    }

    private static Plot Plant(Farm farm, Farmer player, int slot, string seed)
    {
        var (tile, dirt) = Till(farm);
        Require(UseHeld(farm, player, tile, slot) && dirt.crop is { } && player.Items.CountId(seed) == 0,
            $"Native planting did not consume and plant {seed}.");
        return new((int)tile.X, (int)tile.Y, seed);
    }

    private static (Vector2 Tile, HoeDirt Dirt) Till(Farm farm)
    {
        var bounds = farm.Map.Layers[0];
        for (int y = 4; y + 2 < bounds.LayerHeight; y++)
        for (int x = 4; x + 2 < bounds.LayerWidth; x++)
        {
            Vector2 tile = new(x, y);
            if (farm.doesTileHaveProperty(x, y, "Diggable", "Back") is not null && !farm.terrainFeatures.ContainsKey(tile)
                && Free(farm, tile) && Free(farm, tile + Below) && farm.makeHoeDirt(tile, false)
                && farm.GetHoeDirtAtTile(tile) is { crop: null } dirt) return (tile, dirt);
        }
        throw new InvalidOperationException("No open pair of native farm tiles for tilling and approach.");
    }

    private static bool UseHeld(GameLocation location, Farmer player, Vector2 tile, int slot)
    {
        player.CurrentToolIndex = slot;
        player.setTileLocation(tile + Below);
        player.faceDirection(0);
        var item = player.Items[slot] as SObject ?? throw new InvalidOperationException("The fixture lacks the expected held object.");
        return Utility.tryToPlaceItem(location, item, (int)tile.X * 64 + 32, (int)tile.Y * 64 + 32);
    }

    private static SObject PlaceDevice(FarmHouse house, Farmer player)
    {
        player.CurrentToolIndex = 0;
        Vector2 origin = player.Tile;
        for (int dy = -4; dy <= 4; dy++)
        for (int dx = -4; dx <= 4; dx++)
        {
            Vector2 tile = origin + new Vector2(dx, dy);
            if (tile == origin || !Free(house, tile) || !Free(house, tile + Below)) continue;
            player.setTileLocation(tile + Below);
            var held = player.Items[0] as SObject ?? throw new InvalidOperationException("The crock is not held in its prepared slot.");
            if (!Utility.playerCanPlaceItemHere(house, held, (int)tile.X * 64 + 32, (int)tile.Y * 64 + 32, player)) continue;
            if (UseHeld(house, player, tile, 0) && house.objects.TryGetValue(tile, out SObject placed)
                && placed.QualifiedItemId == Crock && player.Items[0] is null && State(placed).Revision == 0) return placed;
            throw new InvalidOperationException("The native placement did not debit and initialize the exact crock.");
        }
        throw new InvalidOperationException("No eligible farmhouse tile for the native crock placement.");
    }

    private static bool Free(GameLocation location, Vector2 tile) => location.isTileOnMap(tile) && !location.objects.ContainsKey(tile)
        && location.CanItemBePlacedHere(tile, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
        && location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "TouchAction", "Back") is null;

    private void EnterFarm()
    {
        Point door = Game1.getFarm().GetMainFarmHouse().getPointForHumanDoor();
        this.FarmRequested = true;
        Game1.warpFarmer("Farm", door.X, door.Y + 1, 2);
    }

    private void ApproachBed()
    {
        this.Bedtime = new NativeBedtimeInteraction();
        this.BedApproachedAt = this.Watch.Elapsed;
        this.Record($"Approached original bed ({++this.BedApproaches}).");
    }

    private bool TrySkipKnownArrival()
    {
        if (!this.FarmRequested || this.SkippedArrival || this.Bedtime is not null || !Game1.eventUp || Game1.isQuestion
            || Game1.activeClickableMenu is not DialogueBox { isQuestion: false }
            || Game1.CurrentEvent is not { id: "1590166", skippable: true } arrival) return false;
        this.SkippedArrival = true;
        this.HomeRequested = false;
        this.Record("Skipped the known original farm arrival cutscene 1590166 before bedtime.");
        arrival.skipEvent();
        return true;
    }

    private void VerifyMorning(Farmer player, NativeBedtimeInteraction sleep)
    {
        var before = this.BeforeSleep!;
        this.AfterLoad = this.Capture(player);
        var next = this.AfterLoad;
        Require(Game1.year == FarmYear && Game1.season == Season.Fall && Game1.dayOfMonth == (this.Phase == "Write" ? 1 : 2)
            && Game1.timeOfDay == 600 && next.TotalDays == sleep.StartTotalDays + 1
            && next.Machine.Raw == before.Machine.Raw && next.Machine.Instance == before.Machine.Instance
            && next.Machine.Phase == (this.Phase == "Write" ? "Ready" : "Idle")
            && next.Inventory.SequenceEqual(before.Inventory) && next.Crops.Length == before.Crops.Length,
            "The native saved morning did not retain the exact machine and inventory across one night.");
        foreach (var (old, grown) in before.Crops.Zip(next.Crops))
        {
            Require(old.Plot == grown.Plot && old.CropSeed == grown.CropSeed && old.Harvest == grown.Harvest
                && !grown.Dead && (grown.Phase > old.Phase || (grown.Phase == old.Phase && grown.PhaseDay == old.PhaseDay + 1)),
                $"The native morning did not advance the living {old.Plot.Seed} crop once.");
        }
        this.Record($"Original Sleep, Saving and Saved advanced to Fall {Game1.dayOfMonth}; raw machine, exact stock and crop progression verified.");
        this.Finish(null);
    }

    private Checkpoint ReadPrevious(string phase)
    {
        var previous = this.Helper.Data.ReadJsonFile<Report>(ReportName(phase));
        Require(previous is { Completed: true, SavingEvents: 1, SavedEvents: 1, AfterLoad: not null }
            && previous.FixtureId == FixtureId && previous.Phase == phase && previous.ProcessId != Environment.ProcessId,
            $"A completed native {phase} report from a distinct process is required.");
        this.PreviousProcessId = previous!.ProcessId;
        return previous.AfterLoad!;
    }

    private void BindCheckpoint(Farmer player, Checkpoint saved, string phase, int day)
    {
        var house = player.currentLocation as FarmHouse ?? throw new InvalidOperationException("The cold boot did not open the farmhouse.");
        this.Device = house.objects[new Vector2(saved.Machine.X, saved.Machine.Y)];
        this.Plots.AddRange(saved.Crops.Select(crop => crop.Plot));
        var cold = this.Capture(player);
        Require(Game1.year == FarmYear && Game1.season == Season.Fall && Game1.dayOfMonth == day && Game1.timeOfDay == 600
            && cold.Machine.Phase == phase && Same(saved, cold), "Cold load changed the saved date, raw batch, stock or crops.");
        this.Record($"Cold boot reproduced the native Fall {day} save checkpoint exactly.");
    }

    private Checkpoint Capture(Farmer player)
    {
        var house = player.currentLocation as FarmHouse ?? throw new InvalidOperationException("Capture must occur at home.");
        var machine = this.Device ?? throw new InvalidOperationException("No exact test machine is bound.");
        Require(house.objects.TryGetValue(machine.TileLocation, out SObject placed) && ReferenceEquals(placed, machine)
            && MachineService.TryGetKind(machine, out var kind) && kind == MachineKind.FermentationCrock, "The placed original crock moved or was replaced.");
        var state = State(machine);
        var crops = this.Plots.Select(plot =>
        {
            HoeDirt dirt = Game1.getFarm().GetHoeDirtAtTile(new Vector2(plot.X, plot.Y));
            Crop crop = dirt?.crop ?? throw new InvalidOperationException($"Missing native saved crop {plot.Seed}.");
            return new RankedCrop(plot, crop.netSeedIndex.Value, crop.indexOfHarvest.Value, crop.currentPhase.Value,
                crop.dayOfCurrentPhase.Value, dirt.state.Value, crop.dead.Value);
        }).ToArray();
        return new(Game1.Date.TotalDays, player.UniqueMultiplayerID,
            new((int)machine.TileLocation.X, (int)machine.TileLocation.Y, state.InstanceId, Raw(machine), state.PhaseAt(MachineService.Now()).ToString()),
            Inventory(player), crops);
    }

    private void VerifyEffectiveContent()
    {
        var crops = DataLoader.Crops(Game1.content);
        var objects = DataLoader.Objects(Game1.content);
        Require(crops["472"].HarvestItemId == "24" && crops["490"].HarvestItemId == "276"
            && crops["483"].HarvestItemId == "262" && crops["483"].Seasons.SequenceEqual(new[] { Season.Summer, Season.Fall })
            && objects["24"].Name == "Parsnip" && objects["399"].Name == "Spring Onion"
            && crops[Id + "SoybeanSeeds"].Seasons.SequenceEqual(new[] { Season.Summer, Season.Fall })
            && crops[Id + "NapaCabbageSeeds"].Seasons.SequenceEqual(new[] { Season.Fall })
            && objects.ContainsKey(Id + "SichuanDouban") && this.Service.Recipes.Count == 10,
            "The full installed candidate did not retain the pinned vanilla rows and P2 content.");
    }

    private void VerifyNativeWheatStock()
    {
        Require(Utility.TryOpenShopMenu("SeedShop", "Pierre", playOpenSound: false) && Game1.activeClickableMenu is ShopMenu,
            "Could not open Pierre's original native seed stock.");
        var shop = (ShopMenu)Game1.activeClickableMenu!;
        Require(shop.ShopId == "SeedShop" && shop.forSale.Any(item => item.QualifiedItemId == WheatSeed && !item.IsRecipe),
            "Pierre's original seasonal wheat seed line is absent.");
        shop.exitThisMenu(playSound: false);
        this.Record("Pierre's vanilla wheat seed line is available in " + Game1.season + ".");
    }

    private static MachineMenu OpenMenu(Farmer player, SObject machine)
    {
        player.setTileLocation(machine.TileLocation + Below);
        player.faceDirection(0);
        return machine.checkForAction(player) && Game1.activeClickableMenu is MachineMenu menu
            ? menu : throw new InvalidOperationException("The original crock action did not open MachineMenu.");
    }

    private static void Click(MachineMenu menu, string name)
    {
        Point center = menu.allClickableComponents.Single(component => component.name == name).bounds.Center;
        menu.receiveLeftClick(center.X, center.Y);
    }

    private static void Close(MachineMenu menu)
    {
        Point center = menu.upperRightCloseButton.bounds.Center;
        menu.receiveLeftClick(center.X, center.Y);
        Require(Game1.activeClickableMenu is null, "The original close control did not exit MachineMenu.");
    }

    private bool IsBoundWorld(Farmer player) => Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen
        && Constants.SaveFolderName == this.SaveName && Game1.uniqueIDForThisGame == this.WorldId
        && player.Name == this.Name && player.farmName.Value == this.Name;

    private static string ReportName(string phase) => $"p3-season-{phase.ToLowerInvariant()}-result.json";
    private static string Raw(SObject machine) => machine.modData[MachineService.StateKey];
    private static MachineState State(SObject machine) => MachineService.TryRead(machine, out var value)
        ? value! : throw new InvalidOperationException("A native crock or old device lacks a valid production state.");
    private static Slot?[] Inventory(Farmer player) => player.Items.Select(item =>
        item is null ? null : new Slot(item.QualifiedItemId, item.Stack, item.Quality)).ToArray();
    private static bool Same(Checkpoint before, Checkpoint after) => before.TotalDays == after.TotalDays && before.Farmer == after.Farmer
        && before.Machine == after.Machine && before.Inventory.SequenceEqual(after.Inventory) && before.Crops.SequenceEqual(after.Crops);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private void Record(string step) { this.Steps.Add(step); this.Monitor.Log(step, LogLevel.Info); }

    private void Finish(string? failure)
    {
        this.Finished = true;
        this.Watch.Stop();
        if (this.Options is not null) this.Options.pauseWhenOutOfFocus = this.PreviousPause;
        this.Helper.Data.WriteJsonFile(ReportName(this.Phase), new
        {
            SchemaVersion = 1, Scenario, this.Phase, FixtureId = this.WorldId, ProcessId = Environment.ProcessId,
            Completed = failure is null, Failure = failure, this.LoadCount, this.SavingEvents, this.SavedEvents,
            this.PreviousProcessId, this.BeforeSleep, this.AfterLoad, this.LegacyDevices, this.Steps, this.LastGame,
            Scope = "Prepared disposable inventory and Summer 28 clock; native placement, menu, crop and bedtime paths; separate installed-package process for each phase."
        });
        if (failure is not null) this.Monitor.Log(failure, LogLevel.Error);
    }

    private sealed record Plot(int X, int Y, string Seed);
    private sealed record RankedCrop(Plot Plot, string CropSeed, string Harvest, int Phase, int PhaseDay, int Water, bool Dead);
    private sealed record Slot(string Item, int Stack, int Quality);
    private sealed record SavedMachine(int X, int Y, Guid Instance, string Raw, string Phase);
    private sealed record LegacyDevice(int X, int Y, string Kind, Guid Instance, string Raw);
    private sealed record GameCheckpoint(string Location, int X, int Y, bool PlayerFree, bool Warping, bool Fading,
        bool ClockRunning, int Time, string? Event, string? Menu, string? Question, bool BedStarted, bool AnswerSent, int BedApproaches);
    private sealed record Checkpoint(int TotalDays, long Farmer, SavedMachine Machine, Slot?[] Inventory, RankedCrop[] Crops);
    private sealed record Report(string Phase, ulong FixtureId, int ProcessId, bool Completed, int SavingEvents, int SavedEvents, Checkpoint? AfterLoad);
}
