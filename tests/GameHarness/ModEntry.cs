using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Inventories;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

public sealed class Config
{
    public bool Enabled { get; set; }
    public string FarmName { get; set; } = "XYS_P0";
    public bool OpenPreview { get; set; }
    public string Scenario { get; set; } = "Memory";
    public ulong SaveFixtureId { get; set; }
    public string NetworkRole { get; set; } = "Observe";
}

/// <summary>
/// 仅本地集成夹具：Memory 模式改内存后 finally 恢复，不调用保存；
/// Persistence 模式会保存明确指定的独立副本，并通过实际磁盘重载继续验证；
/// Interaction 模式为真实界面输入准备并核对内存现场，不保存，也不承诺恢复本次内存。
/// WorldInteraction 为原生鼠标工具/自然拾取准备现场，同样不保存或恢复本次内存。
/// InputObservation只被动采样专用主机输入，不加载、不准备、不改变玩家/时间或发送网络探针。
/// 模式契约不能混用，配置与复跑步骤见本目录 README；禁止在正式存档启用。
/// </summary>
public sealed class ModEntry : Mod
{
    private const string Id = MachineService.Prefix;
    private bool Pending;
    private Config Settings = null!;
    private PersistenceScenario? Persistence;
    private InteractionScenario? Interaction;
    private WorldInteractionScenario? WorldInteraction;
    private NetworkScenario? Network;
    private NetworkLifecycleScenario? NetworkLifecycle;
    private NetworkWelcomeReplayScenario? NetworkWelcomeReplay;
    private InputObservationScenario? InputObservation;
    private GiftInteractionScenario? GiftInteraction;
    private WorldShopInteractionScenario? WorldShops;
    public override void Entry(IModHelper helper)
    {
        this.Settings = helper.ReadConfig<Config>();
        if (this.Settings.Enabled && this.Settings.Scenario == "WorldShopInteraction")
        {
            this.WorldShops = new(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.GameLaunched += (_, _) => this.WorldShops.QueueLoad();
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.WorldShops.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.WorldShops.Tick();
            helper.Events.Input.ButtonPressed += (_, e) => this.WorldShops.OnButton(e.Button);
            helper.Events.Display.MenuChanged += (_, e) => this.WorldShops.OnMenu(e.NewMenu);
            helper.Events.GameLoop.Saving += (_, _) => this.WorldShops.OnSaving();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.WorldShops.OnReturnedToTitle();
            return;
        }
        if (this.Settings.Enabled && this.Settings.Scenario is "GiftInteraction" or "KitchenGiftInteraction")
        {
            this.GiftInteraction = new(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.GameLaunched += (_, _) => this.GiftInteraction.QueueLoad();
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.GiftInteraction.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.GiftInteraction.Tick();
            helper.Events.Input.ButtonPressed += (_, e) => this.GiftInteraction.OnButton(e.Button);
            helper.Events.Display.MenuChanged += (_, e) => this.GiftInteraction.OnMenu(e.NewMenu);
            helper.Events.GameLoop.Saving += (_, _) => this.GiftInteraction.OnSaving();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.GiftInteraction.OnReturnedToTitle();
            return;
        }
        if (this.Settings.Enabled && this.Settings.Scenario == "NetworkWelcomeReplay")
        {
            this.NetworkWelcomeReplay = new(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.NetworkWelcomeReplay.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.NetworkWelcomeReplay.Tick();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.NetworkWelcomeReplay.OnReturnedToTitle();
            helper.Events.GameLoop.Saving += (_, _) => this.NetworkWelcomeReplay.OnSaving();
            return;
        }
        if (this.Settings.Enabled && this.Settings.Scenario == "NetworkLifecycle")
        {
            this.NetworkLifecycle = new NetworkLifecycleScenario(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.NetworkLifecycle.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.NetworkLifecycle.Tick();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.NetworkLifecycle.OnReturnedToTitle();
            helper.Events.GameLoop.Saving += (_, _) => this.NetworkLifecycle.OnSaving();
            return;
        }
        if (this.Settings.Enabled && this.Settings.Scenario == "InputObservation")
        {
            this.InputObservation = new InputObservationScenario(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.InputObservation.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.InputObservation.Tick();
            helper.Events.Input.ButtonPressed += (_, e) => this.InputObservation.OnButtonPressed(e.Button);
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.InputObservation.OnReturnedToTitle();
            return;
        }
        if (this.Settings.Enabled && this.Settings.Scenario == "Network")
        {
            this.Network = new NetworkScenario(helper, this.Settings);
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.Network.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.Network.Tick();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.Network.OnReturnedToTitle();
            return;
        }
        if (this.Settings.Enabled && this.Settings.Scenario == "WorldInteraction")
        {
            this.WorldInteraction = new WorldInteractionScenario(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.GameLaunched += (_, _) => this.WorldInteraction.QueueLoad();
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.WorldInteraction.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.WorldInteraction.Tick();
            helper.Events.Input.ButtonPressed += (_, e) =>
            {
                if ((e.Button == SButton.F8 || e.Button == SButton.MouseMiddle) && Game1.activeClickableMenu is null)
                    this.WorldInteraction.Advance();
            };
            return;
        }
        if (this.Settings.Enabled && this.Settings.Scenario == "Interaction")
        {
            this.Interaction = new InteractionScenario(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.GameLaunched += (_, _) => this.Interaction.QueueLoad();
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.Interaction.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.Interaction.Tick();
            helper.Events.Input.ButtonPressed += (_, e) =>
            {
                // 中键入口适用于桌面自动化的短键按压被游戏轮询遗漏时；两者只核验，不替代 UI 交易。
                if ((e.Button == SButton.F8 || e.Button == SButton.MouseMiddle) && Game1.activeClickableMenu is null)
                    this.Interaction.Advance();
            };
            return;
        }
        // 持久化测试必须另建固定名称/独立 ID 的专用农场，不能复用普通恢复式测试配置。
        if (this.Settings.Enabled && this.Settings.Scenario == "Persistence")
        {
            this.Persistence = new PersistenceScenario(helper, this.Monitor, this.Settings);
            helper.Events.GameLoop.GameLaunched += (_, _) => this.Persistence.QueueLoad();
            helper.Events.GameLoop.SaveLoaded += (_, _) => this.Persistence.OnLoaded();
            helper.Events.GameLoop.UpdateTicked += (_, _) => this.Persistence.Tick();
            return;
        }
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.Pending = true;
        helper.Events.GameLoop.OneSecondUpdateTicked += (_, _) =>
        {
            if (!this.Pending || !Context.IsWorldReady) return;
            this.Pending = false;
            if (!this.Settings.Enabled || Context.IsMultiplayer || Game1.player.farmName.Value != this.Settings.FarmName) return;
            this.Run();
        };
    }

    private void Run()
    {
        var passed = new List<string>();
        string? failure = null;
        Farmer player = Game1.player;
        GameLocation location = player.currentLocation;
        Item[] original = player.Items.ToArray();
        int clock = Game1.timeOfDay;
        Vector2 tile = player.Tile + new Vector2(1, 0);
        if (location.objects.ContainsKey(tile))
        {
            this.Monitor.Log("Harness aborted: adjacent fixture tile isn't empty.", LogLevel.Error);
            return;
        }
        SObject? machine = null;
        // 非泛型入口返回同进程原对象；GetApi<T> 的 T 必须是接口，不能用 object。
        MachineService service = this.Helper.ModRegistry.GetApi(Id) as MachineService
            ?? throw new InvalidOperationException("Main mod API is unavailable; no fixture changes made.");
        string Soy = "(O)" + Id + "_Soybean", Sauce = "(O)" + Id + "_SoySauce", Tofu = "(O)" + Id + "_Tofu";
        string recipe = Id + "_Recipe_SoySauce";
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Test(string name, Action action) { action(); passed.Add(name); this.Monitor.Log("PASS " + name, LogLevel.Info); }
        void Stock(params Item[] items)
        {
            var slots = new Item[player.MaxItems];
            Array.Copy(items, slots, items.Length);
            player.Items.OverwriteWith(slots);
        }
        MachineState State() { Check(MachineService.TryRead(machine!, out var state), "Invalid fixture state"); return state!; }
        Request Request() { var state = State(); return new(Guid.NewGuid(), state.InstanceId, state.Revision, player.UniqueMultiplayerID); }
        string Raw() => machine!.modData[MachineService.StateKey];
        void Fresh(string key)
        {
            if (machine is not null) location.objects.Remove(tile);
            machine = ItemRegistry.Create<SObject>("(BC)" + Id + "_" + key);
            machine.TileLocation = tile;
            location.objects.Add(tile, machine);
            MachineService.Initialize(machine);
        }
        try
        {
            Test("shop and recipe output identities", () =>
            {
                var shops = this.Helper.GameContent.Load<Dictionary<string, ShopData>>("Data/Shops");
                Check(shops["SeedShop"].Items.Any(i => i.ItemId == Soy && i.AvailableStock == 10), "Soy stock missing");
                foreach (string key in new[] { "BeanWorkshop", "FermentationCrock", "ToonTofu" })
                {
                    bool cooking = key == "ToonTofu";
                    string rid = Id + "_Recipe_" + key;
                    var data = this.Helper.GameContent.Load<Dictionary<string, string>>(cooking ? "Data/CookingRecipes" : "Data/CraftingRecipes");
                    Check(data.ContainsKey(rid), "Missing recipe " + rid);
                    var craft = new CraftingRecipe(rid, cooking);
                    Item output = craft.createItem();
                    Check(output.Name == rid && output.QualifiedItemId == (cooking ? "(O)" : "(BC)") + Id + "_" + key, "Recipe name/output mismatch");
                    Check(shops[cooking ? "Saloon" : "Carpenter"].Items.Any(i => i.IsRecipe && i.ItemId == output.QualifiedItemId), "Recipe shop missing");
                }
            });
            Fresh("FermentationCrock");
            Test("missing second ingredient preserves real inventory and state", () =>
            {
                Stock(ItemRegistry.Create(Soy, 3), ItemRegistry.Create("(O)262", 1));
                Item first = player.Items[0]; string before = Raw();
                Check(!service.Execute(machine!, player, Request(), recipe).Applied, "Accepted missing wheat");
                Check(ReferenceEquals(first, player.Items[0]) && first.Stack == 3 && Raw() == before, "Partial debit");
            });
            Test("actual mixed quality input and duplicate request", () =>
            {
                Stock(ItemRegistry.Create(Soy, 2, 2), ItemRegistry.Create("(O)262", 2), ItemRegistry.Create(Soy, 4));
                var request = Request();
                var result = service.Execute(machine!, player, request, recipe);
                Check(result.Applied, result.Message);
                Check(player.Items.CountId(Soy) == 3 && player.Items.CountId("(O)262") == 0 && State().Batch!.Inputs.First().Quality == 2, "Wrong costs");
                string before = Raw();
                Check(!service.Execute(machine!, player, request, recipe).Applied && Raw() == before && player.Items.CountId(Soy) == 3, "Replay deducted");
            });
            Test("busy machine blocks tool explosion NPC and location removal", () =>
            {
                Check(!machine!.performToolAction(new StardewValley.Tools.Pickaxe()), "Tool accepted");
                Check(!machine.onExplosion(player), "Explosion accepted");
                var npc = typeof(GameLocation).GetMethod("checkDestroyItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                Check(!(bool)npc.Invoke(location, new object[] { machine, tile, false })!, "NPC removal accepted");
                Check(location.removeObject(tile, false) is null, "removeObject accepted");
                location.destroyObject(tile, true, player);
                Check(ReferenceEquals(location.objects[tile], machine), "Object lost");
            });
            Test("full real inventory keeps completed batch and exact collection", () =>
            {
                Game1.timeOfDay += 400;
                Stock(Enumerable.Range(0, player.MaxItems).Select(_ => ItemRegistry.Create("(O)388", 999)).ToArray());
                string before = Raw();
                Check(!service.Execute(machine!, player, Request(), null).Applied && Raw() == before, "Full inventory consumed batch");
                Stock(); var request = Request();
                Check(service.Execute(machine!, player, request, null).Applied, "Collection failed");
                Check(player.Items.CountId(Sauce) == 4 && State().CanMove, "Wrong collected quantity");
                Check(!service.Execute(machine!, player, request, null).Applied && player.Items.CountId(Sauce) == 4, "Duplicated output");
            });
            Test("invalid state preserved and empty machine removable", () =>
            {
                machine!.modData[MachineService.StateKey] = "{invalid";
                MachineService.Initialize(machine);
                Check(Raw() == "{invalid" && MachineService.BlocksRemoval(machine), "Corrupt state reset");
                Fresh("BeanWorkshop");
                Check(ReferenceEquals(location.removeObject(tile, false), machine), "Idle removal blocked");
                location.objects.Add(tile, machine!);
            });
            Test("bean route produces two tofu using actual inventory", () =>
            {
                Stock(ItemRegistry.Create(Soy, 3));
                var result = service.Execute(machine!, player, Request(), Id + "_Recipe_Tofu");
                Check(result.Applied, result.Message);
                Game1.timeOfDay += 200;
                Check(service.Execute(machine!, player, Request(), null).Applied && player.Items.CountId(Tofu) == 2, "Tofu output failed");
            });
            Test("collection double click cannot start an unintended batch", () =>
            {
                Fresh("BeanWorkshop"); Stock(ItemRegistry.Create(Soy, 6));
                Check(service.Execute(machine!, player, Request(), Id + "_Recipe_Tofu").Applied, "Setup start failed");
                Game1.timeOfDay += 200;
                var previousMenu = Game1.activeClickableMenu;
                Game1.activeClickableMenu = null;
                try
                {
                    machine!.checkForAction(player);
                    var menu = Game1.activeClickableMenu as XiangyeShiling.Menus.MachineMenu;
                    Check(menu is not null, "Machine menu wasn't opened");
                    // BUG-2026-007：同一按钮坐标上的第二个鼠标点击仍属于领取意图，不能成为新开工。
                    int x = menu!.xPositionOnScreen + 500, y = menu.yPositionOnScreen + 170;
                    menu.receiveLeftClick(x, y);
                    menu.receiveLeftClick(x, y);
                    Check(State().CanMove && player.Items.CountId(Soy) == 3 && player.Items.CountId(Tofu) == 2,
                        "Collection double click unexpectedly consumed the next batch");
                }
                finally { Game1.activeClickableMenu = previousMenu; }
            });
            Test("guide navigation never acquires an operation and preserves the completed-menu lock", () =>
            {
                Fresh("BeanWorkshop"); Stock(ItemRegistry.Create(Soy, 6));
                string before = Raw(); Item[] items = player.Items.ToArray();
                var previousMenu = Game1.activeClickableMenu; Game1.activeClickableMenu = null;
                try
                {
                    // BUG-2026-022：Helper.Translation属于GameHarness，不能传给生产菜单。
                    // 经过真实设备互动prefix取得生产菜单，复用生产模组自己的翻译器与准入入口。
                    machine!.checkForAction(player);
                    var menu = Game1.activeClickableMenu as MachineMenu
                        ?? throw new InvalidOperationException("Native interaction did not create the production menu.");
                    var session = this.Helper.Reflection.GetField<MenuActionSession>(menu, "Session").GetValue();
                    void Click(string property)
                    {
                        Point center = this.Helper.Reflection.GetProperty<Rectangle>(menu, property).GetValue().Center;
                        menu.receiveLeftClick(center.X, center.Y);
                    }
                    bool Open() => this.Helper.Reflection.GetField<bool>(menu, "GuideOpen").GetValue();
                    int Page() => this.Helper.Reflection.GetField<int>(menu, "GuidePage").GetValue();
                    Click("GuideBounds"); Check(Open() && Page() == 0, "Guide did not open at sources");
                    menu.receiveLeftClick(menu.xPositionOnScreen + 500, menu.yPositionOnScreen + 170);
                    Click("NextBounds"); Check(Open() && Page() == 1, "Recipe guide page missing");
                    Click("PreviousBounds"); Check(Page() == 0, "Guide back navigation failed");
                    Click("GuideBounds"); Check(!Open() && ReferenceEquals(Game1.activeClickableMenu, menu), "Guide replaced the operation session");
                    Check(Raw() == before && player.Items.SequenceEqual(items) && player.Items.CountId(Soy) == 6
                        && this.Helper.Reflection.GetField<Request?>(session, "Pending").GetValue() is null,
                        "Guide navigation acquired a request or changed inventory/state");
                    menu.receiveLeftClick(menu.xPositionOnScreen + 500, menu.yPositionOnScreen + 170);
                    string started = Raw(); Check(session.IsComplete && player.Items.CountId(Soy) == 3, "Normal start after guide failed");
                    Click("GuideBounds"); Check(Open(), "Completed menu cannot read guide");
                    Click("NextBounds"); Click("GuideBounds");
                    menu.receiveLeftClick(menu.xPositionOnScreen + 500, menu.yPositionOnScreen + 170);
                    Check(session.IsComplete && Raw() == started && player.Items.CountId(Soy) == 3, "Guide return unlocked a second operation");
                }
                finally { Game1.activeClickableMenu = previousMenu; }
            });
            Test("missing ingredient preview reports deficit without request and retries when stock arrives", () =>
            {
                Fresh("BeanWorkshop"); Stock(); string before = Raw(); Item[] items = player.Items.ToArray();
                var previousMenu = Game1.activeClickableMenu; Game1.activeClickableMenu = null;
                try
                {
                    machine!.checkForAction(player);
                    var menu = Game1.activeClickableMenu as MachineMenu
                        ?? throw new InvalidOperationException("Native interaction did not create the production menu.");
                    var session = this.Helper.Reflection.GetField<MenuActionSession>(menu, "Session").GetValue();
                    menu.receiveLeftClick(menu.xPositionOnScreen + 500, menu.yPositionOnScreen + 170);
                    string feedback = this.Helper.Reflection.GetField<string>(menu, "Feedback").GetValue();
                    Check(feedback.Contains(ItemRegistry.GetDataOrErrorItem(Soy).DisplayName) && feedback.Contains("3")
                        && Raw() == before && player.Items.SequenceEqual(items) && !session.IsComplete
                        && this.Helper.Reflection.GetField<Request?>(session, "Pending").GetValue() is null,
                        "Missing preview omitted the actual deficit or acquired/mutated an operation");
                    Stock(ItemRegistry.Create(Soy, 3));
                    menu.receiveLeftClick(menu.xPositionOnScreen + 500, menu.yPositionOnScreen + 170);
                    Check(session.IsComplete && player.Items.CountId(Soy) == 0 && Raw() != before,
                        "A read-only shortage preview prevented same-menu retry");
                }
                finally { Game1.activeClickableMenu = previousMenu; }
            });
            Test("full inventory collection retries in the same menu and locks after success", () =>
            {
                Fresh("BeanWorkshop"); Stock(ItemRegistry.Create(Soy, 6));
                Check(service.Execute(machine!, player, Request(), Id + "_Recipe_Tofu").Applied, "Retry fixture start failed");
                Game1.timeOfDay += 200;
                // 保留下一批黄豆，但填满所有槽位且没有可叠加的豆腐；首次点击必须完整拒绝。
                Item[] full = Enumerable.Range(0, player.MaxItems).Select(_ => ItemRegistry.Create("(O)388", 999)).ToArray();
                full[0] = ItemRegistry.Create(Soy, 3);
                Stock(full);
                string before = Raw(); Item[] fullBefore = player.Items.ToArray();
                int[] quantitiesBefore = fullBefore.Select(i => i.Stack).ToArray();
                var previousMenu = Game1.activeClickableMenu;
                Game1.activeClickableMenu = null;
                try
                {
                    machine!.checkForAction(player);
                    var menu = Game1.activeClickableMenu as XiangyeShiling.Menus.MachineMenu;
                    Check(menu is not null, "Retry machine menu wasn't opened");
                    int x = menu!.xPositionOnScreen + 500, y = menu.yPositionOnScreen + 170;
                    menu.receiveLeftClick(x, y);
                    Check(Raw() == before && State().PhaseAt(MachineService.Now()) == MachinePhase.Ready
                        && player.Items.SequenceEqual(fullBefore) && player.Items.Select(i => i.Stack).SequenceEqual(quantitiesBefore)
                        && player.Items.CountId(Tofu) == 0, "Full inventory menu click changed the batch or inventory");
                    // 模拟玩家释放一个槽位；保持同一菜单和原请求，不能用重新打开掩盖永久锁定。
                    player.Items[player.MaxItems - 1] = null!;
                    Check(ReferenceEquals(Game1.activeClickableMenu, menu), "Failed collection replaced the menu");
                    menu.receiveLeftClick(x, y);
                    Check(State().CanMove && player.Items.CountId(Tofu) == 2 && player.Items.CountId(Soy) == 3
                        && player.Items.CountId("(O)388") == (player.MaxItems - 2) * 999,
                        "Same-menu retry didn't collect the whole batch exactly once");
                    string collected = Raw(); Item[] after = player.Items.ToArray();
                    int[] quantitiesAfter = after.Select(i => i.Stack).ToArray();
                    menu.receiveLeftClick(x, y);
                    Check(Raw() == collected && State().CanMove && player.Items.SequenceEqual(after)
                        && player.Items.Select(i => i.Stack).SequenceEqual(quantitiesAfter)
                        && player.Items.CountId(Soy) == 3 && player.Items.CountId(Tofu) == 2,
                        "Successful retry allowed another debit or collection in the same menu");
                }
                finally { Game1.activeClickableMenu = previousMenu; }
            });
            Test("real inventory callback failure compensates both sides", () =>
            {
                Fresh("BeanWorkshop"); Stock(ItemRegistry.Create(Soy, 3));
                string before = Raw(); Item input = player.Items[0]; bool once = true;
                void Fail(Inventory inventory, int index, Item previous, Item current)
                {
                    if (!once) return; once = false;
                    throw new InvalidOperationException("Expected harness injection: inventory slot callback");
                }
                // 同容量 OverwriteWith 逐槽 CopyFrom；OnInventoryReplaced 只在底层数组换代时触发。
                player.Items.OnSlotChanged += Fail;
                try { Check(!service.Execute(machine!, player, Request(), Id + "_Recipe_Tofu").Applied, "Fault reported success"); }
                finally { player.Items.OnSlotChanged -= Fail; }
                Check(!once && ReferenceEquals(input, player.Items[0]) && input.Stack == 3 && Raw() == before, "Compensation failed");
            });
            Test("real silent callback change is detected and compensated", () =>
            {
                Fresh("BeanWorkshop"); Stock(ItemRegistry.Create(Soy, 6));
                string before = Raw(); Item input = player.Items[0]; bool once = true;
                void Corrupt(Inventory inventory, int index, Item previous, Item current)
                {
                    if (!once || current is null) return; once = false; current.Stack = 99;
                }
                player.Items.OnSlotChanged += Corrupt;
                try { Check(!service.Execute(machine!, player, Request(), Id + "_Recipe_Tofu").Applied, "Corruption reported success"); }
                finally { player.Items.OnSlotChanged -= Corrupt; }
                Check(!once && ReferenceEquals(input, player.Items[0]) && input.Stack == 6 && Raw() == before, "Corruption rollback failed");
            });
            Test("interaction sale shop accepts cooked food and pays exactly once", () =>
            {
                string dishId = "(O)" + Id + "_ToonTofu";
                var dish = ItemRegistry.Create<SObject>(dishId, 2);
                Stock(dish);
                int moneyBefore = player.Money;
                var previousMenu = Game1.activeClickableMenu;
                try
                {
                    // BUG-2026-008：与人工 UI 路线共用售出商店 ID，不能只按商品售价推断商店接受该分类。
                    string shopId = InteractionScenario.SaleShopId;
                    var data = this.Helper.GameContent.Load<Dictionary<string, ShopData>>("Data/Shops")[shopId];
                    var menu = new ShopMenu(shopId, data, data.Owners.First(), playOpenSound: false);
                    Game1.activeClickableMenu = menu;
                    Check(menu.highlightItemToSell(dish), "Interaction sale shop rejects cooked food: " + shopId);
                    float saleMultiplier = this.Helper.Reflection.GetField<float>(menu, "sellPercentage").GetValue();
                    int expected = 2 * (int)(dish.sellToStorePrice(-1L) * saleMultiplier);
                    // 原生菜单有 250ms 防误触计时；让 update 正常到期，避免把立即点击拒绝误判为商店限制。
                    menu.update(new GameTime(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300)));
                    Check(menu.safetyTimer <= 0, "Sale menu safety timer didn't expire");
                    Point center = menu.inventory.inventory[0].bounds.Center;
                    menu.receiveLeftClick(center.X, center.Y);
                    Check(player.Items.CountId(dishId) == 0 && player.Money == moneyBefore + expected,
                        "Interaction sale didn't consume two dishes and pay the exact native price");
                    menu.receiveLeftClick(center.X, center.Y);
                    Check(player.Items.CountId(dishId) == 0 && player.Money == moneyBefore + expected,
                        "Empty sale slot generated a duplicate payment");
                }
                finally { Game1.activeClickableMenu = previousMenu; player.Money = moneyBefore; }
            });
            PlacementScenarios.Run(player, location, tile, service, Test);
            ToolRemovalScenarios.Run(player, location, tile, service, Test);
        }
        catch (Exception error) { failure = error.ToString(); this.Monitor.Log(failure, LogLevel.Error); }
        finally
        {
            player.Items.OverwriteWith(original);
            Game1.timeOfDay = clock;
            location.objects.Remove(tile);
            Check(player.Items.SequenceEqual(original), "Harness inventory restoration failed");
            this.Helper.Data.WriteJsonFile("result.json", new
            {
                Passed = passed, Failure = failure, Restored = true,
                // 只记录测试格坐标/原生预检结果；用于区分 BUG-010 的现场错误与生产放置失败。
                ToolFixtures = ToolRemovalScenarios.LastSelections
            });
        }
        if (failure is null && this.Settings.OpenPreview)
        {
            // 只打开脱离地图的空闲预览；执行器会拒绝对未放置对象提交，因此不留下测试物品。
            machine = ItemRegistry.Create<SObject>("(BC)" + Id + "_FermentationCrock");
            MachineService.Initialize(machine);
            machine.checkForAction(player);
        }
    }
}
