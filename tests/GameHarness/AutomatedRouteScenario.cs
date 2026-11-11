using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// 无人值守的 P1 单人集成场景。只在专用副本内准备原版建材和站位，
/// 通过真实游戏交易、制作、加工和厨房入口验证同一批物品的去向。完成或失败后退出，不保存。
/// </summary>
internal sealed class AutomatedRouteScenario
{
    private const string FarmName = "XYSP1Route";
    private const string Id = MachineService.Prefix;
    private const string Wheat = "(O)262";
    private static readonly Vector2 Below = new(0, 1);
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Settings;
    private readonly Stopwatch Watch = new();
    private readonly List<string> Passed = new();
    private FarmHouse House = null!;
    private SObject Bean = null!, Crock = null!;
    private Vector2 Stove, StoveStanding;
    private string BeanRaw = "", CrockRaw = "";
    private int LoadDelay = -1, StartClock, LastClock, ClockTicks, StartingMoney;
    private long StartMinutes;
    private bool Loaded, Waiting, Eating, Finished, SavingObserved;
    private KitchenStep Kitchen;
    private TimeSpan KitchenRequestedAt;
    private Options? ChangedOptions;
    private bool OriginalPauseSetting;
    private static Farmer Player => Game1.player;
    private string SaveName => $"{FarmName}_{this.Settings.SaveFixtureId}";
    private enum KitchenStep { None, Preflight, Cook }

    public AutomatedRouteScenario(IModHelper helper, IMonitor monitor, Config settings)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        this.Settings = settings;
        Require(settings.FarmName == FarmName && settings.SaveFixtureId != 0 && !settings.OpenPreview,
            "AutomatedRoute requires an explicitly bound XYSP1Route fixture.");
    }

    public void QueueLoad() => this.LoadDelay = 180;
    public void OnLoaded() => this.Loaded = true;
    private bool MatchesWorld() => Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && Constants.SaveFolderName == this.SaveName && Game1.uniqueIDForThisGame == this.Settings.SaveFixtureId
        && Player.farmName.Value == FarmName;

    public void Tick()
    {
        if (this.Finished)
        {
            Game1.game1.Exit();
            return;
        }
        this.Guard(() =>
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, this.SaveName, this.SaveName)),
                    "Dedicated fixture missing or a world is already active.");
                Game1.activeClickableMenu = null;
                this.Watch.Start();
                SaveGame.Load(this.SaveName);
                return;
            }
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(8), "Timed out waiting for native load, clock or eating.");
            if (this.Loaded && Context.IsPlayerFree)
            {
                this.Loaded = false;
                Require(this.MatchesWorld(), "Refuse to modify a world outside the dedicated fixture.");
                this.PrepareAndStart();
            }
            if (this.Waiting || this.Eating || this.Kitchen != KitchenStep.None)
                Require(this.MatchesWorld(), "Dedicated world changed during the route.");
            if (this.Kitchen != KitchenStep.None) this.PollKitchen();
            if (!this.Eating || Player.isEating) return;
            Require(Count("ToonTofu") == 0 && Count("Tofu") == 1 && Count("SoySauce") == 4
                && Player.Money == this.StartingMoney - 4510, "Eating did not leave the exact route inventory and funds.");
            this.Pass("ate the same dish using the native farmer action");
            this.Finish(null);
        });
    }

    private void PrepareAndStart()
    {
        this.House = Player.currentLocation as FarmHouse ?? throw new InvalidOperationException("Load inside the upgraded farmhouse.");
        Require(!this.House.objects.Values.Any(item => MachineService.TryGetKind(item, out _))
            && new[] { "BeanWorkshop", "FermentationCrock", "ToonTofu" }.All(key => !Player.knowsRecipe(RecipeId(key))),
            "Use a fresh fixture with no route devices or learned recipes.");

        this.StartingMoney = Player.Money;
        var slots = new Item[Player.MaxItems];
        slots[0] = ItemRegistry.Create("(O)388", 80);
        slots[1] = ItemRegistry.Create("(O)390", 60);
        slots[2] = ItemRegistry.Create("(O)334");
        Player.Items.OverwriteWith(slots);
        // Hidden verification doesn't receive desktop focus. Keep the native clock intact and restore this option before exiting.
        this.ChangedOptions = Game1.options;
        this.OriginalPauseSetting = Game1.options.pauseWhenOutOfFocus;
        Game1.options.pauseWhenOutOfFocus = false;

        Buy("Carpenter", MachineId("BeanWorkshop"), 1, true);
        Buy("Carpenter", MachineId("FermentationCrock"), 1, true);
        Require(Player.Money == this.StartingMoney - 3000 && Player.knowsRecipe(RecipeId("BeanWorkshop"))
            && Player.knowsRecipe(RecipeId("FermentationCrock")), "Carpenter payment or learned recipes differ.");
        this.Pass("Carpenter taught both device recipes for 3000g");
        Buy("SeedShop", ObjectId("Soybean"), 6);
        Buy("SeedShop", Wheat, 2);
        Require(Player.Money == this.StartingMoney - 3410 && Count("Soybean") == 6 && Player.Items.CountId(Wheat) == 2,
            "SeedShop purchases or payment differ.");
        this.Pass("SeedShop supplied 6 soybeans and 2 wheat for 410g");
        Buy("Saloon", ObjectId("ChineseToon"), 1);
        Buy("Saloon", ObjectId("ToonTofu"), 1, true);
        Require(Player.Money == this.StartingMoney - 4510 && Count("ChineseToon") == 1 && Player.knowsRecipe(RecipeId("ToonTofu")),
            "Saloon purchases or payment differ.");
        this.Pass("Saloon supplied 1 toon and taught its dish for 1100g");

        this.Bean = this.Place(NativeRouteActions.Craft("BeanWorkshop"));
        this.Crock = this.Place(NativeRouteActions.Craft("FermentationCrock"));
        Require(State(this.Bean).InstanceId != State(this.Crock).InstanceId
            && new[] { "(O)388", "(O)390", "(O)334", MachineId("BeanWorkshop"), MachineId("FermentationCrock") }
                .All(id => Player.Items.CountId(id) == 0), "Crafting or placement did not consume exactly the materials and devices.");
        this.Pass("crafted and placed both purchased devices through the game APIs");
        (this.Stove, this.StoveStanding) = this.FindStove();
        this.RequestKitchen(KitchenStep.Preflight);

        void Buy(string shop, string id, int quantity, bool recipe = false) =>
            NativeRouteActions.Buy(this.Helper, this.Monitor, shop, id, quantity, recipe);
    }

    private void StartBatches()
    {
        this.LastClock = this.StartClock = Game1.timeOfDay;
        this.StartMinutes = MachineService.Now();
        this.ClickMachine(this.Bean);
        this.ClickMachine(this.Crock);
        Require(State(this.Bean).Batch is { } bean && bean.StartedAt == this.StartMinutes && bean.ReadyAt == this.StartMinutes + 120
            && State(this.Crock).Batch is { } crock && crock.StartedAt == this.StartMinutes && crock.ReadyAt == this.StartMinutes + 240
            && Count("Soybean") == 0 && Player.Items.CountId(Wheat) == 0, "Machine menus did not start both batches with the purchased ingredients.");
        this.BeanRaw = this.Bean.modData[MachineService.StateKey];
        this.CrockRaw = this.Crock.modData[MachineService.StateKey];
        this.Waiting = true;
        this.Pass("opened both machine menus and started the purchased batches together");
    }

    private SObject Place(SObject carried)
    {
        var tiles = from y in Enumerable.Range(2, this.House.Map.Layers[0].LayerHeight - 4)
                    from x in Enumerable.Range(2, this.House.Map.Layers[0].LayerWidth - 4)
                    let tile = new Vector2(x, y)
                    where !this.House.isTileOnWall(x, y) && this.CanStand(tile + Below)
                          && this.House.CanItemBePlacedHere(tile)
                    select tile;
        int slot = Player.Items.ToList().FindIndex(item => ReferenceEquals(item, carried));
        Require(slot is >= 0 and < 12, "Crafted device isn't in a usable toolbar slot.");
        Player.CurrentToolIndex = slot;
        foreach (var tile in tiles)
        {
            Player.setTileLocation(tile + Below);
            int x = (int)tile.X * 64 + 32, y = (int)tile.Y * 64 + 32;
            if (!Utility.playerCanPlaceItemHere(this.House, carried, x, y, Player)) continue;
            Require(Utility.tryToPlaceItem(this.House, carried, x, y)
                && Player.Items.CountId(carried.QualifiedItemId) == 0, "Native placement failed or did not consume the device.");
            return this.House.objects[tile];
        }
        throw new InvalidOperationException("No suitable farmhouse tile for the crafted device.");
    }

    public void OnTimeChanged(int oldTime, int newTime)
    {
        if (!this.Waiting) return;
        this.Guard(() =>
        {
            Require(this.MatchesWorld() && Game1.activeClickableMenu is null, "Clock observed outside the dedicated route.");
            int elapsed = ++this.ClockTicks * 10;
            Require(oldTime == this.LastClock && newTime == Game1.timeOfDay && MachineService.Now() == this.StartMinutes + elapsed,
                "Clock did not advance by a native ten-minute step.");
            this.LastClock = newTime;
            if (elapsed <= 120) Observe(this.Bean, this.BeanRaw, 120);
            Observe(this.Crock, this.CrockRaw, 240);
            if (elapsed == 120)
            {
                this.ClickMachine(this.Bean);
                Require(State(this.Bean).Batch is null && Count("Tofu") == 2 && Count("SoySauce") == 0, "Tofu collection differs.");
                this.Pass("collected 2 tofu after 12 native clock changes");
            }
            if (elapsed != 240) return;
            this.Waiting = false;
            this.ClickMachine(this.Crock);
            Require(State(this.Crock).Batch is null && Count("SoySauce") == 4 && Count("Tofu") == 2, "Soy sauce collection differs.");
            this.Pass("collected 4 soy sauce after 24 native clock changes");
            this.RequestKitchen(KitchenStep.Cook);

            void Observe(SObject machine, string raw, int due)
            {
                Require(machine.modData[MachineService.StateKey] == raw
                    && State(machine).PhaseAt(MachineService.Now()) == (elapsed < due ? MachinePhase.Processing : MachinePhase.Ready),
                    "Pending batch changed or became ready at the wrong minute.");
            }
        });
    }

    private void ClickMachine(SObject machine)
    {
        Player.setTileLocation(machine.TileLocation + Below);
        Player.faceDirection(0);
        Require(machine.checkForAction(Player) && Game1.activeClickableMenu is MachineMenu,
            "Placed device did not open its production menu.");
        var menu = (MachineMenu)Game1.activeClickableMenu;
        Point action = this.Helper.Reflection.GetMethod(menu, "ActionBounds").Invoke<Rectangle>(0).Center;
        menu.receiveLeftClick(action.X, action.Y);
        menu.exitThisMenu(playSound: false);
    }

    private void PollKitchen()
    {
        // 原版先请求冰箱锁，成功回调才生成烹饪页。等待实际菜单，避免将同帧为空误判为拒绝。
        if (Game1.activeClickableMenu is not CraftingPage { cooking: true } page)
        {
            Require(Game1.activeClickableMenu is null && this.Watch.Elapsed - this.KitchenRequestedAt < TimeSpan.FromSeconds(10),
                "Native kitchen did not open the cooking page.");
            return;
        }
        var step = this.Kitchen;
        this.Kitchen = KitchenStep.None;
        int index = page.pagesOfCraftingRecipes.FindIndex(recipes => recipes.Values.Any(recipe => recipe.name == RecipeId("ToonTofu")));
        Require(index >= 0, "Purchased dish is absent from the actual kitchen page.");
        if (step == KitchenStep.Preflight)
        {
            page.exitThisMenu(playSound: false);
            this.Monitor.Log("AutomatedRoute preflight: native stove opened the learned recipe.", LogLevel.Info);
            this.StartBatches();
            return;
        }
        this.CookAndEat(page, index);
    }

    private void CookAndEat(CraftingPage page, int index)
    {
        page.currentCraftingPage = index;
        int CookedCount() => Player.recipesCooked.TryGetValue(Id + "_ToonTofu", out int count) ? count : 0;
        int beforeCooked = CookedCount();
        Point recipe = page.pagesOfCraftingRecipes[index].Single(pair => pair.Value.name == RecipeId("ToonTofu")).Key.bounds.Center;
        page.receiveLeftClick(recipe.X, recipe.Y);
        // 原版料理页可直接将成品入包。只有菜单还持有成品时才点击空槽。
        if (page.heldItem is { } held)
        {
            int free = Player.Items.ToList().FindIndex(item => item is null);
            Require(free >= 0 && held.QualifiedItemId == ObjectId("ToonTofu") && held.Stack == 1, "Unexpected cooking output in the menu.");
            Point empty = page.inventory.inventory[free].bounds.Center;
            page.receiveLeftClick(empty.X, empty.Y);
        }
        Require(page.heldItem is null && CookedCount() == beforeCooked + 1
            && Count("ToonTofu") == 1 && Count("ChineseToon") == 0 && Count("Tofu") == 1,
            "Cooking did not consume the purchased toon and collected tofu.");
        int slot = Player.Items.ToList().FindIndex(item => item?.QualifiedItemId == ObjectId("ToonTofu"));
        Require(slot is >= 0 and < 12, "Cooked dish isn't in a usable toolbar slot.");
        page.exitThisMenu(playSound: false);
        this.Pass("cooked one dish at the farmhouse stove using the same tofu and toon");
        Player.CurrentToolIndex = slot;
        // 原生确认回调可能读取上次抓取的物品；无键鼠输入时显式指向本次实际放进快捷栏的料理。
        Require(Player.ActiveObject is { } dish && dish.QualifiedItemId == ObjectId("ToonTofu"), "Cooked dish not selected.");
        Player.mostRecentlyGrabbedItem = Player.ActiveObject;
        Player.eatHeldObject();
        Require(Player.isEating && Count("ToonTofu") == 0, "Native eating did not start or consume the dish.");
        this.Eating = true;
    }

    private (Vector2 Tile, Vector2 Standing) FindStove()
    {
        Point kitchenStanding = this.House.getKitchenStandingSpot();
        Vector2 preferred = new(kitchenStanding.X, kitchenStanding.Y);
        var bounds = this.House.Map.Layers[0];
        var actionTiles = (from y in Enumerable.Range(0, bounds.LayerHeight)
                           from x in Enumerable.Range(0, bounds.LayerWidth)
                           let action = this.House.doesTileHaveProperty(x, y, "Action", "Buildings")
                           where string.Equals(action?.Trim(), "kitchen", StringComparison.OrdinalIgnoreCase)
                           select new Vector2(x, y)).OrderBy(tile => Vector2.DistanceSquared(tile, preferred - Below)).ToArray();
        foreach (var tile in actionTiles)
        foreach (var offset in new[] { Below, new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1) })
        {
            var standing = tile + offset;
            if (this.House.isTileOnMap(standing) && !this.House.isTileOnWall((int)standing.X, (int)standing.Y)
                && this.House.CanItemBePlacedHere(standing, collisionMask: CollisionMask.All & ~CollisionMask.Farmers))
                return (tile, standing);
        }
        throw new InvalidOperationException($"No clear approach to the farmhouse stove; {actionTiles.Length} native kitchen tiles.");
    }

    private void RequestKitchen(KitchenStep step)
    {
        Player.setTileLocation(this.StoveStanding);
        var direction = this.Stove - this.StoveStanding;
        Player.faceDirection(direction.X > 0 ? 1 : direction.X < 0 ? 3 : direction.Y > 0 ? 2 : 0);
        var tile = new xTile.Dimensions.Location((int)this.Stove.X, (int)this.Stove.Y);
        string action = this.House.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings")
            ?? throw new InvalidOperationException("Native stove action disappeared.");
        // 站位由夹具布置，直接把实际地图格的 Action 交给原版处理器，免去对物理输入和鼠标命中层的依赖。
        Require(this.House.performAction(action, Player, tile), $"Native stove action at {tile} was rejected.");
        this.Kitchen = step;
        this.KitchenRequestedAt = this.Watch.Elapsed;
    }

    private bool CanStand(Vector2 tile) => !this.House.isTileOnWall((int)tile.X, (int)tile.Y)
        && this.House.getFloors().Any(floor => floor.Contains((int)tile.X, (int)tile.Y))
        && this.House.CanItemBePlacedHere(tile, collisionMask: CollisionMask.All & ~CollisionMask.Farmers);
    private static MachineState State(SObject machine) => MachineService.TryRead(machine, out var state)
        ? state! : throw new InvalidOperationException("Placed route device has invalid state.");
    private static string ObjectId(string key) => "(O)" + Id + "_" + key;
    private static string MachineId(string key) => "(BC)" + Id + "_" + key;
    private static string RecipeId(string key) => Id + "_Recipe_" + key;
    private static int Count(string key) => Player.Items.CountId(ObjectId(key));

    public void OnSaving()
    {
        if (!this.MatchesWorld() || this.Finished) return;
        this.SavingObserved = true;
        this.Finish("Saving observed during the automated route.");
    }

    public void OnReturnedToTitle()
    {
        if (this.Watch.IsRunning && !this.Finished) this.Finish("Returned to title before route completion.");
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception error) { this.Finish(error.ToString()); }
    }

    private void Pass(string check)
    {
        this.Passed.Add(check);
        this.Monitor.Log("AutomatedRoute PASS: " + check, LogLevel.Info);
        this.Report(false, null);
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        this.Waiting = this.Eating = false;
        if (this.ChangedOptions is not null) this.ChangedOptions.pauseWhenOutOfFocus = this.OriginalPauseSetting;
        this.Report(failure is null, failure);
        this.Monitor.Log(failure ?? "AutomatedRoute completed.", failure is null ? LogLevel.Info : LogLevel.Error);
    }

    private void Report(bool completed, string? failure) => this.Helper.Data.WriteJsonFile("automated-route-result.json", new
    {
        SchemaVersion = 1, ProcessId = Environment.ProcessId, FixtureId = this.Settings.SaveFixtureId,
        Completed = completed, Failure = failure, this.SavingObserved, this.Passed, this.StartClock,
        this.LastClock, this.ClockTicks, ElapsedMilliseconds = this.Watch.ElapsedMilliseconds,
        Money = this.MatchesWorld() ? Player.Money : -1,
        Tofu = this.MatchesWorld() ? Count("Tofu") : 0,
        SoySauce = this.MatchesWorld() ? Count("SoySauce") : 0,
        Dish = this.MatchesWorld() ? Count("ToonTofu") : 0
    });

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
