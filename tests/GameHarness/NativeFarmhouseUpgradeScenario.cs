using System.Diagnostics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using SObject = StardewValley.Object;
using Tile = xTile.Dimensions.Location;

namespace GameHarness;

/// <summary>Uses ordinary shop stock, a native furnace and Robin's first house upgrade in one bound save copy.</summary>
internal sealed class NativeFarmhouseUpgradeScenario
{
    internal const string Scenario = "NativeFarmhouseUpgrade";
    internal const string FarmName = "XYSP1HomeUpgrade";
    internal const ulong FixtureId = 620261125;
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private const string Wood = "(O)388", Stone = "(O)390", Ore = "(O)378", Coal = "(O)382", Bar = "(O)334";
    private const string Id = XiangyeShiling.GameIntegration.MachineService.Prefix;
    private static readonly Vector2 Below = Vector2.UnitY;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<object> Evidence = new();
    private Options? Options;
    private bool PreviousPause, Loaded, Finished;
    private int LoadDelay = -1, OrderDay, MoneyBeforeOrder, SavingEvents, SavedEvents, CompletedNights, SmeltingTimeChanges, BedApproaches;
    private TimeSpan BedApproachedAt;
    private NativeBedtimeInteraction? Bedtime;
    private SObject? Furnace;
    private Stage Current;
    private string? Failure;
    private static Farmer Player => Game1.player;
    private enum Stage { Load, Blacksmith, CopperShop, Robin, MaterialShop, UpgradeOffer, UpgradeConfirm, ReturnHome, Smelt, Bedtime, Kitchen }

    internal NativeFarmhouseUpgradeScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario
            && config.FarmName == FarmName && config.SaveFixtureId == FixtureId, "Bind the upgrade test to its exact disposable save.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.Loaded = true;
    internal void OnReturnedToTitle() { if (this.Loaded && !this.Finished) this.Failure = "Returned to title before the farmhouse proof completed."; }

    internal void OnTimeChanged()
    {
        if (this.Current == Stage.Smelt && !this.Finished) this.SmeltingTimeChanges++;
    }

    internal void OnSaving()
    {
        if (this.Finished) return;
        if (this.Current != Stage.Bedtime || this.Bedtime?.AnswerSent != true || ++this.SavingEvents != this.CompletedNights + 1)
            this.Failure = "An unexpected save occurred outside the authorized native bedtime.";
    }

    internal void OnSaved()
    {
        if (this.Finished) return;
        if (this.Current != Stage.Bedtime || this.Bedtime?.AnswerSent != true || ++this.SavedEvents != this.SavingEvents)
            this.Failure = "Saved did not match the current authorized bedtime.";
    }

    internal void Tick()
    {
        if (this.Finished) { Game1.game1.Exit(); return; }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "The exact disposable upgrade save is absent or another world is already loaded.");
                this.Watch.Start();
                Game1.activeClickableMenu = null;
                SaveGame.Load(SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "Upgrade test failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(8), $"Timed out during {this.Current}.");
            if (!this.Loaded || !Context.IsWorldReady) return;
            Require(Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen && Constants.SaveFolderName == SaveName
                && Game1.uniqueIDForThisGame == FixtureId && Player.farmName.Value == FarmName && Player.Name == FarmName,
                "The loaded world or farmer no longer matches the dedicated save.");
            if (Game1.fadeToBlack || Game1.isWarping || Game1.newDay) return;
            if (Game1.eventUp)
            {
                if (Game1.CurrentEvent is { skippable: true } cutscene && Game1.activeClickableMenu is DialogueBox { isQuestion: false })
                {
                    this.Record($"Skipped original event {cutscene.id}.");
                    cutscene.skipEvent();
                }
                return;
            }

            switch (this.Current)
            {
                case Stage.Load:
                    if (!Context.IsPlayerFree) return;
                    Require(Player.currentLocation is FarmHouse fresh && fresh.upgradeLevel == 0
                        && Player.daysUntilHouseUpgrade.Value == -1 && FindAction(fresh, "kitchen") is null
                        && new[] { Wood, Stone, Ore, Coal, Bar }.All(item => Count(item) == 0)
                        && !Player.knowsRecipe(Recipe("BeanWorkshop")) && !Player.knowsRecipe(Recipe("FermentationCrock")),
                        "The seed must have no kitchen, house order, materials or device recipes.");
                    this.Options = Game1.options;
                    this.PreviousPause = this.Options.pauseWhenOutOfFocus;
                    this.Options.pauseWhenOutOfFocus = false;
                    Player.Money = 25000;
                    this.WarpToCounter("Blacksmith", "Blacksmith");
                    this.Current = Stage.Blacksmith;
                    break;
                case Stage.Blacksmith:
                    if (!AtShop("Blacksmith", "Clint", 1600)) return;
                    this.OpenCounter("Blacksmith");
                    this.Current = Stage.CopperShop;
                    break;
                case Stage.CopperShop:
                    if (Game1.activeClickableMenu is DialogueBox blacksmith) { Answer(blacksmith, "Blacksmith", "Shop"); return; }
                    if (Game1.activeClickableMenu is not ShopMenu copperShop) return;
                    Require(copperShop.ShopId == "Blacksmith", "Unexpected copper supplier.");
                    this.Buy(copperShop, Ore, 5);
                    this.Buy(copperShop, Coal, 1);
                    copperShop.exitThisMenu(playSound: false);
                    this.WarpToCounter("ScienceHouse", "Carpenter");
                    this.Current = Stage.Robin;
                    break;
                case Stage.Robin:
                    if (!AtShop("ScienceHouse", "Robin", 1700)) return;
                    this.OpenCounter("Carpenter");
                    this.Current = Stage.MaterialShop;
                    break;
                case Stage.MaterialShop:
                    if (Game1.activeClickableMenu is DialogueBox carpenter) { Answer(carpenter, "carpenter", "Shop"); return; }
                    if (Game1.activeClickableMenu is not ShopMenu materialShop) return;
                    Require(materialShop.ShopId == "Carpenter", "Unexpected building material supplier.");
                    this.Buy(materialShop, Wood, 530);
                    this.Buy(materialShop, Stone, 60);
                    this.Buy(materialShop, "(BC)" + Id + "_BeanWorkshop", 1, Recipe("BeanWorkshop"));
                    this.Buy(materialShop, "(BC)" + Id + "_FermentationCrock", 1, Recipe("FermentationCrock"));
                    materialShop.exitThisMenu(playSound: false);
                    this.MoneyBeforeOrder = Player.Money;
                    this.OrderDay = Game1.Date.TotalDays;
                    this.Current = Stage.UpgradeOffer;
                    break;
                case Stage.UpgradeOffer:
                    if (!AtShop("ScienceHouse", "Robin", 1700)) return;
                    this.OpenCounter("Carpenter");
                    this.Current = Stage.UpgradeConfirm;
                    break;
                case Stage.UpgradeConfirm:
                    if (Player.daysUntilHouseUpgrade.Value == 3)
                    {
                        Require(Player.HouseUpgradeLevel == 0 && Count(Wood) == 80 && Count(Stone) == 60
                            && Player.Money == this.MoneyBeforeOrder - 10000, "Robin charged or queued the wrong first upgrade.");
                        this.Record("Native Robin accepted 10000g and 450 wood for the first upgrade.");
                        this.Current = Stage.ReturnHome;
                        return;
                    }
                    if (Game1.activeClickableMenu is DialogueBox upgrade && upgrade.isQuestion)
                    {
                        if (Player.currentLocation.lastQuestionKey == "carpenter") Answer(upgrade, "carpenter", "Upgrade");
                        else Answer(upgrade, "upgrade", "Yes");
                    }
                    break;
                case Stage.ReturnHome:
                    if (Game1.activeClickableMenu is DialogueBox { isQuestion: false } response) { response.receiveLeftClick(0, 0); return; }
                    if (!Context.IsPlayerFree) return;
                    Point entry = Utility.getHomeOfFarmer(Player).getEntryLocation();
                    Game1.warpFarmer("FarmHouse", entry.X, entry.Y, 0);
                    this.Current = Stage.Smelt;
                    break;
                case Stage.Smelt:
                    this.AdvanceSmelting();
                    break;
                case Stage.Bedtime:
                    this.AdvanceConstruction();
                    break;
                case Stage.Kitchen:
                    if (Game1.activeClickableMenu is not CraftingPage { cooking: true } kitchen) return;
                    kitchen.exitThisMenu(playSound: false);
                    this.Record("The newly upgraded farmhouse's actual stove opened the native cooking menu.");
                    this.Finish(null);
                    break;
            }
        }
        catch (Exception error) { this.Finish(error.ToString()); }
    }

    private void AdvanceSmelting()
    {
        if (!Context.IsPlayerFree || Player.currentLocation is not FarmHouse house) return;
        if (this.Furnace is null)
        {
            // Only the station is prepared. Purchased fuel and ore enter and leave through native machine actions.
            this.Furnace = PlaceFurnace(house);
            Player.CurrentToolIndex = Player.Items.ToList().FindIndex(item => item?.QualifiedItemId == Ore);
            Require(Player.CurrentToolIndex is >= 0 and < 12 && Player.ActiveObject is { QualifiedItemId: Ore } selected
                && this.Furnace.performObjectDropInAction(selected, probe: false, Player, returnFalseIfItemConsumed: false)
                && Count(Ore) == 0 && Count(Coal) == 0 && Count(Bar) == 0 && this.Furnace.minutesUntilReady.Value > 0,
                "The native furnace did not consume five ore and one coal for a pending copper bar.");
            this.SmeltingTimeChanges = 0;
            this.Record("Started the original furnace with five purchased copper ore and one coal.");
            return;
        }
        if (!this.Furnace.readyForHarvest.Value) return;
        Require(this.SmeltingTimeChanges > 0 && this.Furnace.checkForAction(Player) && Count(Bar) == 1
            && this.Furnace.heldObject.Value is null, "The original furnace did not yield exactly one copper bar after natural clock advancement.");
        this.Record("Collected one ordinary copper bar from the original furnace.");
        this.Current = Stage.Bedtime;
    }

    private void AdvanceConstruction()
    {
        if (this.Bedtime is null)
        {
            // A sleep tile entered during the native wake-up transition can be recorded before Sleep is eligible.
            // For later nights, wait until the original clock has actually advanced from the 06:00 wake-up.
            if ((this.CompletedNights == 0 || Game1.timeOfDay >= 610) && Context.IsPlayerFree && Game1.shouldTimePass())
                this.ApproachBed();
            return;
        }
        if (this.SavedEvents == this.CompletedNights)
        {
            this.Bedtime.Tick(this.Record);
            if (!this.Bedtime.AnswerSent && Game1.activeClickableMenu is null && this.Watch.Elapsed - this.BedApproachedAt >= TimeSpan.FromSeconds(10))
            {
                Require(this.BedApproaches == 1, $"No native Sleep prompt after two approaches (day {Game1.Date.TotalDays}, "
                    + $"time {Game1.timeOfDay}, tile {Player.TilePoint}, can move {Player.CanMove}, has moved {Player.hasMoved}, "
                    + $"clock active {Game1.shouldTimePass()}).");
                if (Context.IsPlayerFree && Game1.shouldTimePass()) this.ApproachBed();
            }
            return;
        }
        if (!Context.IsPlayerFree) return;
        this.CompletedNights++;
        this.Bedtime = null;
        this.BedApproaches = 0;
        bool upgraded = this.CompletedNights == 3;
        Require(Game1.Date.TotalDays == this.OrderDay + this.CompletedNights && Player.HouseUpgradeLevel == (upgraded ? 1 : 0)
            && Player.daysUntilHouseUpgrade.Value == (upgraded ? -1 : 3 - this.CompletedNights) && this.SavingEvents == this.CompletedNights
            && this.SavedEvents == this.CompletedNights, "Native construction countdown differed after a real night.");
        this.Record($"Native bedtime {this.CompletedNights}: upgrade level {Player.HouseUpgradeLevel}, days left {Player.daysUntilHouseUpgrade.Value}.");
        if (!upgraded) return;

        Require(Count(Wood) == 80 && Count(Stone) == 60 && Count(Bar) == 1, "The acquired materials changed during construction.");
        SObject bean = NativeRouteActions.Craft("BeanWorkshop");
        SObject crock = NativeRouteActions.Craft("FermentationCrock");
        Require(Count(Wood) == 0 && Count(Stone) == 0 && Count(Bar) == 0 && Count(bean.QualifiedItemId) == 1
            && Count(crock.QualifiedItemId) == 1, "Native crafting did not use all and only the acquired materials.");
        this.Record("Crafted the two purchased blueprints with the remaining bought wood/stone and the smelted bar.");
        this.OpenCounter("kitchen");
        this.Current = Stage.Kitchen;
    }

    private void ApproachBed()
    {
        this.Bedtime = new NativeBedtimeInteraction();
        this.BedApproachedAt = this.Watch.Elapsed;
        this.BedApproaches++;
        this.Record($"Approached the original bed at {Player.TilePoint}, night {this.CompletedNights + 1}, attempt {this.BedApproaches}.");
    }

    private static SObject PlaceFurnace(FarmHouse house)
    {
        SObject carried = ItemRegistry.Create<SObject>("(BC)13");
        Require(Player.addItemToInventory(carried) is null, "The prepared furnace does not fit the backpack.");
        int slot = Player.Items.ToList().FindIndex(item => ReferenceEquals(item, carried));
        Require(slot is >= 0 and < 12, "The prepared furnace must be in a usable toolbar slot.");
        Player.CurrentToolIndex = slot;
        var map = house.Map.Layers[0];
        for (int y = 1; y + 2 < map.LayerHeight; y++)
        for (int x = 1; x + 1 < map.LayerWidth; x++)
        {
            Vector2 tile = new(x, y);
            if (house.isTileOnWall(x, y) || house.isTileOnWall(x, y + 1) || !house.CanItemBePlacedHere(tile)
                || !house.CanItemBePlacedHere(tile + Below, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
                || house.doesTileHaveProperty(x, y + 1, "TouchAction", "Back") is not null) continue;
            Player.setTileLocation(tile + Below);
            Player.faceDirection(0);
            if (!Utility.playerCanPlaceItemHere(house, carried, x * 64 + 32, y * 64 + 32, Player)) continue;
            Require(Utility.tryToPlaceItem(house, carried, x * 64 + 32, y * 64 + 32) && Count(carried.QualifiedItemId) == 0,
                "The prepared native furnace could not be placed.");
            return house.objects[tile];
        }
        throw new InvalidOperationException("No usable native furnace tile in the starting house.");
    }

    private static void Answer(DialogueBox prompt, string question, string response)
    {
        Require(prompt.isQuestion && Player.currentLocation.lastQuestionKey == question, "Unexpected native shop question.");
        if (prompt.transitioning || prompt.safetyTimer > 0 || prompt.characterIndexInDialogue < prompt.getCurrentString().Length) return;
        int choice = Array.FindIndex(prompt.responses, candidate => candidate.responseKey == response);
        Require(choice >= 0, $"Native question {question} has no {response} response.");
        prompt.selectedResponse = choice;
        prompt.receiveLeftClick(0, 0);
    }

    private void Buy(ShopMenu shop, string id, int quantity, string? recipe = null)
    {
        var stock = shop.forSale.Single(item => item.QualifiedItemId == id && item.IsRecipe == (recipe is not null));
        int price = shop.itemPriceAndStock[stock].Price;
        int funds = Player.Money, before = Count(id);
        Require(price > 0 && funds >= price * quantity && shop.heldItem is null, $"Native stock is not purchasable: {id}.");
        AccessTools.Method(typeof(ShopMenu), "tryToPurchaseItem").Invoke(shop, new object?[] { stock, null, quantity, 0, 0 });
        if (recipe is null)
        {
            Require(shop.heldItem is Item held && held.QualifiedItemId == id && held.Stack == quantity
                && Player.addItemToInventory(held) is null, $"Native purchase did not deliver {id} × {quantity}.");
            shop.heldItem = null;
        }
        Require(Player.Money == funds - price * quantity && Count(id) == before + (recipe is null ? quantity : 0)
            && (recipe is null || Player.knowsRecipe(recipe)), $"Native purchase did not preserve price, quantity or recipe: {id}.");
        this.Record($"{shop.ShopId}: bought {id} × {quantity} at {price}g each.");
    }

    private static bool AtShop(string location, string owner, int closes)
    {
        if (!Context.IsPlayerFree || Player.currentLocation.Name != location || Game1.timeOfDay < 900) return false;
        Require(Game1.timeOfDay < closes, $"The ordinary {owner} business window elapsed.");
        if (Game1.getCharacterFromName(owner) is not { } npc || npc.currentLocation != Player.currentLocation) return false;
        var counter = FindAction(Player.currentLocation, owner == "Robin" ? "Carpenter" : "Blacksmith")
            ?? throw new InvalidOperationException($"The native {owner} counter is missing.");
        return Vector2.Distance(npc.Tile, new Vector2(counter.Tile.X, counter.Tile.Y)) <= 3;
    }

    private void WarpToCounter(string location, string action)
    {
        var map = Game1.getLocationFromName(location) ?? throw new InvalidOperationException($"Missing native {location} map.");
        this.Record($"Fixture used native warp to reach {location}.");
        var target = FindAction(map, action) ?? throw new InvalidOperationException($"Missing {action} on {location}.");
        Game1.warpFarmer(location, target.Tile.X, target.Tile.Y + 1, 0);
    }

    private void OpenCounter(string action)
    {
        GameLocation map = Player.currentLocation;
        var target = FindAction(map, action) ?? throw new InvalidOperationException($"Missing {action} on {map.Name}.");
        Player.setTileLocation(new Vector2(target.Tile.X, target.Tile.Y) + Below);
        Player.faceDirection(0);
        Require(map.performAction(target.Action, Player, target.Tile), $"Native map action {target.Action} failed in {map.Name}.");
        this.Record($"Opened {target.Action} from the actual {map.Name} map tile.");
    }

    private static (Tile Tile, string Action)? FindAction(GameLocation map, string token)
    {
        var bounds = map.Map.Layers[0];
        for (int y = 0; y + 1 < bounds.LayerHeight; y++)
        for (int x = 0; x < bounds.LayerWidth; x++)
        {
            string? action = map.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (action?.Split(' ', 2)[0].Equals(token, StringComparison.OrdinalIgnoreCase) == true) return (new Tile(x, y), action);
        }
        return null;
    }

    private static string Recipe(string key) => Id + "_Recipe_" + key;
    private static int Count(string id) => Player.Items.CountId(id);

    private void Record(string step)
    {
        this.Evidence.Add(new { Step = step, Day = Game1.Date.TotalDays, Time = Game1.timeOfDay });
        this.Monitor.Log(step, LogLevel.Info);
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        if (this.Options is not null) this.Options.pauseWhenOutOfFocus = this.PreviousPause;
        if (failure is not null) this.Monitor.Log(failure, LogLevel.Error);
        this.Helper.Data.WriteJsonFile("native-farmhouse-upgrade-result.json", new
        {
            SchemaVersion = 1, ProcessId = Environment.ProcessId, FixtureId, Completed = failure is null, Failure = failure,
            Stage = this.Current.ToString(), this.Evidence, this.SmeltingTimeChanges, this.SavingEvents, this.SavedEvents, this.CompletedNights,
            Preparation = "The fixture supplied funds and a furnace, and set travel and interaction positions. Native shops supplied all wood, stone, ore, coal and both blueprints."
        });
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
