using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// 仅为人工/桌面输入准备可复跑的 UI 验证环境，不调用购买、制作、开工、领取或食用方法。
/// 只加载显式 ID 的 XYSP1UI 副本；所有修改留在内存，不保存，也不承诺恢复本次内存。
/// F8 或中键在操作者关闭当前菜单后核对结果并准备下一步，未满足断言时保留现场供排查。
/// </summary>
internal sealed class InteractionScenario
{
    private const string Id = MachineService.Prefix;
    private const string FarmName = "XYSP1UI";
    // BUG-2026-008：原生皮埃尔商店接受料理；商品出售列表不能证明商店愿意收购。
    // 与 Memory 付款回归共用此入口，Data/Shops 接受规则变更时明确失败，不修改原版收购范围。
    internal const string SaleShopId = "SeedShop";
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Config;
    private readonly List<string> Passed = new();
    private int LoadDelay = -1;
    private bool Pending, Started;
    private int Step;
    private int FixtureTime = 600;
    private SObject Bean = null!, Crock = null!;
    private int ExpectedSale;
    private static Farmer Player => Game1.player;
    private static string ObjectId(string key) => "(O)" + Id + "_" + key;
    private static string MachineId(string key) => "(BC)" + Id + "_" + key;
    private static string RecipeId(string key) => Id + "_Recipe_" + key;
    private string SaveName => FarmName + "_" + this.Config.SaveFixtureId;
    private static int Count(string key) => Player.Items.CountId(ObjectId(key));
    private static readonly string[] Steps =
    {
        "皮埃尔：购买黄豆 9、小麦 2，放入背包，关闭菜单后按 F8",
        "罗宾：购买豆坊和发酵缸图纸，关闭后按 F8",
        "格斯：购买香椿 3、香椿拌豆腐菜谱，放入背包，关闭后按 F8",
        "制作：制作豆坊与发酵缸各一台，放入背包，关闭后按 F8",
        "豆坊第一批：点击开始加工，关闭后按 F8",
        "豆坊第一批：领取豆腐，关闭后按 F8",
        "豆坊第二批：点击开始加工，关闭后按 F8",
        "豆坊第二批：领取豆腐，关闭后按 F8",
        "发酵缸：点击开始加工，关闭后按 F8",
        "发酵缸：领取酱油，关闭后按 F8",
        "烹饪：制作香椿拌豆腐 3 份，放入背包，关闭后按 F8",
        "食用：选择香椿拌豆腐，右键并确认吃一份，动画结束后按 F8",
        "售出：在皮埃尔商店售出剩余两份香椿拌豆腐，关闭后按 F8",
        "完成；退出游戏，不保存测试副本"
    };

    public InteractionScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        this.Helper = helper; this.Monitor = monitor; this.Config = config;
        Require(config.FarmName == FarmName && config.SaveFixtureId != 0, "UI fixture requires XYSP1UI and an explicit distinct ID.");
    }
    public void QueueLoad() => this.LoadDelay = 180;
    public void OnLoaded() => this.Pending = true;
    public void Tick()
    {
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady, "Refuse to replace an active world.");
                Require(File.Exists(Path.Combine(Constants.SavesPath, this.SaveName, this.SaveName)), "UI fixture is not installed.");
                Game1.activeClickableMenu = null;
                SaveGame.Load(this.SaveName);
            }
            if (this.Pending && Context.IsWorldReady)
            {
                this.Pending = false; this.VerifyIdentity(); this.Setup(); this.Started = true; this.Prepare();
            }
            // 防止耗时界面检查推进到昏倒/自动保存；加工等待由准备步骤显式推进测试时钟。
            if (this.Started && Context.IsWorldReady) Game1.timeOfDay = this.FixtureTime;
        }
        catch (Exception error) { this.Pending = false; this.LoadDelay = -1; this.Fail(error); }
    }
    private void VerifyIdentity() => Require(Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && Player.farmName.Value == FarmName && Game1.uniqueIDForThisGame == this.Config.SaveFixtureId
        && Game1.GetSaveGameName() == FarmName, "Refuse to mutate any save except the dedicated UI fixture.");
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static MachineState State(SObject machine)
    {
        Require(MachineService.TryRead(machine, out var state), "Unreadable placed state"); return state!;
    }
    private void Setup()
    {
        Require(!Player.knowsRecipe(RecipeId("BeanWorkshop")) && !Player.knowsRecipe(RecipeId("ToonTofu")), "Use a fresh fixture with unlearned recipes.");
        var stock = new Item[Player.MaxItems];
        stock[0] = new StardewValley.Tools.Pickaxe();
        stock[1] = ItemRegistry.Create("(O)388", 80); stock[2] = ItemRegistry.Create("(O)390", 60); stock[3] = ItemRegistry.Create("(O)334");
        Player.Items.OverwriteWith(stock); Player.Money = 20000;
        Game1.year = 1; Game1.season = Season.Spring; Game1.dayOfMonth = 1;
        Game1.netWorldState.Value.UpdateFromGame1();
    }
    public void Advance()
    {
        if (!this.Started || this.Step == 13) return;
        try
        {
            this.VerifyIdentity(); Require(Game1.activeClickableMenu is null && !Player.isEating, "Close the menu / finish the eating animation before F8.");
            if (this.Step is >= 4 and <= 9)
            {
                var currentMachine = this.Step < 8 ? this.Bean : this.Crock;
                Require(Player.currentLocation.objects.TryGetValue(currentMachine.TileLocation, out var placed)
                    && ReferenceEquals(placed, currentMachine), "Fixture machine moved; rerun on a fresh in-memory fixture.");
            }
            switch (this.Step)
            {
                case 0: Require(Count("Soybean") == 9 && Player.Items.CountId("(O)262") == 2 && Player.Money == 19410, "Wrong Pierre purchases or total"); break;
                case 1: Require(Player.knowsRecipe(RecipeId("BeanWorkshop")) && Player.knowsRecipe(RecipeId("FermentationCrock")) && Player.Money == 16410, "Blueprint learning or total failed"); break;
                case 2: Require(Count("ChineseToon") == 3 && Player.knowsRecipe(RecipeId("ToonTofu")) && Player.Money == 15110, "Wrong Gus purchases or total"); break;
                case 3:
                    Require(Player.Items.CountId(MachineId("BeanWorkshop")) == 1 && Player.Items.CountId(MachineId("FermentationCrock")) == 1
                        && Player.Items.CountId("(O)388") == 0 && Player.Items.CountId("(O)390") == 0 && Player.Items.CountId("(O)334") == 0, "Crafting costs or outputs wrong"); break;
                case 4: Require(State(this.Bean).Batch is not null && Count("Soybean") == 6, "First bean start failed"); break;
                case 5: Require(State(this.Bean).CanMove && Count("Tofu") == 2, "First bean collection failed"); break;
                case 6: Require(State(this.Bean).Batch is not null && Count("Soybean") == 3, "Second bean start failed"); break;
                case 7: Require(State(this.Bean).CanMove && Count("Tofu") == 4, "Second bean collection failed"); break;
                case 8: Require(State(this.Crock).Batch is not null && Count("Soybean") == 0 && Player.Items.CountId("(O)262") == 0, "Sauce start failed"); break;
                case 9: Require(State(this.Crock).CanMove && Count("SoySauce") == 4, "Sauce collection failed"); break;
                case 10: Require(Count("ToonTofu") == 3 && Count("ChineseToon") == 0 && Count("Tofu") == 1, "Cooking quantities wrong"); break;
                case 11:
                    var dish = ItemRegistry.Create<SObject>(ObjectId("ToonTofu"));
                    Require(Count("ToonTofu") == 2 && Player.Stamina == Math.Min(Player.MaxStamina, 20 + dish.staminaRecoveredOnConsumption())
                        && Player.health == Math.Min(Player.maxHealth, 20 + dish.healthRecoveredOnConsumption()), "Food consumption/recovery wrong"); break;
                case 12: Require(Count("ToonTofu") == 0 && Player.Money == 15110 + this.ExpectedSale && Count("SoySauce") == 4 && Count("Tofu") == 1, "Sale money or unrelated inventory changed"); break;
            }
            this.Passed.Add(Steps[this.Step]); this.Monitor.Log("PASS UI " + this.Step + ": " + Steps[this.Step], LogLevel.Info);
            this.Step++; this.Prepare();
        }
        catch (Exception error) { this.Fail(error); }
    }
    private void Shop(string id)
    {
        var data = this.Helper.GameContent.Load<Dictionary<string, ShopData>>("Data/Shops")[id];
        Game1.activeClickableMenu = new ShopMenu(id, data, data.Owners.First(), playOpenSound: false);
    }
    private static void CraftMenu(bool cooking)
    {
        int w = 800 + IClickableMenu.borderWidth * 2, h = 600 + IClickableMenu.borderWidth * 2;
        var p = Utility.getTopLeftPositionForCenteringOnScreen(w, h);
        Game1.activeClickableMenu = new CraftingPage((int)p.X, (int)p.Y, w, h, cooking, standaloneMenu: true);
    }
    private void PlaceCraftedMachines()
    {
        var location = Player.currentLocation;
        var tiles = (from y in Enumerable.Range(2, location.Map.Layers[0].LayerHeight - 3)
                     from x in Enumerable.Range(2, location.Map.Layers[0].LayerWidth - 3)
                     where location.CanItemBePlacedHere(new Vector2(x, y)) && location.CanItemBePlacedHere(new Vector2(x, y + 1))
                     select new Vector2(x, y)).Take(2).ToArray();
        Require(tiles.Length == 2, "Need two fixture floor tiles");
        SObject Place(string key, Vector2 tile)
        {
            var carried = (SObject)Player.Items.Single(i => i?.QualifiedItemId == MachineId(key));
            Require(carried.placementAction(location, (int)tile.X * 64, (int)tile.Y * 64, Player), "Fixture placement failed");
            Player.removeItemFromInventory(carried); return location.objects[tile];
        }
        this.Bean = Place("BeanWorkshop", tiles[0]); this.Crock = Place("FermentationCrock", tiles[1]);
    }
    private void Prepare()
    {
        switch (this.Step)
        {
            case 0: this.Shop("SeedShop"); break;
            case 1: this.Shop("Carpenter"); break;
            case 2: this.Shop("Saloon"); break;
            case 3: CraftMenu(false); break;
            case >= 4 and <= 9:
                if (this.Step == 4) this.PlaceCraftedMachines();
                var machine = this.Step < 8 ? this.Bean : this.Crock;
                // 跳过真实等候，仍由生产菜单执行开工与领取，夹具不调用 Execute。
                if (this.Step % 2 == 1) this.FixtureTime += 400;
                Game1.timeOfDay = this.FixtureTime;
                Player.Position = (machine.TileLocation + new Vector2(0, 1)) * 64;
                // 选择空槽，避免关闭按钮后的额外点击在世界中挥镐，干扰本轮加工验证。
                Player.CurrentToolIndex = Player.Items.ToList().FindIndex(i => i is null);
                Player.faceDirection(0); machine.checkForAction(Player); break;
            case 10: CraftMenu(true); break;
            case 11:
                Player.Stamina = 20; Player.health = 20;
                Player.CurrentToolIndex = Player.Items.ToList().FindIndex(i => i?.QualifiedItemId == ObjectId("ToonTofu"));
                Player.faceDirection(2); break;
            case 12:
                this.Shop(SaleShopId);
                Require(((ShopMenu)Game1.activeClickableMenu).highlightItemToSell(ItemRegistry.Create<SObject>(ObjectId("ToonTofu"))),
                    "Interaction sale shop rejects cooked food: " + SaleShopId);
                float saleMultiplier = this.Helper.Reflection.GetField<float>(Game1.activeClickableMenu, "sellPercentage").GetValue();
                this.ExpectedSale = 2 * (int)(ItemRegistry.Create<SObject>(ObjectId("ToonTofu")).sellToStorePrice(-1L) * saleMultiplier); break;
        }
        Game1.addHUDMessage(new HUDMessage(Steps[this.Step], HUDMessage.newQuest_type));
        this.Report(null);
    }
    private void Fail(Exception error)
    {
        this.Monitor.Log(error.Message, LogLevel.Warn); this.Report(error.ToString());
        Game1.showRedMessage(error.Message);
    }
    private void Report(string? failure) => this.Helper.Data.WriteJsonFile("interaction-result.json", new
    {
        Completed = this.Step == 13, Step = this.Step, Instruction = Steps[this.Step], Passed = this.Passed,
        Failure = failure, Money = Context.IsWorldReady ? Player.Money : -1, Saved = false,
        Inventory = Context.IsWorldReady ? Player.Items.Where(i => i is not null).Select(i => new { i.QualifiedItemId, i.Stack }).ToArray() : null
    });
}
