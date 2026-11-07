using System.Text.Json;
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
/// P1 跨保存集成测试。只允许 XYSP1Save 独立副本和显式配置的 game ID；会修改并保存此副本。
/// 逐帧驱动游戏 Save 枚举器，完成后退出标题再用游戏 Load 读取磁盘；不把 JSON 克隆当读档。
/// </summary>
internal sealed class PersistenceScenario
{
    private const string Id = MachineService.Prefix;
    private const string MarkerKey = Id + "/LocalHarnessCheckpoint";
    private const string FarmName = "XYSP1Save";
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Config;
    private IEnumerator<int>? Save;
    private int LoadDelay = -1;
    private bool Loaded, Stopped;
    private Checkpoint State = new();
    private MachineService Service => (MachineService)(this.Helper.ModRegistry.GetApi(Id)
        ?? throw new InvalidOperationException("Machine API unavailable."));
    private static Farmer Player => Game1.player;
    private static GameLocation House => Game1.getLocationFromName("FarmHouse");
    private string SaveName => FarmName + "_" + this.Config.SaveFixtureId;

    public PersistenceScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        this.Helper = helper; this.Monitor = monitor; this.Config = config;
        if (config.FarmName != FarmName || config.SaveFixtureId == 0)
            throw new InvalidOperationException("Persistence mode requires XYSP1Save and an explicit distinct game ID.");
    }
    public void QueueLoad() => this.LoadDelay = 180;
    public void OnLoaded() => this.Loaded = true;
    public void Tick()
    {
        if (this.Stopped) return;
        try
        {
            if (this.LoadDelay >= 0)
            {
                if (--this.LoadDelay != 0) return;
                this.LoadDelay = -1;
                Require(!Context.IsWorldReady, "Refuse to replace an active game.");
                Require(File.Exists(Path.Combine(Constants.SavesPath, this.SaveName, this.SaveName)), "Dedicated fixture not installed.");
                Game1.activeClickableMenu = null;
                SaveGame.Load(this.SaveName);
                return;
            }
            if (this.Save is not null)
            {
                if (this.Save.MoveNext()) return;
                this.Save.Dispose(); this.Save = null;
                this.Reload();
                return;
            }
            if (!this.Loaded || !Context.IsWorldReady) return;
            this.Loaded = false;
            this.VerifyIdentity();
            this.State = Player.modData.TryGetValue(MarkerKey, out string raw)
                ? JsonSerializer.Deserialize<Checkpoint>(raw) ?? throw new InvalidOperationException("Bad checkpoint") : new();
            Game1.activeClickableMenu = new DialogueBox("P1 专用副本验证中，请勿操作。");
            switch (this.State.Stage)
            {
                case 0: this.Setup(); break;
                case 1: this.AfterFirstLoad(); break;
                case 2: this.AfterSeasonLoad(); break;
                case 3: this.AfterCollectionLoad(); break;
                default: throw new InvalidOperationException("Unknown checkpoint stage");
            }
        }
        catch (Exception error)
        {
            this.Stopped = true;
            this.Monitor.Log(error.ToString(), LogLevel.Error);
            this.Report(false, error.ToString());
            Game1.activeClickableMenu = new DialogueBox("P1 测试失败：" + error.Message);
        }
    }
    private void VerifyIdentity() => Require(Context.IsMainPlayer && !Context.IsMultiplayer
        && Player.farmName.Value == FarmName && Game1.uniqueIDForThisGame == this.Config.SaveFixtureId
        && Game1.GetSaveGameName() == FarmName, "Refuse to modify any farm except the dedicated save fixture.");
    private void Test(string name, Action action)
    {
        action(); this.State.Passed.Add(name); this.Monitor.Log("PASS " + name, LogLevel.Info);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static string ObjectId(string key) => "(O)" + Id + "_" + key;
    private static void Clock(int day, int hhmm)
    {
        Game1.year = 1; Game1.season = Season.Spring; Game1.dayOfMonth = day; Game1.timeOfDay = hhmm;
        Game1.netWorldState.Value.UpdateFromGame1();
    }
    private static void Stock(params Item[] items)
    {
        var slots = new Item[Player.MaxItems]; Array.Copy(items, slots, items.Length); Player.Items.OverwriteWith(slots);
    }
    private static void Add(Item item) => Require(Player.addItemToInventory(item) is null, "Fixture inventory unexpectedly full");
    private static string Inventory() => JsonSerializer.Serialize(Player.Items.Select(i => i is null ? null : new { i.QualifiedItemId, i.Stack, i.Quality }));
    private SObject Machine(int index) => House.objects[new Vector2(this.State.Tiles[index][0], this.State.Tiles[index][1])];
    private static MachineState Read(SObject machine)
    {
        Require(MachineService.TryRead(machine, out var state), "Machine state unreadable"); return state!;
    }
    private static Request NewRequest(SObject machine)
    {
        var state = Read(machine); return new(Guid.NewGuid(), state.InstanceId, state.Revision, Player.UniqueMultiplayerID);
    }
    private OperationResult Operate(SObject machine, string? recipe, Request? request = null)
    {
        Player.currentLocation = House; Game1.currentLocation = House;
        Player.Position = (machine.TileLocation + new Vector2(0, 1)) * 64;
        return this.Service.Execute(machine, Player, request ?? NewRequest(machine), recipe is null ? null : Id + "_Recipe_" + recipe);
    }
    private static SObject Place(SObject carried, Vector2 tile)
    {
        Require(House.CanItemBePlacedHere(tile), "Fixture placement tile isn't clear");
        Require(carried.placementAction(House, (int)tile.X * 64, (int)tile.Y * 64, Player), "placementAction rejected");
        Require(House.objects.TryGetValue(tile, out var placed) && placed.QualifiedItemId == carried.QualifiedItemId, "No placed object");
        Player.removeItemFromInventory(carried);
        return placed!;
    }
    private void Buy(string shopId, string qualifiedId, int quantity, bool recipe = false)
    {
        var data = this.Helper.GameContent.Load<Dictionary<string, ShopData>>("Data/Shops")[shopId];
        var shop = new ShopMenu(shopId, data, data.Owners.First(), playOpenSound: false);
        var item = shop.forSale.Single(i => i.QualifiedItemId == qualifiedId && i.IsRecipe == recipe);
        int price = shop.itemPriceAndStock[item].Price;
        this.Monitor.Log($"Purchase {qualifiedId}: {quantity} at {price}g.", LogLevel.Info);
        int money = Player.Money;
        var purchase = typeof(ShopMenu).GetMethod("tryToPurchaseItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        purchase.Invoke(shop, new object?[] { item, null, quantity, 0, 0 });
        Require(Player.Money == money - price * quantity, "Shop didn't debit expected amount");
        if (recipe) Require(Player.knowsRecipe(((Item)item).Name), "Purchased recipe wasn't learned");
        else
        {
            Require(shop.heldItem is Item held && held.QualifiedItemId == qualifiedId && held.Stack == quantity, "Wrong purchased stack");
            Add((Item)shop.heldItem); shop.heldItem = null;
        }
    }
    private static SObject Craft(string key, bool cooking = false)
    {
        var recipe = new CraftingRecipe(Id + "_Recipe_" + key, cooking);
        Require(Player.knowsRecipe(recipe.name) && recipe.doesFarmerHaveIngredientsInInventory(), "Recipe unlearned or missing ingredients");
        recipe.consumeIngredients(null);
        var output = (SObject)recipe.createItem(); Add(output); return output;
    }
    private void Setup()
    {
        Require(!House.objects.Values.Any(i => MachineService.TryGetKind(i, out _)), "Fixture already contains untracked machines");
        Clock(28, 600);
        this.State.Tiles = (from y in Enumerable.Range(2, House.Map.Layers[0].LayerHeight - 3)
                            from x in Enumerable.Range(2, House.Map.Layers[0].LayerWidth - 3)
                            where House.CanItemBePlacedHere(new Vector2(x, y)) select new[] { x, y }).Take(4).ToList();
        Require(this.State.Tiles.Count == 4, "Need four free indoor fixture tiles");
        Vector2 Tile(int i) => new(this.State.Tiles[i][0], this.State.Tiles[i][1]);
        Stock(ItemRegistry.Create("(O)388", 80), ItemRegistry.Create("(O)390", 60), ItemRegistry.Create("(O)334", 1));
        Player.Money = 20000;
        this.Test("native shop purchase debits and learns three recipes", () =>
        {
            this.Buy("SeedShop", ObjectId("Soybean"), 6); this.Buy("SeedShop", "(O)262", 2);
            this.Buy("Saloon", ObjectId("ChineseToon"), 1);
            this.Buy("Carpenter", "(BC)" + Id + "_BeanWorkshop", 1, true);
            this.Buy("Carpenter", "(BC)" + Id + "_FermentationCrock", 1, true);
            this.Buy("Saloon", ObjectId("ToonTofu"), 1, true);
            Require(Player.Money == 15490, $"Expected 15490g after prototype purchases, got {Player.Money}g");
        });
        this.Test("native crafting consumes materials and placement creates fresh identity", () =>
        {
            var bean = Place(Craft("BeanWorkshop"), Tile(0));
            Place(Craft("FermentationCrock"), Tile(1));
            Require(Player.Items.CountId("(O)388") == 0 && Player.Items.CountId("(O)390") == 0 && Player.Items.CountId("(O)334") == 0, "Wrong blueprint costs");
            Guid firstId = Read(bean).InstanceId;
            var pickup = House.removeObject(Tile(0), false);
            Require(pickup is not null, "Idle pickup rejected");
            // Remove-object path returns the carried instance; placing it must issue a new ID.
            Player.Position = (Tile(0) + new Vector2(0, 1)) * 64;
            var replaced = Place(pickup!, Tile(0));
            Require(Read(replaced).InstanceId != firstId, "Placement reused old identity");
        });
        this.Test("purchased ingredients reach tofu dish and soy sauce through native recipes", () =>
        {
            Require(this.Operate(this.Machine(0), "Tofu").Applied, "Tofu start failed");
            Require(this.Operate(this.Machine(1), "SoySauce").Applied, "Sauce start failed");
            Game1.timeOfDay = 1000;
            Require(this.Operate(this.Machine(0), null).Applied && this.Operate(this.Machine(1), null).Applied, "Route collection failed");
            Craft("ToonTofu", true);
            Require(Player.Items.CountId(ObjectId("Tofu")) == 1 && Player.Items.CountId(ObjectId("ToonTofu")) == 1
                && Player.Items.CountId(ObjectId("SoySauce")) == 4 && Player.Items.CountId(ObjectId("Soybean")) == 0
                && Player.Items.CountId(ObjectId("ChineseToon")) == 0 && Player.Items.CountId("(O)262") == 0, "Route conservation failed");
        });
        // 用独立材料准备保存矩阵：A/B 加工中，C 待领取，D 已领取；此处不宣称原料是自然取得。
        Stock(ItemRegistry.Create(ObjectId("Soybean"), 12), ItemRegistry.Create("(O)262", 2));
        Place(ItemRegistry.Create<SObject>("(BC)" + Id + "_BeanWorkshop"), Tile(2));
        Place(ItemRegistry.Create<SObject>("(BC)" + Id + "_BeanWorkshop"), Tile(3));
        Clock(27, 2200);
        Require(this.Operate(this.Machine(2), "Tofu").Applied && this.Operate(this.Machine(3), "Tofu").Applied, "Ready fixtures failed");
        Clock(28, 600);
        Require(this.Operate(this.Machine(3), null).Applied, "Idle fixture collection failed");
        Require(this.Operate(this.Machine(0), "Tofu").Applied && this.Operate(this.Machine(1), "SoySauce").Applied, "Processing fixtures failed");
        this.SaveCheckpoint(1);
    }
    private void VerifySnapshot()
    {
        Require(this.State.Inventory == Inventory(), "Inventory changed during disk save/load");
        for (int i = 0; i < 4; i++) Require(this.Machine(i).modData[MachineService.StateKey] == this.State.Raw[i], "Machine JSON changed across save/load");
    }
    private void AfterFirstLoad()
    {
        this.Test("disk reload preserves processing ready and collected states", () =>
        {
            this.VerifySnapshot();
            Require(Read(this.Machine(0)).PhaseAt(MachineService.Now()) == MachinePhase.Processing
                && Read(this.Machine(1)).PhaseAt(MachineService.Now()) == MachinePhase.Processing
                && Read(this.Machine(2)).PhaseAt(MachineService.Now()) == MachinePhase.Ready
                && Read(this.Machine(3)).CanMove, "Wrong loaded phases");
            Require(this.Operate(this.Machine(2), null).Applied, "Ready collection after reload failed");
            var idle = Read(this.Machine(3));
            Require(!this.Operate(this.Machine(3), null, new(idle.LastRequestId!.Value, idle.InstanceId, idle.Revision - 1, Player.UniqueMultiplayerID)).Applied, "Replay after load succeeded");
        });
        this.SetCheckpoint(2);
        // 通过游戏原生日切换/季节更新及其夜间保存，不能用修改日期字段代替这项验收。
        this.Report(false, null);
        Game1.activeClickableMenu = null;
        Game1.timeOfDay = 2300;
        Game1.newDayAfterFade(this.Reload);
    }
    private void AfterSeasonLoad()
    {
        this.Test("native overnight crosses spring to summer and reload retains output", () =>
        {
            Require(Game1.season == Season.Summer && Game1.dayOfMonth == 1, "Season rollover not persisted");
            this.VerifySnapshot();
            Require(Read(this.Machine(0)).PhaseAt(MachineService.Now()) == MachinePhase.Ready
                && Read(this.Machine(1)).PhaseAt(MachineService.Now()) == MachinePhase.Ready, "Overnight batch not ready");
            Require(this.Operate(this.Machine(0), null).Applied && this.Operate(this.Machine(1), null).Applied, "Post-night collection failed");
            Require(Player.Items.CountId(ObjectId("Tofu")) == 6 && Player.Items.CountId(ObjectId("SoySauce")) == 4, "Wrong final totals");
        });
        this.SaveCheckpoint(3);
    }
    private void AfterCollectionLoad()
    {
        this.Test("collected disk save cannot regenerate products", () =>
        {
            this.VerifySnapshot();
            for (int i = 0; i < 4; i++) Require(Read(this.Machine(i)).CanMove && !this.Operate(this.Machine(i), null).Applied, "Collected machine produced again");
            Require(this.State.Inventory == Inventory(), "Duplicate collection changed inventory");
        });
        this.Stopped = true; this.Report(true, null);
        Game1.activeClickableMenu = new DialogueBox("P1 保存、跨季节重载与获取链路集成验证通过。");
    }
    private void SetCheckpoint(int stage)
    {
        this.VerifyIdentity(); this.State.Stage = stage;
        this.State.Raw = Enumerable.Range(0, 4).Select(i => this.Machine(i).modData[MachineService.StateKey]).ToList();
        this.State.Inventory = Inventory();
        Player.modData[MarkerKey] = JsonSerializer.Serialize(this.State);
    }
    private void SaveCheckpoint(int stage)
    {
        this.SetCheckpoint(stage); this.Report(false, null);
        this.Save = SaveGame.Save();
    }
    private void Reload()
    {
        this.VerifyIdentity();
        this.Monitor.Log("Saved checkpoint " + this.State.Stage + "; exiting to title before disk reload.", LogLevel.Info);
        Game1.ExitToTitle(); this.QueueLoad();
    }
    private void Report(bool completed, string? failure) => this.Helper.Data.WriteJsonFile("persistence-result.json",
        new { Completed = completed, Stage = this.State.Stage, Passed = this.State.Passed, Failure = failure });
    public sealed class Checkpoint
    {
        public int Stage { get; set; }
        public List<int[]> Tiles { get; set; } = new();
        public List<string> Raw { get; set; } = new();
        public string Inventory { get; set; } = "";
        public List<string> Passed { get; set; } = new();
    }
}
