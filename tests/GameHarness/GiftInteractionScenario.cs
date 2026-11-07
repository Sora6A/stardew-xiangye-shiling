using Microsoft.Xna.Framework;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Locations;
using XiangyeShiling.GameIntegration;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// P1成品赠礼的真实鼠标验证，只在独立XYSP1UI副本准备两份料理和可接近的原版Robin。
/// 不调用NPC.checkAction/tryToReceiveActiveObject/receiveGift，不扣料理或修改观测中的好感。
/// 准备会改变本次内存中的库存、NPC位置/日程、好感与时钟；不保存、不恢复，完成后必须退出进程。
/// 没有包含准备/聊天等待的限时器；中键只在关闭对话后核验，失败立即停下并保留报告。
/// KitchenGiftInteraction先准备真实升级农舍厨房/已学菜谱/原料；须由灶台实际右键打开菜单并烹饪两份，
/// 核验后保留同一库存成品用于送礼。该分支不创建料理/烹饪菜单或调用地图Action，不重复同日拒绝阶段。
/// </summary>
internal sealed class GiftInteractionScenario
{
    private const string FarmName = "XYSP1UI", NpcName = "Robin";
    private const string DishId = "(O)" + MachineService.Prefix + "_ToonTofu";
    private const string ToonId = "(O)" + MachineService.Prefix + "_ChineseToon";
    private const string TofuId = "(O)" + MachineService.Prefix + "_Tofu";
    private const string RecipeId = MachineService.Prefix + "_Recipe_ToonTofu";
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Config;
    private readonly bool KitchenFirst;
    private int KitchenClicks, KitchenMenus, CookedBefore;
    private Vector2 KitchenTile;
    private bool KitchenCompleted;
    private Harmony? KitchenPatch;
    private static GiftInteractionScenario? KitchenObserver;
    private readonly List<KitchenActionSample> NativeKitchenActions = new();
    private int LastGrabX, LastGrabY;
    private int LoadDelay = -1, Step, RightClicks, Dialogues, Taste, ExpectedPoints;
    private uint GiftsBefore;
    private bool Pending, Started, Stopped, SavingObserved;
    private string? Failure;
    private NPC Recipient = null!;
    private readonly List<object> Evidence = new();
    private readonly List<object> KitchenInputSamples = new();
    private static Farmer Player => Game1.player;
    private string SaveName => FarmName + "_" + this.Config.SaveFixtureId;
    private int FinalStep => this.KitchenFirst ? 1 : 2;

    internal GiftInteractionScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        this.Helper = helper; this.Monitor = monitor; this.Config = config;
        this.KitchenFirst = config.Scenario == "KitchenGiftInteraction";
        this.Step = this.KitchenFirst ? -1 : 0;
        Require(config.FarmName == FarmName && config.SaveFixtureId != 0 && !config.OpenPreview,
            "GiftInteraction requires the explicit XYSP1UI fixture ID and OpenPreview=false.");
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.Pending = true;
    private static void Require(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }
    private void VerifyIdentity() => Require(Context.IsMainPlayer && !Context.IsMultiplayer && Context.IsWorldReady
        && Player.farmName.Value == FarmName && Game1.GetSaveGameName() == FarmName
        && Game1.uniqueIDForThisGame == this.Config.SaveFixtureId,
        "GiftInteraction refuses any world except the dedicated single-player fixture.");

    internal void Tick()
    {
        if (this.Stopped) return;
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady, "Refuse to replace an active world.");
                Require(File.Exists(Path.Combine(Constants.SavesPath, this.SaveName, this.SaveName)), "Gift fixture is not installed.");
                Game1.activeClickableMenu = null;
                SaveGame.Load(this.SaveName);
            }
            if (this.Pending && Context.IsWorldReady)
            {
                this.Pending = false; this.VerifyIdentity(); this.Prepare(); this.Started = true;
                this.Report();
            }
            // 避免人工查看时跨日/昏倒；两次赠礼必须确实发生于同一天。
            if (this.Started && Context.IsWorldReady) { this.VerifyIdentity(); Game1.timeOfDay = 1200; }
        }
        catch (Exception error) { this.Fail(error); }
    }

    private void Prepare()
    {
        Require(Game1.dayOfMonth == 1 && Game1.season == Season.Spring && !Game1.eventUp,
            "Use the spring1 fixture without a native event.");
        Require(Player.currentLocation is FarmHouse, "Gift preparation expects the fixture farmhouse.");
        var house = (FarmHouse)Player.currentLocation;
        Require(Player.CanMove && Game1.activeClickableMenu is null, "Gift preparation requires normal player input without a menu.");
        if (this.KitchenFirst) { this.PrepareKitchen(house); return; }
        var stock = new Item[Player.MaxItems]; stock[0] = ItemRegistry.Create<SObject>(DishId, 2);
        Player.Items.OverwriteWith(stock);
        this.PrepareRecipient(house);
    }

    private void PrepareKitchen(FarmHouse house)
    {
        // 只升级专用副本的当前内存，地图/灶台由原生方法生成；不写XML，也不绕过厨房入口。
        house.setMapForUpgradeLevel(1);
        Require(house.GetFridge()?.Items.Any(item => item is not null) != true,
            "Kitchen fixture requires an empty fridge so only prepared backpack ingredients can be used.");
        this.KitchenTile = (from y in Enumerable.Range(1, house.Map.Layers[0].LayerHeight - 2)
                            from x in Enumerable.Range(1, house.Map.Layers[0].LayerWidth - 2)
                            let tile = new Vector2(x, y)
                            where house.doesTileHaveProperty(x, y, "Action", "Buildings") is "kitchen" or "Kitchen"
                                && CanStand(house, tile + new Vector2(0, 1))
                            select tile).First();
        Player.setTileLocation(this.KitchenTile + new Vector2(0, 1)); Player.faceDirection(0);
        var stock = new Item[Player.MaxItems]; stock[0] = ItemRegistry.Create(ToonId, 2); stock[1] = ItemRegistry.Create(TofuId, 2);
        Player.Items.OverwriteWith(stock); Player.CurrentToolIndex = 2;
        Player.cookingRecipes[RecipeId] = 0;
        this.CookedBefore = CookedCount();
        Require(KitchenObserver is null, "A kitchen observation is already active.");
        this.KitchenPatch = new Harmony(MachineService.Prefix + ".GameHarness.KitchenObservation");
        KitchenObserver = this;
        this.KitchenPatch.Patch(AccessTools.Method(typeof(GameLocation), "performAction",
            new[] { typeof(string), typeof(Farmer), typeof(xTile.Dimensions.Location) }),
            postfix: new HarmonyMethod(typeof(GiftInteractionScenario), nameof(ObserveKitchenAction)));
        Game1.timeOfDay = 1200;
        Game1.addHUDMessage(new HUDMessage("右键灶台，制作两份香椿拌豆腐放入背包，关闭菜单后按中键。不要保存。", HUDMessage.newQuest_type));
    }

    private static bool CanStand(FarmHouse house, Vector2 p) => !house.isTileOnWall((int)p.X, (int)p.Y)
        && house.getFloors().Any(floor => floor.Contains((int)p.X, (int)p.Y))
        && house.CanItemBePlacedHere(p, collisionMask: CollisionMask.All & ~CollisionMask.Farmers);
    private static int CookedCount() => Player.recipesCooked.TryGetValue(MachineService.Prefix + "_ToonTofu", out int count) ? count : 0;

    private void PrepareRecipient(FarmHouse house)
    {
        // Kitchen分支不能再创建成品：必须保留同一背包中由原版烹饪真正产出的两份。
        Require(Player.Items.CountId(DishId) == 2, "Gift preparation requires exactly two dishes in the actual inventory.");
        // 不移除家具/对象：查询原生空地，只将本次内存的两个人物定位到相邻可站立格。
        Vector2 tile = (from y in Enumerable.Range(2, house.Map.Layers[0].LayerHeight - 4)
                        from x in Enumerable.Range(2, house.Map.Layers[0].LayerWidth - 4)
                        let candidate = new Vector2(x, y)
                        where CanStand(house, candidate) && CanStand(house, candidate + new Vector2(1, 0))
                        select candidate).First();
        this.Recipient = Game1.getCharacterFromName(NpcName) ?? throw new InvalidOperationException("Native Robin missing.");
        Require(this.Recipient.CanReceiveGifts() && !this.Recipient.isBirthday() && string.IsNullOrEmpty(Player.spouse),
            "Gift fixture requires an unmarried player and a non-birthday giftable NPC.");
        this.Recipient.controller = null; this.Recipient.Schedule?.Clear(); this.Recipient.followSchedule = false;
        Game1.warpCharacter(this.Recipient, house.NameOrUniqueName, tile);
        this.Recipient.Halt(); this.Recipient.faceDirection(1);
        Player.setTileLocation(tile + new Vector2(1, 0)); Player.faceDirection(3);
        Player.friendshipData[NpcName] = new Friendship(0);
        Player.CurrentToolIndex = Player.Items.ToList().FindIndex(item => item?.QualifiedItemId == DishId);
        Require(Player.ActiveObject is { Quality: 0 }, "Gift fixture requires a normal-quality cooked dish.");
        this.Taste = this.Recipient.getGiftTasteForThisItem(Player.ActiveObject);
        // 来自当前原生普通品质/非生日/非配偶规则；不是本模组新增喜好数据。
        this.ExpectedPoints = this.Taste switch { 0 => 80, 2 => 45, 4 => -20, 6 => -40, 8 => 20,
            _ => throw new InvalidOperationException("Unexpected native taste: " + this.Taste) };
        this.GiftsBefore = Game1.stats.GiftsGiven;
        Game1.timeOfDay = 1200;
        Game1.addHUDMessage(new HUDMessage("右键罗宾送一份料理，关闭对话，再按中键核验。不要保存。", HUDMessage.newQuest_type));
    }

    internal void OnButton(SButton button)
    {
        if (!this.Started || this.Stopped || this.Step == this.FinalStep) return;
        try
        {
            this.VerifyIdentity();
            if (this.Step == -1 && button == SButton.MouseRight)
            {
                var cursor = this.Helper.Input.GetCursorPosition();
                this.LastGrabX = (int)cursor.GrabTile.X; this.LastGrabY = (int)cursor.GrabTile.Y;
                this.KitchenInputSamples.Add(new { ConfiguredX = (int)this.KitchenTile.X, ConfiguredY = (int)this.KitchenTile.Y,
                    CursorX = (int)cursor.Tile.X, CursorY = (int)cursor.Tile.Y, GrabX = (int)cursor.GrabTile.X, GrabY = (int)cursor.GrabTile.Y,
                    ActiveMenu = Game1.activeClickableMenu?.GetType().Name, Player.CanMove,
                    CursorAction = Player.currentLocation.doesTileHaveProperty((int)cursor.Tile.X, (int)cursor.Tile.Y, "Action", "Buildings"),
                    GrabAction = Player.currentLocation.doesTileHaveProperty((int)cursor.GrabTile.X, (int)cursor.GrabTile.Y, "Action", "Buildings") });
                if (Game1.activeClickableMenu is null) this.KitchenClicks++;
                this.Report();
            }
            if (button == SButton.MouseRight && Game1.activeClickableMenu is null && Player.ActiveObject?.QualifiedItemId == DishId)
                this.RightClicks++;
            if (button is not (SButton.MouseMiddle or SButton.F8)) return;
            Require(Game1.activeClickableMenu is null && !Player.isEating, "Close the native dialogue before verifying.");
            if (this.Step == -1)
            {
                Require(this.KitchenClicks > 0 && this.KitchenMenus > 0 && this.NativeKitchenActions.Count > 0 && Player.currentLocation is FarmHouse,
                    "Require actual kitchen-tile input and the resulting native cooking menu.");
                Require(Player.Items.CountId(ToonId) == 0 && Player.Items.CountId(TofuId) == 0 && Player.Items.CountId(DishId) == 2
                    && CookedCount() == this.CookedBefore + 2, "Native kitchen did not consume two of each ingredient/cook exactly two dishes.");
                this.Evidence.Add(new { Stage = -1, KitchenTileX = (int)this.KitchenTile.X, KitchenTileY = (int)this.KitchenTile.Y,
                    Action = Player.currentLocation.doesTileHaveProperty((int)this.KitchenTile.X, (int)this.KitchenTile.Y, "Action", "Buildings"),
                    this.KitchenClicks, this.KitchenMenus, NativeActions = this.NativeKitchenActions.ToArray(),
                    ToonCount = Player.Items.CountId(ToonId), TofuCount = Player.Items.CountId(TofuId),
                    DishCount = Player.Items.CountId(DishId), NativeCookedDelta = CookedCount() - this.CookedBefore });
                this.KitchenCompleted = true; this.Step = 0;
                this.PrepareRecipient((FarmHouse)Player.currentLocation); this.Report(); return;
            }
            Require(this.RightClicks > 0 && this.Dialogues > 0, "Require a real right-click and resulting native dialogue.");
            Friendship friendship = Player.friendshipData[NpcName];
            Require(Player.Items.CountId(DishId) == 1 && friendship.Points == this.ExpectedPoints
                && friendship.GiftsToday == 1 && friendship.GiftsThisWeek == 1
                && Game1.stats.GiftsGiven == this.GiftsBefore + 1 && Player.hasItemBeenGifted(this.Recipient, MachineService.Prefix + "_ToonTofu"),
                this.Step == 0 ? "Native gift didn't consume exactly one dish/update friendship once."
                    : "Same-day rejected gift changed inventory, friendship, or gift counters.");
            this.Evidence.Add(new { Stage = this.Step, RightClicks = this.RightClicks, NativeDialogues = this.Dialogues,
                DishCount = Player.Items.CountId(DishId), friendship.Points, friendship.GiftsToday, friendship.GiftsThisWeek,
                TotalGiftDelta = Game1.stats.GiftsGiven - this.GiftsBefore });
            this.Step++; this.RightClicks = 0; this.Dialogues = 0; this.Report();
            if (this.Step == this.FinalStep) this.DetachKitchenObservation();
            Game1.addHUDMessage(new HUDMessage(this.Step == this.FinalStep
                ? "本轮流程通过；关闭测试窗口，不睡觉或保存。"
                : this.Step == 1
                ? "再右键罗宾尝试当天第二份，关闭拒绝对话后按中键。"
                : "赠礼与当天重复拒绝通过；关闭测试窗口，不睡觉或保存。", HUDMessage.newQuest_type));
        }
        catch (Exception error) { this.Fail(error); }
    }

    internal void OnMenu(IClickableMenu? menu)
    {
        if (this.Started && !this.Stopped && this.Step == -1 && this.KitchenClicks > 0 && this.NativeKitchenActions.Count > 0
            && menu is CraftingPage { cooking: true })
            this.KitchenMenus++;
        if (this.Started && !this.Stopped && this.Step < 2 && this.RightClicks > 0 && menu is DialogueBox) this.Dialogues++;
    }
    internal void OnSaving() { this.SavingObserved = true; this.Fail(new InvalidOperationException("Unexpected native saving during no-save gift validation.")); }
    internal void OnReturnedToTitle()
    {
        this.DetachKitchenObservation();
        if (this.Started && this.Step != this.FinalStep && !this.Stopped)
            this.Fail(new InvalidOperationException("Gift validation ended before its final stage."));
    }
    // BUG-2026-023：SMAPI GrabTile不等于原生最终Action格。只读postfix捕获成功执行的真实格，
    // 同时要求本地Farmer/当前Location/厨房token；不能仅因鼠标靠近厨房或弹出任意菜单而算通过。
    private static void ObserveKitchenAction(GameLocation __instance, string fullActionString, Farmer who,
        xTile.Dimensions.Location tileLocation, bool __result)
    {
        var owner = KitchenObserver;
        if (owner is null || !owner.Started || owner.Stopped || owner.Step != -1) return;
        var sample = new KitchenActionSample((int)owner.KitchenTile.X, (int)owner.KitchenTile.Y,
            owner.LastGrabX, owner.LastGrabY, tileLocation.X, tileLocation.Y, fullActionString, __result,
            ReferenceEquals(who, Player), ReferenceEquals(__instance, Player.currentLocation));
        if (KitchenEntryProof.Matches(sample)) owner.NativeKitchenActions.Add(sample);
    }
    private void DetachKitchenObservation()
    {
        this.KitchenPatch?.UnpatchAll(this.KitchenPatch.Id); this.KitchenPatch = null;
        if (ReferenceEquals(KitchenObserver, this)) KitchenObserver = null;
    }
    private void Fail(Exception error)
    {
        this.Stopped = true; this.Pending = false; this.LoadDelay = -1; this.Failure = error.ToString();
        this.DetachKitchenObservation();
        this.Monitor.Log(this.Failure, LogLevel.Error); this.Report();
    }
    private void Report() => this.Helper.Data.WriteJsonFile(this.KitchenFirst ? "kitchen-gift-interaction-result.json" : "gift-interaction-result.json", new
    {
        SchemaVersion = 1, Scenario = this.KitchenFirst ? "KitchenGiftInteraction" : "GiftInteraction", ProcessId = Environment.ProcessId,
        FixtureId = this.Config.SaveFixtureId, Completed = this.Step == this.FinalStep && (!this.KitchenFirst || this.KitchenCompleted)
            && this.Failure is null && !this.SavingObserved,
        this.KitchenCompleted, this.KitchenClicks, this.KitchenMenus,
        this.KitchenInputSamples,
        NativeKitchenActions = this.NativeKitchenActions.ToArray(),
        this.Step, this.Failure, this.SavingObserved, SavedByHarness = false,
        Recipient = NpcName, ItemId = DishId, NativeGiftTaste = this.Taste, ExpectedFriendshipDelta = this.ExpectedPoints,
        this.Evidence, this.RightClicks, NativeDialogues = this.Dialogues,
        Preparation = this.KitchenFirst
            ? "In-memory upgraded native farmhouse/kitchen, learned recipe/raw ingredients, NPC positioning/friendship and fixed noon; no physical map travel."
            : "In-memory inventory, friendship, NPC positioning/schedule and fixed noon; no physical map travel."
    });
}
