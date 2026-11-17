using System.Diagnostics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using XiangyeShiling.GameIntegration;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Checks live CP data, native purchases, plantability and crop progression in one unsaved fixture.</summary>
internal sealed class P2CropShopScenario
{
    internal const string Scenario = "P2CropShop";
    internal const string FarmName = "XYSP2Crops";
    internal const ulong FixtureId = 620261127;
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private const string Id = MachineService.Prefix + "_";
    private const string Soy = "(O)" + Id + "Soybean", Napa = "(O)" + Id + "NapaCabbage";
    private const string SoySeed = "(O)" + Id + "SoybeanSeeds", NapaSeed = "(O)" + Id + "NapaCabbageSeeds";
    private const string Pork = "(O)" + Id + "Pork", Fava = "(O)" + Id + "BroadBeans";
    private static readonly Vector2 Below = Vector2.UnitY;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<object> Evidence = new();
    private int LoadDelay = -1, LoadCount, SavingEvents;
    private bool FarmRequested, Finished;
    private string? Failure;
    private static Farmer Player => Game1.player;

    internal P2CropShopScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "Bind the crop and shop test to its exact disposable save.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.LoadCount++;
    internal void OnSaving() { this.SavingEvents++; this.Failure = "An unexpected save occurred during the crop and shop test."; }
    internal void OnReturnedToTitle() { if (this.LoadCount > 0 && !this.Finished) this.Failure = "The crop and shop world returned to title."; }

    internal void Tick()
    {
        if (this.Finished) { Game1.game1.Exit(); return; }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "Missing crop fixture or another world is already loaded.");
                Game1.activeClickableMenu = null;
                this.Watch.Start();
                SaveGame.Load(SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "Crop test failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(3), "Timed out loading the disposable world or reaching the farm.");
            if (this.LoadCount == 0 || !Context.IsPlayerFree || Game1.eventUp || Game1.isWarping || Game1.fadeToBlack) return;
            Require(this.LoadCount == 1 && Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen
                && Constants.SaveFolderName == SaveName && Game1.uniqueIDForThisGame == FixtureId
                && Player.farmName.Value == FarmName && Player.Name == FarmName, "The active world or farmer differs from the crop fixture.");
            if (!this.FarmRequested)
            {
                Require(Player.currentLocation.Name == "FarmHouse" && new[] { Soy, Napa, SoySeed, NapaSeed, Pork, Fava }.All(id => Count(id) == 0),
                    "Load the untouched crop fixture inside its farmhouse.");
                this.FarmRequested = true;
                Point door = Game1.getFarm().GetMainFarmHouse().getPointForHumanDoor();
                Game1.warpFarmer("Farm", door.X, door.Y + 1, 2);
                return;
            }
            Require(Player.currentLocation is Farm, "The native farm warp did not land on the farm.");
            this.VerifyObjectData();
            var crops = this.Helper.GameContent.Load<Dictionary<string, CropData>>("Data/Crops");
            var shops = this.Helper.GameContent.Load<Dictionary<string, ShopData>>("Data/Shops");
            var soyCrop = this.VerifyCropData(crops, SoySeed, Soy, 0, Season.Summer, Season.Fall);
            var napaCrop = this.VerifyCropData(crops, NapaSeed, Napa, 1, Season.Fall);
            Require(crops["472"].HarvestItemId == "24" && crops["490"].HarvestItemId == "276", "Vanilla parsnip or pumpkin crop was replaced.");
            Player.Money = 25000;
            this.VerifyShops(shops);
            this.Grow((Farm)Player.currentLocation, SoySeed, Soy, soyCrop, Season.Summer, 28);
            this.Grow((Farm)Player.currentLocation, NapaSeed, Napa, napaCrop, Season.Fall, 1);
            this.Finish(null);
        }
        catch (Exception error) { this.Finish(error.ToString()); }
    }

    private void VerifyObjectData()
    {
        const string atlas = "Mods/Sora6A.XiangyeShiling/Objects";
        var objects = this.Helper.GameContent.Load<Dictionary<string, ObjectData>>("Data/Objects");
        foreach (var (item, index, category) in new[] { (Soy, 4, -75), (Napa, 5, -75), (Pork, 6, -14), (Fava, 7, -75),
            (SoySeed, 36, -74), (NapaSeed, 37, -74) })
        {
            Require(objects.TryGetValue(item[3..], out var data) && data.Texture == atlas && data.SpriteIndex == index
                && data.Category == category && data.Price > 0 && (category != -74 || data.Type == "Seeds")
                && !ItemRegistry.GetDataOrErrorItem(item).IsErrorItem, $"Effective native object metadata differs for {item}.");
        }
        var texture = this.Helper.GameContent.Load<Texture2D>(atlas);
        Require(texture.Width == 128 && texture.Height == 80, "The effective object texture cannot supply reserved item slots.");
        this.Record("Effective native object data registered both seeds and four ingredients with the original mod atlas.");
    }

    private CropData VerifyCropData(IReadOnlyDictionary<string, CropData> crops, string seed, string product, int sprite, params Season[] seasons)
    {
        Require(crops.TryGetValue(seed[3..], out var data) && data is not null && data.Seasons.SequenceEqual(seasons)
            && ItemRegistry.GetData(data.HarvestItemId)?.QualifiedItemId == product
            && data.DaysInPhase.Count == 5 && data.DaysInPhase.All(days => days > 0) && data.DaysInPhase.Sum() < 28
            && data.RegrowDays == -1 && data.NeedsWatering && !data.IsRaised && !data.IsPaddyCrop
            && data.HarvestMethod == HarvestMethod.Grab && data.HarvestMinStack == 1 && data.HarvestMaxStack == 1
            && data.SpriteIndex == sprite && data.Texture == "Mods/Sora6A.XiangyeShiling/Crops", $"Effective CP crop metadata differs for {seed}.");
        var texture = this.Helper.GameContent.Load<Texture2D>(data!.Texture);
        Require(texture.Width == 256 && texture.Height == 32, "The live crop texture cannot provide the required source frames.");
        this.Record($"Effective native crop data registered {seed} → {product} with {data.DaysInPhase.Sum()} growth days.");
        return data;
    }

    private void VerifyShops(IReadOnlyDictionary<string, ShopData> shops)
    {
        var pierre = shops["SeedShop"].Items;
        var gus = shops["Saloon"].Items;
        var soyStock = pierre.Single(row => row.ItemId == Soy);
        var napaStock = pierre.Single(row => row.ItemId == Napa);
        Require(new[] { soyStock, napaStock }.All(row => row.AvailableStock is > 0 and <= 999 && row.AvailableStockLimit == LimitedStockMode.Global),
            "Pierre's produced vegetables must have one finite shared stock line each.");
        Require(new[] { Pork, Fava }.All(id => gus.Single(row => row.ItemId == id) is { AvailableStock: -1, Condition: null or "" }),
            "Gus must carry one season-independent, regular stock line for each new ingredient.");

        foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
        {
            SetDate(season, 1);
            ShopMenu shop = OpenShop("SeedShop", "Pierre");
            bool soySeason = season is Season.Summer or Season.Fall;
            bool napaSeason = season == Season.Fall;
            Require((Offered(shop, SoySeed) is not null) == soySeason && (Offered(shop, NapaSeed) is not null) == napaSeason
                && (Offered(shop, Soy) is not null) != soySeason && (Offered(shop, Napa) is not null) != napaSeason,
                $"Pierre's native {season} seed or off-season stock differs.");
            string? vanilla = season switch { Season.Spring => "(O)472", Season.Summer => "(O)482", Season.Fall => "(O)490", _ => null };
            Require(vanilla is null || Offered(shop, vanilla) is not null, $"Pierre's original {season} seed stock disappeared.");
            if (season == Season.Summer) Buy(shop, SoySeed, 1, pierre.Single(row => row.ItemId == SoySeed).Price);
            if (season == Season.Fall) Buy(shop, NapaSeed, 1, pierre.Single(row => row.ItemId == NapaSeed).Price);
            shop.exitThisMenu(playSound: false);

            ShopMenu saloon = OpenShop("Saloon", "Gus");
            Require(new[] { "(O)346", Pork, Fava }.All(id => Offered(saloon, id) is not null), $"Gus's native {season} stock differs.");
            if (season == Season.Spring)
            {
                Buy(saloon, Pork, 1, gus.Single(row => row.ItemId == Pork).Price);
                Buy(saloon, Fava, 1, gus.Single(row => row.ItemId == Fava).Price);
            }
            saloon.exitThisMenu(playSound: false);
            this.Record($"Native shops retained vanilla stock and exposed the expected {season} rows.");
        }

        SetDate(Season.Spring, 1);
        ShopMenu initial = OpenShop("SeedShop", "Pierre");
        Require(Stock(initial, Soy) == soyStock.AvailableStock && Stock(initial, Napa) == napaStock.AvailableStock,
            "Native spring vegetable stock differs from the effective global caps.");
        Buy(initial, Soy, soyStock.AvailableStock, soyStock.Price);
        Buy(initial, Napa, napaStock.AvailableStock, napaStock.Price);
        initial.exitThisMenu(playSound: false);
        ShopMenu reopened = OpenShop("SeedShop", "Pierre");
        Require(Offered(reopened, Soy) is null && Offered(reopened, Napa) is null, "Reopening the native shop refilled same-day stock.");
        reopened.exitThisMenu(playSound: false);
        SetDate(Season.Spring, 2);
        // Exercise the real team day-reset stock path without advancing or saving the whole world.
        Player.team.NewDay();
        ShopMenu nextDay = OpenShop("SeedShop", "Pierre");
        Require(Stock(nextDay, Soy) == soyStock.AvailableStock && Stock(nextDay, Napa) == napaStock.AvailableStock,
            "The native daily reset did not refill both finite global stock lines.");
        nextDay.exitThisMenu(playSound: false);
        this.Record($"Native spring limits {soyStock.AvailableStock}/{napaStock.AvailableStock} depleted across reopening and refilled on the team day reset.");
    }

    private void Grow(Farm farm, string seed, string product, CropData data, Season plantingSeason, int plantingDay)
    {
        SetDate(Season.Spring, 2);
        var (tile, dirt) = Till(farm);
        int slot = Player.Items.ToList().FindIndex(item => item?.QualifiedItemId == seed);
        Require(slot is >= 0 and < 12, "The bought seed is not in the toolbar.");
        Player.CurrentToolIndex = slot;
        Player.setTileLocation(tile + Below);
        Player.faceDirection(0);
        var item = Player.ActiveObject ?? throw new InvalidOperationException("The bought seed is not active.");
        int x = (int)tile.X * 64 + 32, y = (int)tile.Y * 64 + 32;
        Require(!Utility.tryToPlaceItem(farm, item, x, y) && dirt.crop is null && Count(seed) == 1,
            $"The original planting path consumed or planted {seed} in spring.");

        SetDate(plantingSeason, plantingDay);
        Require(Utility.tryToPlaceItem(farm, item, x, y) && dirt.crop is { } && Count(seed) == 0 && !dirt.readyForHarvest(),
            $"The native placement path did not plant and debit {seed}.");
        int growth = data.DaysInPhase.Sum();
        for (int elapsed = 1; elapsed <= growth; elapsed++)
        {
            int calendar = plantingDay - 1 + elapsed;
            SetDate((Season)((int)plantingSeason + calendar / 28), calendar % 28 + 1);
            dirt.state.Value = HoeDirt.watered;
            dirt.dayUpdate();
            Require(dirt.crop is { } && (dirt.readyForHarvest() == (elapsed == growth)),
                $"Native watered day {elapsed} matured or lost {seed} at the wrong time.");
        }
        int before = Count(product);
        Require(dirt.performUseAction(tile) && Count(product) == before + 1 && dirt.crop is null,
            $"Native harvest did not put exactly one {product} in the backpack and clear the plant.");
        this.Record($"Planted the purchased {seed} in {plantingSeason} {plantingDay}; after {growth} native soil updates harvested {product} on {Game1.season} {Game1.dayOfMonth}.");
    }

    private static (Vector2 Tile, HoeDirt Dirt) Till(Farm farm)
    {
        var bounds = farm.Map.Layers[0];
        for (int y = 4; y + 2 < bounds.LayerHeight; y++)
        for (int x = 4; x + 2 < bounds.LayerWidth; x++)
        {
            Vector2 tile = new(x, y);
            if (farm.doesTileHaveProperty(x, y, "Diggable", "Back") is null || farm.terrainFeatures.ContainsKey(tile)
                || !farm.CanItemBePlacedHere(tile) || !farm.CanItemBePlacedHere(tile + Below)) continue;
            if (farm.makeHoeDirt(tile, false) && farm.GetHoeDirtAtTile(tile) is { crop: null } dirt) return (tile, dirt);
        }
        throw new InvalidOperationException("No free spot for native tilled soil.");
    }

    private static void SetDate(Season season, int day)
    {
        Game1.season = season;
        Game1.dayOfMonth = day;
        Game1.timeOfDay = 1300;
        Game1.netWorldState.Value.UpdateFromGame1();
    }

    private static ShopMenu OpenShop(string shop, string owner) => Utility.TryOpenShopMenu(shop, owner, playOpenSound: false)
        && Game1.activeClickableMenu is ShopMenu menu && menu.ShopId == shop
        ? menu : throw new InvalidOperationException($"Could not open the original {shop} menu.");

    private static ISalable? Offered(ShopMenu shop, string id) => shop.forSale.SingleOrDefault(item => item.QualifiedItemId == id && !item.IsRecipe);

    private static int Stock(ShopMenu shop, string id) => Offered(shop, id) is { } item ? shop.itemPriceAndStock[item].Stock : 0;

    private static void Buy(ShopMenu shop, string id, int amount, int expectedPrice)
    {
        var stock = Offered(shop, id) ?? throw new InvalidOperationException($"Missing native stock {id}.");
        int price = shop.itemPriceAndStock[stock].Price, funds = Player.Money, before = Count(id);
        Require(price > 0 && price == expectedPrice && funds >= price * amount && Stock(shop, id) >= amount && shop.heldItem is null,
            $"Cannot buy {id} × {amount} at its effective data price.");
        AccessTools.Method(typeof(ShopMenu), "tryToPurchaseItem").Invoke(shop, new object?[] { stock, null, amount, 0, 0 });
        Require(shop.heldItem is Item held && held.QualifiedItemId == id && held.Stack == amount && Player.addItemToInventory(held) is null,
            $"The native purchase did not deliver {id} × {amount}.");
        shop.heldItem = null;
        Require(Player.Money == funds - price * amount && Count(id) == before + amount, $"Wrong native payment or inventory for {id}.");
    }

    private static int Count(string id) => Player.Items.CountId(id);

    private void Record(string step)
    {
        this.Evidence.Add(new { Step = step, Season = Game1.season.ToString(), Day = Game1.dayOfMonth });
        this.Monitor.Log(step, LogLevel.Info);
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        if (failure is not null) this.Monitor.Log(failure, LogLevel.Error);
        this.Helper.Data.WriteJsonFile("p2-crop-shop-result.json", new
        {
            SchemaVersion = 1, ProcessId = Environment.ProcessId, FixtureId, Completed = failure is null,
            Failure = failure, this.LoadCount, this.SavingEvents, this.Evidence,
            Preparation = "The fixture sets funds, dates, standing positions and wet soil, opens the original shop menus directly, and runs the original team and soil day-update methods. It never saves."
        });
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
