using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Objects;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Exercises the installed item's native data, sale, consumption and gift paths in one disposable world.</summary>
internal sealed class P3ItemUseScenario
{
    internal const string Scenario = "P3ItemUse", FarmName = "XYSP3ItemUse";
    internal const ulong FixtureId = 620261134;
    private const string Prefix = "Sora6A.XiangyeShiling_";
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private static Farmer Player => Game1.player;

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<object> Items = new();
    private readonly List<object> Sales = new();
    private int LoadDelay = -1, LoadCount, SavingEvents, Stage, CaptureDelay;
    private string? Failure, Capture;
    private bool? PreviousPauseWhenOutOfFocus;
    private NPC? Robin;
    private object? Food, Gift;

    internal P3ItemUseScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "Bind item-use smoke to the exact disposable farm.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.LoadCount++;
    internal void OnSaving() { this.SavingEvents++; this.Failure = "Unexpected native save during item-use validation."; }

    internal void Tick()
    {
        if (this.Stage == 6) { Game1.game1.Exit(); return; }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "The exact disposable item-use save is absent or another world is loaded.");
                this.Watch.Start();
                Game1.activeClickableMenu = null;
                SaveGame.Load(SaveName);
            }
            if (!this.Watch.IsRunning) return;
            Require(this.Failure is null, this.Failure ?? "A frame or save audit failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromSeconds(85), "Timed out in the installed item-use smoke.");
            if (!Context.IsWorldReady || this.LoadCount == 0) return;
            Require(this.LoadCount == 1 && Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen
                && Game1.uniqueIDForThisGame == FixtureId && Player.Name == FarmName && Player.farmName.Value == FarmName,
                "A different test farmer or world is active.");
            if (this.PreviousPauseWhenOutOfFocus is null)
            {
                this.PreviousPauseWhenOutOfFocus = Game1.options.pauseWhenOutOfFocus;
                Game1.options.pauseWhenOutOfFocus = false;
            }
            switch (this.Stage)
            {
                case 0 when Context.IsPlayerFree && !Game1.fadeToBlack: this.ReadContentAndExercisePrices(); break;
                case 1 when this.Capture is null:
                    Game1.activeClickableMenu?.exitThisMenu(playSound: false);
                    this.Stage = 2;
                    break;
                case 2 when Context.IsPlayerFree && !Game1.fadeToBlack: this.BeginEating(); break;
                case 3 when !Player.isEating: this.CheckFoodAndWarp(); break;
                case 4 when !Game1.fadeToBlack && this.Robin is { } robin && ReferenceEquals(Player.currentLocation, robin.currentLocation):
                    this.GiveGift(robin); break;
                case 5 when this.Capture is null: this.Finish(null); break;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.ToString()); }
    }

    internal void OnRendered()
    {
        if (this.Capture is not { } filename || this.CaptureDelay-- > 0) return;
        try
        {
            GraphicsDevice device = Game1.graphics.GraphicsDevice;
            int width = device.PresentationParameters.BackBufferWidth, height = device.PresentationParameters.BackBufferHeight;
            var pixels = new Color[checked(width * height)];
            device.GetBackBufferData(pixels);
            using var texture = new Texture2D(device, width, height);
            texture.SetData(pixels);
            using var stream = File.Create(Path.Combine(this.Helper.DirectoryPath, filename));
            texture.SaveAsPng(stream, width, height);
            this.Capture = null;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Failure = error.ToString(); }
    }

    private void ReadContentAndExercisePrices()
    {
        var objects = this.Helper.GameContent.Load<Dictionary<string, ObjectData>>("Data/Objects");
        var machines = this.Helper.GameContent.Load<Dictionary<string, BigCraftableData>>("Data/BigCraftables");
        var crops = this.Helper.GameContent.Load<Dictionary<string, CropData>>("Data/Crops");
        var active = objects.Where(entry => entry.Key.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
        var equipment = machines.Keys.Where(key => key.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
        Require(active.Length == 38 && equipment.Length == 2 && objects.ContainsKey("262") && machines.ContainsKey("FishSmoker"),
            "The loaded content does not contain the full catalog alongside the original game data.");
        Require(crops.Where(pair => pair.Key.StartsWith(Prefix, StringComparison.Ordinal)).ToArray() is { Length: 2 } addedCrops
            && addedCrops.All(pair => !pair.Value.CountForMonoculture && !pair.Value.CountForPolyculture),
            "The two added crops changed the required achievement pools.");
        Require(ItemRegistry.Create<SObject>("(O)262").countsForShippedCollection(), "The original shipping entry failed its native positive control.");
        var professions = Player.professions.ToArray();
        Player.professions.Clear();
        foreach (var (key, data) in active)
        {
            var item = ItemRegistry.Create<SObject>("(O)" + key);
            int price = item.sellToStorePrice(Player.UniqueMultiplayerID);
            Require(item.QualifiedItemId == "(O)" + key && price == data.Price && item.Category == data.Category
                && !item.countsForShippedCollection() && item.canBeShipped() && item.canBeGivenAsGift(), $"Native data mismatch for {key}.");
            CheckSprite(item.QualifiedItemId);
            this.Items.Add(new { item.QualifiedItemId, item.DisplayName, item.Category, BasePrice = price, item.Edibility,
                Energy = item.staminaRecoveredOnConsumption(), Health = item.healthRecoveredOnConsumption(),
                ShippingCollection = item.countsForShippedCollection() });
        }
        foreach (string key in equipment)
        {
            var machine = ItemRegistry.Create<SObject>("(BC)" + key);
            Require(machine.QualifiedItemId == "(BC)" + key && !machine.canBeShipped(), $"Machine shipping restriction changed for {key}.");
            CheckSprite(machine.QualifiedItemId);
        }

        var soy = Object("Soybean", quality: 2);
        var tofu = Object("Tofu");
        var dish = Object("MapoTofu");
        int goodSoy = soy.sellToStorePrice(Player.UniqueMultiplayerID);
        int plainTofu = tofu.sellToStorePrice(Player.UniqueMultiplayerID);
        int plainDish = dish.sellToStorePrice(Player.UniqueMultiplayerID);
        Player.professions.Add(Farmer.tiller);
        Player.professions.Add(Farmer.artisan);
        int tiller = soy.sellToStorePrice(Player.UniqueMultiplayerID);
        int artisan = tofu.sellToStorePrice(Player.UniqueMultiplayerID);
        Require(goodSoy > Object("Soybean").Price && tiller == (int)(goodSoy * 1.1f)
            && artisan == (int)(plainTofu * 1.4f) && dish.sellToStorePrice(Player.UniqueMultiplayerID) == plainDish,
            "Original quality, Tiller, Artisan or cooked-food pricing differed from the tested rules.");
        this.Sell(soy, tiller);
        this.Sell(tofu, artisan);
        this.Sell(dish, plainDish);
        Player.professions.Clear();
        foreach (int profession in professions) Player.professions.Add(profession);
        Player.Items[0] = Object("Soybean");
        Player.Items[1] = Object("SmokedPork");
        Player.Items[2] = Object("MapoTofu");
        Player.Items[3] = ItemRegistry.Create("(BC)" + Prefix + "BeanWorkshop");
        Player.Items[4] = ItemRegistry.Create("(BC)" + Prefix + "FermentationCrock");
        Game1.activeClickableMenu = new GameMenu();
        this.Capture = "p3-native-inventory.png";
        this.CaptureDelay = 3;
        this.Stage = 1;
    }

    private static void CheckSprite(string id)
    {
        var data = ItemRegistry.GetData(id) ?? throw new InvalidOperationException($"Missing texture data for {id}.");
        Rectangle source = data.GetSourceRect();
        Require(source.Width == 16 && source.Height == (id.StartsWith("(BC)", StringComparison.Ordinal) ? 32 : 16)
            && data.GetTexture().Bounds.Contains(source), $"Native sprite coordinates are invalid for {id}.");
    }

    private void Sell(SObject item, int nativePrice)
    {
        Player.Items[0] = item;
        Require(Utility.TryOpenShopMenu("SeedShop", "Pierre", playOpenSound: false)
            && Game1.activeClickableMenu is ShopMenu, "Pierre's native sell menu did not open.");
        var shop = (ShopMenu)Game1.activeClickableMenu;
        Require(shop.highlightItemToSell(item), $"Pierre refused {item.QualifiedItemId}.");
        int before = Player.Money;
        float multiplier = this.Helper.Reflection.GetField<float>(shop, "sellPercentage").GetValue();
        shop.update(new GameTime(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300)));
        Point slot = shop.inventory.inventory[0].bounds.Center;
        shop.receiveLeftClick(slot.X, slot.Y);
        shop.receiveLeftClick(slot.X, slot.Y);
        Require(Player.Items[0] is null && Player.Money - before == (int)(nativePrice * multiplier), $"Exact native payout failed for {item.QualifiedItemId}.");
        this.Sales.Add(new { item.QualifiedItemId, item.Quality, NativePrice = nativePrice, Paid = Player.Money - before });
        shop.exitThisMenu(playSound: false);
    }

    private void BeginEating()
    {
        Player.Stamina = 20;
        Player.health = 10;
        var meal = Object("MapoTofu");
        Player.Items[2] = null;
        Player.Items[0] = meal;
        Player.CurrentToolIndex = 0;
        Player.mostRecentlyGrabbedItem = meal;
        Player.eatHeldObject();
        Require(Player.isEating && Player.Items[0] is null, "Native eating did not consume the selected dish.");
        this.Stage = 3;
    }

    private void CheckFoodAndWarp()
    {
        var meal = Object("MapoTofu");
        int energy = meal.staminaRecoveredOnConsumption(), health = meal.healthRecoveredOnConsumption();
        Require(Math.Abs(Player.Stamina - Math.Min(Player.MaxStamina, 20 + energy)) < .01
            && Player.health == Math.Min(Player.maxHealth, 10 + health), "Native meal recovery differs from the item description.");
        this.Food = new { meal.QualifiedItemId, Energy = energy, Health = health };
        this.Robin = Game1.getCharacterFromName("Robin") ?? throw new InvalidOperationException("Original NPC Robin is missing.");
        Vector2 where = this.Robin.Tile;
        Game1.warpFarmer(this.Robin.currentLocation.NameOrUniqueName, (int)where.X, (int)where.Y + 1, false);
        this.Stage = 4;
    }

    private void GiveGift(NPC robin)
    {
        string name = robin.Name;
        int before = Player.getFriendshipLevelForNPC(name);
        Player.Items[0] = Object("MapoTofu", stack: 2);
        Player.CurrentToolIndex = 0;
        Require(robin.tryToReceiveActiveObject(Player, probe: true) && Player.Items[0]?.Stack == 2, "Gift probe mutated the inventory or was blocked.");
        Require(robin.tryToReceiveActiveObject(Player, probe: false) && Player.Items[0]?.Stack == 1
            && Player.friendshipData[name].GiftsToday == 1, "Native gift did not deduct exactly one item and register the daily counter.");
        int points = Player.getFriendshipLevelForNPC(name);
        Game1.activeClickableMenu?.exitThisMenu(playSound: false);
        robin.tryToReceiveActiveObject(Player, probe: false);
        Require(Player.Items[0]?.Stack == 1 && Player.friendshipData[name].GiftsToday == 1
            && Player.getFriendshipLevelForNPC(name) == points, "Same-day repeated gift changed original friendship or inventory.");
        this.Gift = new { Npc = name, Points = points - before, CountToday = 1, DuplicateRejected = true };
        this.Capture = "p3-native-gift.png";
        this.CaptureDelay = 3;
        this.Stage = 5;
    }

    private static SObject Object(string name, int stack = 1, int quality = 0) => ItemRegistry.Create<SObject>("(O)" + Prefix + name, stack, quality);

    private void Finish(string? error)
    {
        if (this.PreviousPauseWhenOutOfFocus is bool pause) Game1.options.pauseWhenOutOfFocus = pause;
        if (error is not null) this.Monitor.Log(error, LogLevel.Error);
        this.Helper.Data.WriteJsonFile("p3-item-use-result.json", new
        {
            Scenario, FixtureId, ProcessId = Environment.ProcessId, Completed = error is null && this.SavingEvents == 0,
            Failure = error, this.LoadCount, this.SavingEvents, this.Items, this.Sales, this.Food, this.Gift,
            Preparation = "Dedicated in-memory stock, professions, location and food; native pricing, purchase menu sale, actual eating and NPC gift."
        });
        this.Stage = 6;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
