using System.Diagnostics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Locations;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using SObject = StardewValley.Object;
using Tile = xTile.Dimensions.Location;

namespace GameHarness;

/// <summary>Buys the installed recipes from native Gus stock and cooks each in the original farmhouse kitchen.</summary>
internal sealed class P2KitchenScenario
{
    internal const string Scenario = "P2Kitchen", FarmName = "XYSP2Kitchen";
    internal const ulong FixtureId = 620261131;
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private const string Prefix = MachineService.Prefix + "_", Oil = "(O)247", EggCategory = "-5", Wood = "(O)388";
    // Frozen ingredient identities independently of loaded recipe counts. A non-white or large egg witnesses each category recipe.
    private static readonly Dish[] Dishes =
    {
        new("VegetarianChicken", new[] { O("TofuSheets"), O("SoySauce"), Oil }),
        new("FriedTofu", new[] { O("Tofu"), Oil }),
        new("OrchidTofu", new[] { O("DriedTofu"), O("SoySauce"), Oil }),
        new("StirFriedBamboo", new[] { O("BambooShoots"), Oil }),
        new("ScallionEggs", new[] { O("WildScallion"), EggCategory }, "(O)180"),
        new("ToonEggs", new[] { O("ChineseToon"), EggCategory }, "(O)182"),
        new("MugwortRiceCake", new[] { O("Mugwort"), "(O)423", "(O)245" }),
        new("ToonTofu", new[] { O("ChineseToon"), O("Tofu") }),
        new("BambooSmokedPork", new[] { O("BambooShoots"), O("SmokedPork") }),
        new("ScallionSmokedTofu", new[] { O("WildScallion"), O("SmokedTofu") }),
        new("SourCabbageTofu", new[] { O("SourCabbage"), O("Tofu") }),
        new("BraisedBamboo", new[] { O("BambooShoots"), O("SoySauce"), Oil }),
        new("SaucedTofu", new[] { O("DriedTofu"), O("SoybeanPaste") }),
        new("MapoTofu", new[] { O("Tofu"), O("SichuanDouban"), O("Pork") }),
        new("TofuPorkRoll", new[] { O("TofuSheets"), O("Pork"), O("SoySauce") }),
        new("YubaSoup", new[] { O("Yuba"), EggCategory }, "(O)174")
    };
    private static readonly Vector2[] Offsets = { Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY, Vector2.UnitX };

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<Purchase> Purchases = new();
    private readonly List<Cook> Cooked = new();
    private int LoadDelay = -1, LoadCount, SavingEvents, OriginalMoney, OriginalFacing, OriginalTool;
    private Vector2 OriginalTile;
    private Item[]? OriginalInventory;
    private TimeSpan StoveRequestedAt;
    private bool Finished;
    private string? Failure;
    private static Farmer Player => Game1.player;

    internal P2KitchenScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "Bind P2 cooking to its exact disposable farm.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.LoadCount++;
    internal void OnSaving() { this.SavingEvents++; this.Failure = "Unexpected native save during the P2 kitchen test."; }
    internal void OnReturnedToTitle() { if (this.LoadCount > 0 && !this.Finished) this.Failure = "Returned to title before the P2 kitchen test finished."; }

    internal void Tick()
    {
        if (this.Finished) { Game1.game1.Exit(); return; }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "Missing P2 kitchen fixture or another world is already loaded.");
                Game1.activeClickableMenu = null;
                this.Watch.Start();
                SaveGame.Load(SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "P2 kitchen test failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(3), "Timed out loading the dedicated farmhouse or its kitchen.");
            if (this.LoadCount == 0 || !Context.IsWorldReady) return;
            Require(this.LoadCount == 1 && MatchesWorld(), "The active world or farmer differs from the kitchen fixture.");
            if (this.OriginalInventory is null)
            {
                if (Context.IsPlayerFree && !Game1.eventUp && !Game1.isWarping && !Game1.fadeToBlack) this.Prepare();
                return;
            }
            if (Game1.activeClickableMenu is not CraftingPage { cooking: true } kitchen)
            {
                Require(Game1.activeClickableMenu is null && this.Watch.Elapsed - this.StoveRequestedAt < TimeSpan.FromSeconds(10),
                    "The native stove did not open the cooking menu.");
                return;
            }

            foreach (Dish dish in Dishes) this.CookOne(kitchen, dish);
            kitchen.exitThisMenu(playSound: false);
            this.Finish(null);
        }
        catch (Exception error) { this.Finish(error.ToString()); }
    }

    private static bool MatchesWorld() => Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen
        && Constants.SaveFolderName == SaveName && Game1.uniqueIDForThisGame == FixtureId && Player.Name == FarmName && Player.farmName.Value == FarmName;

    private void Prepare()
    {
        Require(Player.currentLocation is FarmHouse && Player.HouseUpgradeLevel >= 1 && Player.MaxItems >= 12
            && Dishes.All(dish => !Player.knowsRecipe(dish.RecipeId) && !Player.recipesCooked.ContainsKey(dish.Output[3..])),
            "The fixture must be in an upgraded farmhouse with no previously learned or cooked mod dishes.");
        var house = (FarmHouse)Player.currentLocation;
        Require(house.fridge.Value.Items.All(item => item is null), "Use an empty native fridge so ingredient debits come from the prepared inventory.");
        var recipeData = this.Helper.GameContent.Load<Dictionary<string, string>>("Data/CookingRecipes");
        Require(recipeData.ContainsKey("Fried Egg") && recipeData.Keys.Where(key => key.StartsWith(Prefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal).SetEquals(Dishes.Select(dish => dish.RecipeId)),
            "The installed data must include all sixteen mod dishes and the vanilla fried egg.");
        var stock = this.Helper.GameContent.Load<Dictionary<string, ShopData>>("Data/Shops")["Saloon"].Items;
        this.OriginalMoney = Player.Money;
        this.OriginalTile = Player.Tile;
        this.OriginalTool = Player.CurrentToolIndex;
        this.OriginalFacing = Player.FacingDirection;
        this.OriginalInventory = Player.Items.ToArray();

        var rows = Dishes.ToDictionary(dish => dish, dish => stock.Single(row => row.IsRecipe && row.ItemId == dish.Output));
        Player.Money = rows.Values.Sum(row => row.Price);
        Require(Utility.TryOpenShopMenu("Saloon", "Gus", playOpenSound: false)
            && Game1.activeClickableMenu is ShopMenu { ShopId: "Saloon" }, "Could not open the original Gus stock menu.");
        var shop = (ShopMenu)Game1.activeClickableMenu;
        var tryPurchase = AccessTools.Method(typeof(ShopMenu), "tryToPurchaseItem")
            ?? throw new MissingMethodException(typeof(ShopMenu).FullName, "tryToPurchaseItem");
        foreach (Dish dish in Dishes)
        {
            var offered = shop.forSale.Single(item => item.QualifiedItemId == dish.Output && item.IsRecipe);
            int price = shop.itemPriceAndStock[offered].Price;
            // Native Object.Name decorates blueprint stock with " Recipe"; Object.name retains the ID used when learning it.
            string? internalName = (offered as SObject)?.name;
            string expectedOfferName = dish.RecipeId + " Recipe";
            Require(price > 0 && price == rows[dish].Price && Player.Money >= price && internalName == dish.RecipeId
                && offered.Name == expectedOfferName && shop.heldItem is null,
                $"Expected blueprint (item {dish.Output}, internal {dish.RecipeId}, offered {expectedOfferName}, price {rows[dish].Price}); "
                + $"actual (item {offered.QualifiedItemId}, internal {internalName}, offered {offered.Name}, price {price}, "
                + $"available money {Player.Money}, has held item {shop.heldItem is not null}).");
            int before = Player.Money;
            var inventory = Snapshot();
            tryPurchase.Invoke(shop, new object?[] { offered, null, 1, 0, 0 });
            Require(Player.Money == before - price && Player.cookingRecipes.ContainsKey(dish.RecipeId) && shop.heldItem is null
                && inventory.SequenceEqual(Snapshot()), $"Native blueprint purchase failed for {dish.RecipeId}.");
            this.Purchases.Add(new(dish.RecipeId, price, offered.QualifiedItemId, offered.Name));
        }
        shop.exitThisMenu(playSound: false);
        Require(Player.Money == 0 && this.Purchases.Count == Dishes.Length, "The native charges did not match the complete recipe stock.");
        Require(Utility.TryOpenShopMenu("Saloon", "Gus", playOpenSound: false)
            && Game1.activeClickableMenu is ShopMenu { ShopId: "Saloon" } reopened
            && !reopened.forSale.Any(item => item.IsRecipe && Dishes.Any(dish => dish.Output == item.QualifiedItemId)),
            "A learned mod recipe still appeared in the reopened original stock menu.");
        Game1.activeClickableMenu.exitThisMenu(playSound: false);
        this.Monitor.Log("Bought all sixteen blueprints through the original Gus menu and verified their exact charges.", LogLevel.Info);
        this.OpenStove(house);
    }

    private void OpenStove(FarmHouse house)
    {
        Point standing = house.getKitchenStandingSpot();
        var idealAction = standing.ToVector2() - Vector2.UnitY;
        var bounds = house.Map.Layers[0];
        var actions = from y in Enumerable.Range(0, bounds.LayerHeight)
                      from x in Enumerable.Range(0, bounds.LayerWidth)
                      let action = house.doesTileHaveProperty(x, y, "Action", "Buildings")
                      where string.Equals(action?.Trim(), "kitchen", StringComparison.OrdinalIgnoreCase)
                      orderby Vector2.DistanceSquared(new Vector2(x, y), idealAction)
                      select (At: new Vector2(x, y), Action: action!);
        foreach (var action in actions)
        for (int direction = 0; direction < Offsets.Length; direction++)
        {
            Vector2 stand = action.At + Offsets[direction];
            if (!house.isTileOnMap(stand) || house.isTileOnWall((int)stand.X, (int)stand.Y)
                || !house.CanItemBePlacedHere(stand, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)) continue;
            Player.setTileLocation(stand);
            Player.faceDirection(direction);
            var tile = new Tile((int)action.At.X, (int)action.At.Y);
            this.StoveRequestedAt = this.Watch.Elapsed;
            Require(house.performAction(action.Action, Player, tile), "The actual map stove action was refused.");
            return;
        }
        throw new InvalidOperationException("The upgraded farmhouse has no accessible native stove action.");
    }

    private void CookOne(CraftingPage kitchen, Dish dish)
    {
        int pageIndex = kitchen.pagesOfCraftingRecipes.FindIndex(page => page.Values.Any(recipe => recipe.name == dish.RecipeId));
        Require(pageIndex >= 0, $"Learned recipe {dish.RecipeId} is absent from the actual cooking page.");
        var recipe = kitchen.pagesOfCraftingRecipes[pageIndex].Single(pair => pair.Value.name == dish.RecipeId);
        Require(recipe.Value.recipeList.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(dish.Ingredients)
            && recipe.Value.recipeList.Values.All(count => count is >= 1 and <= 998)
            && recipe.Value.numberProducedPerCraft is >= 1 and <= 999,
            $"The original recipe parser did not retain the intended ingredients or yield for {dish.RecipeId}.");
        var actualIngredients = recipe.Value.recipeList.ToDictionary(pair => pair.Key == EggCategory ? dish.Egg! : pair.Key, pair => pair.Value);
        if (dish.Egg is not null)
        {
            Require(ItemRegistry.Create(dish.Egg).Category == -5, $"{dish.Egg} is not a native egg-category fixture.");
            Require(recipe.Value.recipeList.ContainsKey(EggCategory), $"{dish.RecipeId} restricted eggs to a single item ID.");
        }
        kitchen.currentCraftingPage = pageIndex;
        Point center = recipe.Key.bounds.Center;
        int beforeCooked = CookedCount(dish);
        bool rejectedWithoutEgg = false;
        if (dish.Egg is not null)
        {
            Stock(actualIngredients.Where(pair => pair.Key != dish.Egg));
            var before = Snapshot();
            kitchen.receiveLeftClick(center.X, center.Y);
            Require(kitchen.heldItem is null && beforeCooked == CookedCount(dish) && before.SequenceEqual(Snapshot()),
                $"Cooking {dish.RecipeId} without an egg debited some inputs or created an output.");
            rejectedWithoutEgg = true;
        }

        Stock(actualIngredients);
        int produced = recipe.Value.numberProducedPerCraft;
        kitchen.receiveLeftClick(center.X, center.Y);
        if (kitchen.heldItem is { } held)
        {
            int free = Player.Items.ToList().FindIndex(item => item is null);
            Require(free >= 0 && held.QualifiedItemId == dish.Output && held.Stack == produced && held.Quality == 0,
                $"Unexpected native held output for {dish.RecipeId}.");
            Point empty = kitchen.inventory.inventory[free].bounds.Center;
            kitchen.receiveLeftClick(empty.X, empty.Y);
        }
        Require(kitchen.heldItem is null && CookedCount(dish) == beforeCooked + 1 && Player.Items.CountId(dish.Output) == produced
            && Player.Items.OfType<Item>().Where(item => item.QualifiedItemId == dish.Output).All(item => item.Quality == 0)
            && actualIngredients.All(pair => Player.Items.CountId(pair.Key) == 1) && Player.Items.CountId(Wood) == 2
            && Player.Items.OfType<Item>().All(item => item.QualifiedItemId == dish.Output || item.QualifiedItemId == Wood
                || actualIngredients.ContainsKey(item.QualifiedItemId)),
            $"Native cooking of {dish.RecipeId} produced the wrong output or changed another ingredient.");
        this.Cooked.Add(new(dish.RecipeId, pageIndex, dish.Output, produced, actualIngredients, dish.Egg,
            rejectedWithoutEgg, CookedCount(dish) - beforeCooked));
        this.Monitor.Log($"Cooked {dish.RecipeId} through the original stove menu.", LogLevel.Info);
    }

    private static void Stock(IEnumerable<KeyValuePair<string, int>> ingredients)
    {
        var slots = new Item[Player.MaxItems];
        int index = 0;
        foreach (var pair in ingredients)
        {
            var item = ItemRegistry.Create(pair.Key, pair.Value + 1);
            Require(item.QualifiedItemId == pair.Key && item.Stack == pair.Value + 1, $"Kitchen input {pair.Key} was unavailable.");
            slots[index++] = item;
        }
        slots[index] = ItemRegistry.Create(Wood, 2);
        Player.Items.OverwriteWith(slots);
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        if (this.OriginalInventory is not null && MatchesWorld())
        {
            if (Game1.activeClickableMenu is CraftingPage { cooking: true } or ShopMenu { ShopId: "Saloon" })
                Game1.activeClickableMenu.exitThisMenu(playSound: false);
            Player.Items.OverwriteWith(this.OriginalInventory);
            Player.Money = this.OriginalMoney;
            Player.CurrentToolIndex = this.OriginalTool;
            Player.setTileLocation(this.OriginalTile);
            Player.faceDirection(this.OriginalFacing);
            foreach (Dish dish in Dishes)
            {
                Player.cookingRecipes.Remove(dish.RecipeId);
                Player.recipesCooked.Remove(dish.Output[3..]);
            }
        }
        this.Helper.Data.WriteJsonFile("p2-kitchen-result.json", new
        {
            SchemaVersion = 1, Scenario, ProcessId = Environment.ProcessId, FixtureId, Completed = failure is null,
            Failure = failure, this.LoadCount, this.SavingEvents, this.Purchases, this.Cooked,
            Preparation = "The fixture supplies starting funds and cooking ingredients and sets a standing tile. It opens native Gus stock directly and the stove via the actual farmhouse map action, and does not save."
        });
        this.Monitor.Log(failure ?? "P2 native cooking and blueprint purchases passed for all sixteen dishes.", failure is null ? LogLevel.Info : LogLevel.Error);
    }

    private static string O(string key) => "(O)" + Prefix + key;
    private static int CookedCount(Dish dish) => Player.recipesCooked.TryGetValue(dish.Output[3..], out int count) ? count : 0;
    private static StackValue[] Snapshot() => Player.Items.Select(item => new StackValue(item?.QualifiedItemId, item?.Stack ?? 0, item?.Quality ?? 0)).ToArray();
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    private sealed record Dish(string Key, string[] Ingredients, string? Egg = null)
    {
        internal string RecipeId => Prefix + "Recipe_" + Key;
        internal string Output => O(Key);
    }
    private sealed record Purchase(string RecipeId, int Price, string ItemId, string NativeOfferName);
    private sealed record Cook(string RecipeId, int Page, string Output, int Quantity, Dictionary<string, int> Inputs,
        string? Egg, bool RejectedWithoutEgg, int NativeCookedCountDelta);
    private sealed record StackValue(string? Id, int Count, int Quality);
}
