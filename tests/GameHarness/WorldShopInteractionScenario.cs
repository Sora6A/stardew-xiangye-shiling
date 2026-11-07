using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// P1三家商店的真实地图柜台/购买验证。只在独立XYSP1UI准备空背包、资金、人物位置与营业时刻；
/// 不创建商店菜单、不调用地图Action/购买/学习方法、不修改原生或CP库存。中键只核验并定位下一店。
/// 原生Action的只读postfix与实际ShopId分开留证，避免将鼠标候选格/任意菜单误作真实柜台入口。
/// 只改本次内存，不保存也不恢复；结束必须关闭进程。没有将准备/聊天等待计入的操作倒计时。
/// </summary>
internal sealed class WorldShopInteractionScenario
{
    private const string FarmName = "XYSP1UI", Id = MachineService.Prefix;
    private static readonly string[] Maps = { "SeedShop", "ScienceHouse", "Saloon" };
    private static readonly string[] Shops = { "SeedShop", "Carpenter", "Saloon" };
    private static readonly string[] Owners = { "Pierre", "Robin", "Gus" };
    private static readonly string[] Tokens = { "Buy", "Carpenter", "Saloon" };
    private static readonly int[] Times = { 900, 1000, 1200 }, Totals = { 19590, 16590, 15490 };
    private static readonly string[] Instructions = {
        "右键柜台，购买黄豆6、小麦2入包，关闭商店后按中键。不要保存。",
        "右键罗宾柜台，选择商店，购买豆坊和发酵缸图纸，关闭后按中键。",
        "右键格斯柜台，购买香椿1和香椿拌豆腐菜谱，关闭后按中键。"
    };
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Config;
    private readonly Harmony Patch;
    private static WorldShopInteractionScenario? Observer;
    private readonly List<object> Evidence = new(), NativeActions = new(), Stock = new();
    private int LoadDelay = -1, Step, RightClicks, Menus;
    private bool Pending, Started, AwaitingWarp, Stopped, SavingObserved;
    private string? Failure;
    private GameLocation Target = null!;
    private Vector2 Counter, Standing;
    private static Farmer Player => Game1.player;
    private string SaveName => FarmName + "_" + this.Config.SaveFixtureId;
    private static string ObjectId(string key) => "(O)" + Id + "_" + key;
    private static string MachineId(string key) => "(BC)" + Id + "_" + key;
    private static string RecipeId(string key) => Id + "_Recipe_" + key;
    private static int Count(string key) => Player.Items.CountId(ObjectId(key));
    private static void Require(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }

    internal WorldShopInteractionScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        this.Helper = helper; this.Monitor = monitor; this.Config = config;
        Require(config.FarmName == FarmName && config.SaveFixtureId != 0 && !config.OpenPreview,
            "WorldShopInteraction requires the explicit single-player XYSP1UI fixture and OpenPreview=false.");
        Require(Observer is null, "Another shop observation is active.");
        this.Patch = new Harmony(Id + ".GameHarness.WorldShopObservation"); Observer = this;
        this.Patch.Patch(AccessTools.Method(typeof(GameLocation), "performAction",
            new[] { typeof(string), typeof(Farmer), typeof(xTile.Dimensions.Location) }),
            postfix: new HarmonyMethod(typeof(WorldShopInteractionScenario), nameof(ObserveAction)));
    }
    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.Pending = true;
    private void VerifyIdentity() => Require(Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && Player.farmName.Value == FarmName && Game1.GetSaveGameName() == FarmName
        && Game1.uniqueIDForThisGame == this.Config.SaveFixtureId, "Refuse any world except the dedicated single-player shop fixture.");

    internal void Tick()
    {
        if (this.Stopped) return;
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, this.SaveName, this.SaveName)),
                    "Refuse to replace an active world or load an absent fixture.");
                Game1.activeClickableMenu = null; SaveGame.Load(this.SaveName);
            }
            if (this.Pending && Context.IsWorldReady)
            {
                this.Pending = false; this.VerifyIdentity();
                Require(Game1.dayOfMonth == 1 && Game1.season == Season.Spring && !Game1.eventUp && Player.CanMove
                    && Game1.activeClickableMenu is null, "Use the spring1 fixture with normal input and no event/menu.");
                Require(new[] { "BeanWorkshop", "FermentationCrock", "ToonTofu" }.All(key => !Player.knowsRecipe(RecipeId(key))),
                    "Use a fresh fixture with unlearned prototype recipes.");
                Player.Items.OverwriteWith(new Item[Player.MaxItems]); Player.Money = 20000;
                this.Started = true; this.Prepare();
            }
            if (this.Started && Context.IsWorldReady)
            {
                this.VerifyIdentity(); Game1.timeOfDay = Times[Math.Min(this.Step, 2)];
                if (this.AwaitingWarp && ReferenceEquals(Player.currentLocation, this.Target) && !Game1.fadeToBlack
                    && Game1.activeClickableMenu is null && Player.CanMove)
                {
                    Player.setTileLocation(this.Standing); Player.faceDirection(0);
                    this.AwaitingWarp = false; this.Report();
                    Game1.addHUDMessage(new HUDMessage(Instructions[this.Step], HUDMessage.newQuest_type));
                }
            }
        }
        catch (Exception error) { this.Fail(error); }
    }

    private void Prepare()
    {
        this.NativeActions.Clear(); this.Stock.Clear(); this.RightClicks = 0; this.Menus = 0;
        this.Target = Game1.getLocationFromName(Maps[this.Step]) ?? throw new InvalidOperationException("Shop map missing.");
        this.Counter = (from y in Enumerable.Range(1, this.Target.Map.Layers[0].LayerHeight - 2)
                        from x in Enumerable.Range(1, this.Target.Map.Layers[0].LayerWidth - 2)
                        let action = this.Target.doesTileHaveProperty(x, y, "Action", "Buildings")
                        where action?.Split(' ')[0] == Tokens[this.Step]
                            && this.Target.CanItemBePlacedHere(new Vector2(x, y + 1), collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
                        select new Vector2(x, y)).First();
        this.Standing = this.Counter + new Vector2(0, 1);
        NPC owner = Game1.getCharacterFromName(Owners[this.Step]) ?? throw new InvalidOperationException("Native shop owner missing.");
        owner.controller = null; owner.Schedule?.Clear(); owner.followSchedule = false;
        // Pierre原生柜台要求(4,17)；Gus须在(9,17,10,2)，Robin在柜台三格内。定位只属准备，不验营业日程。
        Vector2 ownerTile = this.Step == 0 ? new(4, 17) : this.Counter - new Vector2(0, 1);
        Game1.warpCharacter(owner, Maps[this.Step], ownerTile); owner.Halt(); owner.faceDirection(2);
        Player.CurrentToolIndex = Player.Items.ToList().FindIndex(item => item is null);
        Require(Player.CurrentToolIndex >= 0, "Shop fixture needs an empty selected slot.");
        Game1.timeOfDay = Times[this.Step]; this.AwaitingWarp = true;
        Game1.warpFarmer(Maps[this.Step], (int)this.Standing.X, (int)this.Standing.Y, false);
    }

    private static void ObserveAction(GameLocation __instance, string fullActionString, Farmer who,
        xTile.Dimensions.Location tileLocation, bool __result)
    {
        var owner = Observer;
        if (owner is null || !owner.Started || owner.Stopped || owner.AwaitingWarp || owner.Step >= 3) return;
        if (__result && ReferenceEquals(who, Player) && ReferenceEquals(__instance, owner.Target)
            && tileLocation.X == (int)owner.Counter.X && tileLocation.Y == (int)owner.Counter.Y
            && fullActionString.Split(' ')[0] == Tokens[owner.Step])
            owner.NativeActions.Add(new { Location = __instance.NameOrUniqueName, Action = fullActionString,
                X = tileLocation.X, Y = tileLocation.Y, Succeeded = true, LocalActor = true });
    }

    internal void OnMenu(IClickableMenu? menu)
    {
        if (!this.Started || this.Stopped || this.AwaitingWarp || this.Step >= 3 || menu is not ShopMenu shop || shop.ShopId != Shops[this.Step]) return;
        this.Menus++;
        string[] ids = this.Step switch { 0 => new[] { ObjectId("Soybean"), "(O)262" },
            1 => new[] { MachineId("BeanWorkshop"), MachineId("FermentationCrock") },
            _ => new[] { ObjectId("ChineseToon"), ObjectId("ToonTofu") } };
        foreach (var item in shop.forSale.OfType<SObject>().Where(item => ids.Contains(item.QualifiedItemId)))
        {
            var entry = shop.itemPriceAndStock[item];
            this.Stock.Add(new { ShopId = shop.ShopId, item.QualifiedItemId, IsRecipe = item.isRecipe.Value, entry.Price, entry.Stock });
        }
        this.Report();
    }

    internal void OnButton(SButton button)
    {
        if (!this.Started || this.Stopped || this.AwaitingWarp || this.Step >= 3) return;
        try
        {
            this.VerifyIdentity();
            if (button == SButton.MouseRight && Game1.activeClickableMenu is null) this.RightClicks++;
            if (button is not (SButton.MouseMiddle or SButton.F8)) return;
            Require(Game1.activeClickableMenu is null && !Player.isEating, "Close the shop before verifying.");
            Require(this.RightClicks > 0 && this.NativeActions.Count > 0 && this.Menus > 0 && this.Stock.Count >= 2,
                "Require actual counter input, successful native map Action and expected ShopId/stock.");
            Require(Player.Money == Totals[this.Step] && Count("Soybean") == 6 && Player.Items.CountId("(O)262") == 2
                && Count("ChineseToon") == (this.Step == 2 ? 1 : 0), "Wrong actual purchases, prior inventory or exact payment.");
            Require(Player.knowsRecipe(RecipeId("BeanWorkshop")) == (this.Step >= 1)
                && Player.knowsRecipe(RecipeId("FermentationCrock")) == (this.Step >= 1)
                && Player.knowsRecipe(RecipeId("ToonTofu")) == (this.Step == 2), "Recipe purchase/learning didn't match the stage.");
            Require(Player.Items.Where(item => item is not null).All(item => new[] { ObjectId("Soybean"), "(O)262", ObjectId("ChineseToon") }.Contains(item.QualifiedItemId)),
                "Unexpected inventory item; recipe purchases must teach recipes rather than grant devices or food.");
            this.Evidence.Add(new { Stage = this.Step, ShopId = Shops[this.Step], this.RightClicks, this.Menus,
                Actions = this.NativeActions.ToArray(), Stock = this.Stock.ToArray(), Player.Money,
                Soybean = Count("Soybean"), Wheat = Player.Items.CountId("(O)262"), ChineseToon = Count("ChineseToon"),
                BeanRecipe = Player.knowsRecipe(RecipeId("BeanWorkshop")), CrockRecipe = Player.knowsRecipe(RecipeId("FermentationCrock")),
                DishRecipe = Player.knowsRecipe(RecipeId("ToonTofu")) });
            this.Step++; this.Report();
            if (this.Step < 3) this.Prepare();
            else { this.Detach(); Game1.addHUDMessage(new HUDMessage("三家柜台购买通过；关闭测试窗口，不睡觉或保存。", HUDMessage.newQuest_type)); }
        }
        catch (Exception error) { this.Fail(error); }
    }
    internal void OnSaving() { this.SavingObserved = true; this.Fail(new InvalidOperationException("Unexpected saving during no-save shop validation.")); }
    internal void OnReturnedToTitle() { this.Detach(); if (this.Started && this.Step < 3 && !this.Stopped) this.Fail(new InvalidOperationException("Shop validation ended early.")); }
    private void Detach() { this.Patch.UnpatchAll(this.Patch.Id); if (ReferenceEquals(Observer, this)) Observer = null; }
    private void Fail(Exception error) { this.Stopped = true; this.Pending = false; this.LoadDelay = -1; this.Failure = error.ToString(); this.Detach(); this.Monitor.Log(this.Failure, LogLevel.Error); this.Report(); }
    private void Report() => this.Helper.Data.WriteJsonFile("world-shop-interaction-result.json", new {
        SchemaVersion = 1, Scenario = "WorldShopInteraction", ProcessId = Environment.ProcessId, FixtureId = this.Config.SaveFixtureId,
        Completed = this.Step == 3 && this.Failure is null && !this.SavingObserved, this.Step, this.Failure, this.SavingObserved,
        SavedByHarness = false, this.AwaitingWarp, this.Evidence,
        CurrentShop = this.Step < 3 ? Shops[this.Step] : null, this.RightClicks, this.Menus, Actions = this.NativeActions.ToArray(),
        Preparation = "In-memory empty backpack/funds, owner position/schedule, shop warps and fixed opening times; no physical travel or natural NPC schedule acceptance."
    });
}
