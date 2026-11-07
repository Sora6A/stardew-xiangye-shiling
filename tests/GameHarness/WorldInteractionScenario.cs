using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Tools;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// P1 真实世界输入夹具：制作、放置、开工、领取、挥镐、吸取和重放均由操作者的游戏输入完成。
/// 只允许固定 XYSP1UI/8624092802；准备资金、素材、已学图纸、站位和时钟，不生成库存/地图设备。
/// 不调用 Execute、checkForAction、placementAction、DoFunction、collect 或 updateChunks；读取原生预检不代替操作。
/// 内存不保存也不恢复。Tick 只冻结时钟、观察与报告；Idle 实际击落后仅一次移离磁吸范围，
/// 这是保留掉落供核验的准备，下一阶段必须实际走近自然拾取，不能自动追随玩家或驱动掉落实体。
/// </summary>
internal sealed class WorldInteractionScenario
{
    private const string Id = MachineService.Prefix;
    private const string FarmName = "XYSP1UI";
    private const ulong FixtureId = 8624092802;
    private const int CompletedStep = 20;
    private const string CorruptRaw = "{world-interaction-invalid";
    private static WorldInteractionScenario? ActiveObserver;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly List<string> Passed = new();
    private readonly List<ToolEvidence> Tools = new();
    private readonly List<PlacementEvidence> Placements = new();
    private readonly List<DropEvidence> DropObservations = new();
    private readonly List<InputEvidence> InputTrace = new();
    private readonly List<NativeHitEvidence> HitRecords = new();
    private readonly List<MovementEvidence> MovementTrail = new();
    // 有限保留短测按键发生时的状态，不能用下一秒已经失焦的报告解释操作者当时为何没移动。
    // 只采 WASD/Escape，聊天/文本订阅期间不记录；不读取文本、不改变或抑制输入。
    private readonly List<object> ControlInputEvents = new();
    private int OmittedControlEvents;
    private string? ControlObservationFailure;
    private Route[] Routes = Array.Empty<Route>();
    private GameLocation Location = null!;
    private Pickaxe Pickaxe = null!;
    private int LoadDelay = -1, Step, FixtureTime = 600, ReportTicks;
    private bool Pending, Started, Stopped, InventoryPrepared;
    private string? Failure;
    private Item[] StageInventory = Array.Empty<Item>();
    private int[] StageQuantities = Array.Empty<int>();
    private Dictionary<string, int> StageCounts = new();
    private KeyValuePair<Vector2, SObject>[] StageObjects = Array.Empty<KeyValuePair<Vector2, SObject>>();
    private Debris[] StageDebris = Array.Empty<Debris>();
    private string? StageRaw;
    private float StageStamina;
    private int StageTargetClicks, StageCrafted, StageActualToolHits, StageMovementInputs;
    private bool StageSwingObserved;
    private Vector2 LastObservedPosition;
    private string? ObserverFailure;
    private WallProbeEvidence? WallProbe;
    private SObject? StageCarrier;
    private string Instruction = "等待加载固定 WorldInteraction 测试副本。";
    private static Farmer Player => Game1.player;
    private static string MachineId(string key) => "(BC)" + Id + "_" + key;
    private static string RecipeId(string key) => Id + "_Recipe_" + key;
    private static string SaveName => FarmName + "_" + FixtureId;
    private Route Current => this.Step < 18 ? this.Routes[this.Step / 9] : this.Routes[this.Step - 18];
    private int Phase => this.Step < 18 ? this.Step % 9 : 9;
    private static float[] Point(Vector2 point) => new[] { point.X, point.Y };
    private static Vector2 TileAt(Vector2 pixels) => new((int)pixels.X / 64, (int)pixels.Y / 64);
    private static Dictionary<string, int> Counts() => Player.Items.Where(i => i is not null)
        .GroupBy(i => i.QualifiedItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Stack));

    public WorldInteractionScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        this.Helper = helper; this.Monitor = monitor;
        Require(config.Enabled && config.Scenario == "WorldInteraction" && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "WorldInteraction requires XYSP1UI and fixed ID 8624092802.");
        // 只读记录真实鼠标发起点；不会 Suppress 输入，也不会替操作者调用任何世界动作。
        helper.Events.Input.ButtonPressed += this.ObserveInput;
        // BUG-2026-011：按钮事件与任意体力下降不能拼接成命中证据，须观察原生 DoFunction 的实际目标。
        // 仅本地夹具挂载只读 prefix/postfix；不调用原生动作、不改参数/返回值，生产模组不安装这些观察器。
        ActiveObserver = this;
        new Harmony(Id + ".GameHarness.WorldInteraction").Patch(
            AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction)),
            prefix: new HarmonyMethod(typeof(WorldInteractionScenario), nameof(BeforeNativeHit)),
            postfix: new HarmonyMethod(typeof(WorldInteractionScenario), nameof(AfterNativeHit)));
    }

    public void QueueLoad() => this.LoadDelay = 180;
    public void OnLoaded() => this.Pending = true;

    public void Tick()
    {
        if (this.Stopped)
        {
            // 致命错误只留一次 Failure；仍可读现场，但绝不反复改动身份不匹配的世界。
            if (this.Started && IsFixedWorld() && ReferenceEquals(Player.currentLocation, this.Location))
                Game1.timeOfDay = this.FixtureTime;
            if (++this.ReportTicks >= 60) { this.ReportTicks = 0; this.Report(); }
            return;
        }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                this.LoadDelay = -1;
                Require(!Context.IsWorldReady, "Refuse to replace an active world.");
                Require(File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)), "Fixed UI fixture is not installed.");
                Game1.activeClickableMenu = null;
                SaveGame.Load(SaveName);
            }
            if (this.Pending && Context.IsWorldReady)
            {
                this.Pending = false;
                Require(!this.Started, "Restart the process to rerun WorldInteraction.");
                this.VerifyIdentity();
                this.Setup(); this.Started = true; this.Prepare();
            }
            if (!this.Started || !Context.IsWorldReady) return;
            Require(this.ObserverFailure is null, "Read-only native tool observer failed: " + this.ObserverFailure);
            this.VerifyIdentity();
            Require(ReferenceEquals(Player.currentLocation, this.Location), "Stay in the prepared FarmHouse for this route.");
            Game1.timeOfDay = this.FixtureTime;
            if (!this.Stopped && this.Step < CompletedStep)
            {
                if (this.IsToolPhase() && ReferenceEquals(Player.CurrentTool, this.Pickaxe) && Player.UsingTool)
                    this.StageSwingObserved = true;
                if (this.Phase == 6) this.ObserveActualDrop();
                if (this.Phase == 7 && Player.Position != this.LastObservedPosition)
                {
                    this.MovementTrail.Add(new(this.Step, this.Current.Key, Point(Player.Position), Point(Player.Tile)));
                    this.LastObservedPosition = Player.Position;
                }
            }
            // 只更新现场坐标/实体，不把定时报告当作阶段通过，也不抹掉之前的 Failure。
            if (++this.ReportTicks >= 60) { this.ReportTicks = 0; this.Report(); }
        }
        catch (Exception error)
        {
            this.Pending = false; this.LoadDelay = -1; this.Stopped = true; this.Fail(error);
        }
    }

    private static bool IsFixedWorld() => Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && Player.farmName.Value == FarmName && Game1.uniqueIDForThisGame == FixtureId
        && Game1.GetSaveGameName() == FarmName;
    private void VerifyIdentity() => Require(IsFixedWorld(), "Refuse to mutate any save except fixed XYSP1UI/8624092802.");

    private void Setup()
    {
        Require(Player.currentLocation.Name == "FarmHouse" && !Game1.eventUp && !Player.isRidingHorse(),
            "WorldInteraction requires the unmounted local player in FarmHouse without an event.");
        this.Location = Player.currentLocation;
        this.VerifyWallPreparationRegression();
        var service = this.Helper.ModRegistry.GetApi(Id) as MachineService
            ?? throw new InvalidOperationException("Main machine API is unavailable.");
        this.Routes = new[]
        {
            new Route("BeanWorkshop", "豆坊", service.Recipes.Single(r => r.Machine == MachineKind.BeanWorkshop),
                new Dictionary<string, int> { ["(O)388"] = 50, ["(O)390"] = 20, ["(O)334"] = 1 }),
            new Route("FermentationCrock", "发酵缸", service.Recipes.Single(r => r.Machine == MachineKind.FermentationCrock),
                new Dictionary<string, int> { ["(O)388"] = 30, ["(O)390"] = 40 })
        };
        Require(this.Routes.All(r => Player.Items.CountId(MachineId(r.Key)) == 0
            && this.Location.objects.Values.All(o => o.QualifiedItemId != MachineId(r.Key))),
            "Use a fresh fixture without either device in inventory or FarmHouse.");
        var data = this.Helper.GameContent.Load<Dictionary<string, string>>("Data/CraftingRecipes");
        foreach (Route route in this.Routes)
        {
            string[] tokens = data[RecipeId(route.Key)].Split('/')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var costs = new Dictionary<string, int>();
            for (int i = 0; i < tokens.Length; i += 2) costs.Add(tokens[i], int.Parse(tokens[i + 1]));
            Require(SameCounts(costs, route.CraftingCosts), route.Name + ": crafting costs changed; update the fixture explicitly.");
            if (!Player.craftingRecipes.ContainsKey(RecipeId(route.Key))) Player.craftingRecipes.Add(RecipeId(route.Key), 0);
        }
        this.Pickaxe = new Pickaxe();
        var stock = new Item[Player.MaxItems];
        stock[0] = this.Pickaxe; stock[1] = ItemRegistry.Create("(O)388", 80);
        stock[2] = ItemRegistry.Create("(O)390", 60); stock[3] = ItemRegistry.Create("(O)334");
        stock[4] = ItemRegistry.Create("(O)" + Id + "_Soybean", 6); stock[5] = ItemRegistry.Create("(O)262", 2);
        Player.Items.OverwriteWith(stock); Player.Money = 20000;
        this.InventoryPrepared = true;
        Player.UsingTool = false; Player.CanMove = true; Player.isInBed.Value = false;
        Require(2 - Player.MiningLevel * 0.1f > 0, "Tool proof requires a positive native pickaxe stamina cost.");
        Player.Stamina = Player.MaxStamina;
        Vector2 origin = Player.Tile + new Vector2(1, 0);
        var reserved = new HashSet<Vector2>();
        foreach (Route route in this.Routes)
        {
            this.SelectSite(route, origin, reserved);
            reserved.Add(route.Tile); reserved.Add(route.Standing);
        }
        this.PositionAt(this.Routes[0]);
        Game1.timeOfDay = this.FixtureTime;
    }

    /// <summary>探针只用于只读放置预检，从未放进背包或地图；实际设备只能由 CraftingPage 产生。</summary>
    private void SelectSite(Route route, Vector2 origin, HashSet<Vector2> reserved)
    {
        var probe = ItemRegistry.Create<SObject>(MachineId(route.Key));
        Vector2[] offsets = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };
        int[] directions = { 1, 3, 2, 0 };
        for (int radius = 0; radius <= 8; radius++)
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
                    Vector2 tile = origin + new Vector2(x, y);
                    if (!this.Location.isTileOnMap(tile) || reserved.Contains(tile) || this.Location.objects.ContainsKey(tile)) continue;
                    for (int approach = 0; approach < offsets.Length; approach++)
                    {
                        Vector2 standing = tile + offsets[approach];
                        if (reserved.Contains(standing) || !this.CanStand(standing)) continue;
                        Player.setTileLocation(standing); Player.faceDirection(directions[approach]);
                        if (Player.Tile != standing || !Utility.playerCanPlaceItemHere(this.Location, probe,
                            (int)tile.X * 64 + 32, (int)tile.Y * 64 + 32, Player)) continue;
                        route.Tile = tile; route.Standing = standing; route.Facing = directions[approach]; return;
                    }
                }
        throw new InvalidOperationException(route.Name + ": no free native-placeable site with an adjacent standing tile.");
    }

    private bool CanStand(Vector2 tile)
    {
        // BUG-2026-012：放置预检可接受农舍墙后的 Back 格，不能据此认定玩家能看见/走到该格。
        // 按原生室内空点选择补充墙面排除，并限制在原生地板矩形；不调用有碰撞副作用的查询。
        if (this.Location is FarmHouse house && (house.isTileOnWall((int)tile.X, (int)tile.Y)
            || !house.getFloors().Any(floor => floor.Contains((int)tile.X, (int)tile.Y)))) return false;
        return this.Location.CanItemBePlacedHere(tile, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
            && !this.Location.farmers.Any(f => !ReferenceEquals(f, Player)
                && f.GetBoundingBox().Intersects(new Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64)));
    }

    /// <summary>
    /// BUG-2026-012 的原生组件负例：固定副本的 (8,2) 是原生物品查询允许的墙格。
    /// 在任何素材/图纸准备之前验证实际选址判据拒绝它；不是手写地板结果或纯单元替代。
    /// 房屋布局若变化需换独立副本并明确更新本回归，不能跳过或把正式农舍拿来调整。
    /// </summary>
    private void VerifyWallPreparationRegression()
    {
        Require(this.Location is FarmHouse, "Wall regression requires the fixed FarmHouse.");
        var house = (FarmHouse)this.Location;
        Vector2 wall = new(8, 2);
        bool nativeAllows = this.Location.CanItemBePlacedHere(wall, collisionMask: CollisionMask.All & ~CollisionMask.Farmers);
        bool nativeWall = house.isTileOnWall(8, 2);
        bool eligible = this.CanStand(wall);
        this.WallProbe = new(Point(wall), nativeAllows, nativeWall, eligible);
        Require(nativeAllows && nativeWall, "Fixed house wall negative control changed; update the fixture explicitly.");
        Require(!eligible, "BUG-2026-012: observation preparation accepted the native house wall (8,2).");
    }

    private void PositionAt(Route route)
    {
        Require(this.CanStand(route.Standing), "Prepared standing tile became occupied.");
        Player.setTileLocation(route.Standing); Player.faceDirection(route.Facing);
        Require(Player.Tile == route.Standing, "StandingPixel alignment failed.");
    }

    private void Prepare()
    {
        if (this.Step == CompletedStep)
        {
            this.Instruction = "20 步完成；退出整个游戏，不保存，禁用夹具并核对副本文件哈希。";
            this.Report(); Game1.addHUDMessage(new HUDMessage(this.Instruction, HUDMessage.newQuest_type)); return;
        }
        Route route = this.Current;
        int phase = this.Phase;
        this.StageTargetClicks = 0; this.StageActualToolHits = 0; this.StageMovementInputs = 0;
        this.StageSwingObserved = false; this.StageCarrier = null;
        if (phase != 7) this.PositionAt(route);
        switch (phase)
        {
            case 0:
                Player.CurrentToolIndex = this.EmptySlot();
                this.StageCrafted = Player.craftingRecipes[RecipeId(route.Key)];
                int w = 800 + IClickableMenu.borderWidth * 2, h = 600 + IClickableMenu.borderWidth * 2;
                Vector2 top = Utility.getTopLeftPositionForCenteringOnScreen(w, h);
                Game1.activeClickableMenu = new CraftingPage((int)top.X, (int)top.Y, w, h, cooking: false, standaloneMenu: true);
                this.Instruction = route.Name + "：实际制作一台，放入背包后关闭制作菜单，再按 F8/中键。图纸已由夹具准备，本轮不算购买验收。"; break;
            case 1:
            case 8:
                this.StageCarrier = Player.Items.OfType<SObject>().Single(i => i.QualifiedItemId == MachineId(route.Key));
                Player.CurrentToolIndex = Player.Items.ToList().FindIndex(i => ReferenceEquals(i, this.StageCarrier));
                Require(Utility.playerCanPlaceItemHere(this.Location, this.StageCarrier, (int)route.Tile.X * 64 + 32,
                    (int)route.Tile.Y * 64 + 32, Player), "Actual carried device failed native placement precheck.");
                this.Instruction = route.Name + (phase == 1 ? "：手持刚制作的设备" : "：手持真实拾取的设备")
                    + "，左键目标足迹格放置一台；完成后 F8/中键。"; break;
            case 2:
                Player.CurrentToolIndex = this.EmptySlot();
                this.Instruction = route.Name + "：右键目标设备打开菜单，实际点击开始加工；关闭菜单后 F8/中键。"; break;
            case 3:
            case 4:
            case 6:
            case 9:
                if (phase == 4)
                {
                    long ready = ReadState(route.Machine!).Batch!.ReadyAt;
                    long sameDay = ready - (long)Game1.Date.TotalDays * 1440;
                    Require(sameDay is >= 0 and < 1440, "Fixture completion crosses day; update clock preparation explicitly.");
                    this.FixtureTime = (int)(sameDay / 60 * 100 + sameDay % 60);
                    Game1.timeOfDay = this.FixtureTime;
                    Require(ReadState(route.Machine!).PhaseAt(MachineService.Now()) == MachinePhase.Ready, "Ready clock preparation failed.");
                }
                if (phase == 9)
                {
                    Require(ReadState(route.Machine!).CanMove, "Corrupt preparation requires the actual replacement to be idle.");
                    route.RestoreIdleRaw = route.Machine!.modData[MachineService.StateKey];
                    route.Machine.modData[MachineService.StateKey] = CorruptRaw;
                }
                Player.CurrentToolIndex = Player.Items.ToList().FindIndex(i => ReferenceEquals(i, this.Pickaxe));
                Player.Stamina = Player.MaxStamina;
                this.Instruction = route.Name + "：" + (phase == 3 ? "加工中" : phase == 4 ? "待领取" : phase == 9 ? "夹具损坏状态" : "已领取空闲")
                    + "，选镐后左键目标足迹格挥镐一次，等待动画结束再 F8/中键。"
                    + (phase == 6 ? "击落后夹具只移远一次保留真实掉落；下一步须按方向键走近拾取。" : "应保留设备和原状态。"); break;
            case 5:
                Player.CurrentToolIndex = this.EmptySlot();
                this.Instruction = route.Name + "：右键目标设备，实际领取整批产品；关闭菜单后 F8/中键。"; break;
            case 7:
                Player.CurrentToolIndex = this.EmptySlot();
                this.Instruction = route.Name + "：只用实际 W/A/S/D 或方向键走近报告的掉落坐标，等待自然吸取；不要左键挥镐。吸取后 F8/中键。"; break;
        }
        this.StageInventory = Player.Items.ToArray(); this.StageQuantities = this.StageInventory.Select(i => i?.Stack ?? 0).ToArray();
        this.StageCounts = Counts(); this.StageObjects = this.Location.objects.Pairs.ToArray(); this.StageDebris = this.Location.debris.ToArray();
        this.StageRaw = route.Machine?.modData.GetValueOrDefault(MachineService.StateKey);
        this.StageStamina = Player.Stamina;
        this.LastObservedPosition = Player.Position;
        this.Instruction += " 目标=(" + route.Tile.X + "," + route.Tile.Y + ")，世界像素中心=("
            + (route.Tile.X * 64 + 32) + "," + (route.Tile.Y * 64 + 32) + ")，手持槽位=" + (Player.CurrentToolIndex + 1) + "。";
        this.Report(); Game1.addHUDMessage(new HUDMessage(this.Instruction, HUDMessage.newQuest_type));
    }

    public void Advance()
    {
        if (!this.Started || this.Stopped || this.Step >= CompletedStep) return;
        try
        {
            this.VerifyIdentity();
            Require(ReferenceEquals(Player.currentLocation, this.Location) && Game1.activeClickableMenu is null
                && !Player.UsingTool && !Player.isEating, "Close the menu and wait for the tool/eating animation before advancing.");
            Route route = this.Current; int phase = this.Phase;
            switch (phase)
            {
                case 0:
                    this.RequireCounts(route.CraftingCosts.Select(p => (p.Key, -p.Value)).Append((MachineId(route.Key), 1)));
                    Require(Player.craftingRecipes[RecipeId(route.Key)] == this.StageCrafted + 1
                        && this.MapMatches(null, null) && this.DebrisUnchanged(), "Craft exactly one device into inventory; do not drop/place it before this check."); break;
                case 1:
                case 8:
                    Require(this.StageTargetClicks > 0, "A real mouse placement on the selected tile wasn't observed.");
                    this.RequireCounts(new[] { (MachineId(route.Key), -1) });
                    Require(this.Location.objects.TryGetValue(route.Tile, out SObject? placed)
                        && placed.QualifiedItemId == MachineId(route.Key) && !ReferenceEquals(placed, this.StageCarrier)
                        && this.StageCarrier!.Stack == 0 && this.MapMatches(route.Tile, placed) && this.DebrisUnchanged()
                        && this.Location.objects.Values.Count(o => o.QualifiedItemId == MachineId(route.Key)) == 1, "Native placement did not consume one carrier and create exactly one device.");
                    MachineState state = ReadState(placed!);
                    Require(state.CanMove && state.Revision == 0 && state.InstanceId != Guid.Empty
                        && (phase == 1 || state.InstanceId != route.FirstInstanceId), "Placement identity wasn't a new idle instance.");
                    route.Machine = placed;
                    if (phase == 1) route.FirstInstanceId = state.InstanceId; else route.ReplacementInstanceId = state.InstanceId;
                    this.Placements.Add(new(route.Key, phase == 1 ? "Initial" : "Replacement", Point(route.Tile),
                        phase == 1 ? null : route.FirstInstanceId, state.InstanceId, state.Revision, true)); break;
                case 2:
                    Require(this.StageTargetClicks > 0, "A real world click opening the target machine wasn't observed.");
                    MachineState started = ReadState(this.RequireMachine(route));
                    Require(started.InstanceId == route.FirstInstanceId && started.Revision == 1
                        && started.PhaseAt(MachineService.Now()) == MachinePhase.Processing && started.Batch?.RecipeId == route.Recipe.Id
                        && started.Batch.Output == route.Recipe.Output && started.Batch.ReadyAt - started.Batch.StartedAt == route.Recipe.Minutes
                        && route.Recipe.Inputs.All(i => started.Batch.Inputs.Where(s => s.ItemId == i.ItemId).Sum(s => s.Quantity) == i.Quantity),
                        "Native menu didn't start the exact complete batch.");
                    this.RequireCounts(route.Recipe.Inputs.Select(i => (i.ItemId, -i.Quantity)));
                    Require(this.MapMatches(null, null) && this.DebrisUnchanged(), "Start changed unrelated map/drop state."); break;
                case 3:
                case 4:
                case 9:
                    this.RequireToolProof(route);
                    Require(ReferenceEquals(this.RequireMachine(route), route.Machine) && route.Machine!.modData[MachineService.StateKey] == this.StageRaw
                        && this.InventoryUnchanged() && this.MapMatches(null, null) && this.DebrisUnchanged(), "Protected tool hit changed target JSON/reference, inventory or drops.");
                    if (phase != 9) Require(ReadState(route.Machine!).PhaseAt(MachineService.Now())
                        == (phase == 3 ? MachinePhase.Processing : MachinePhase.Ready), "Protected phase was wrong.");
                    this.AddToolEvidence(route, true);
                    if (phase == 9) route.Machine!.modData[MachineService.StateKey] = route.RestoreIdleRaw!;
                    break;
                case 5:
                    MachineState collected = ReadState(this.RequireMachine(route));
                    Require(this.StageTargetClicks > 0 && collected.CanMove && collected.InstanceId == route.FirstInstanceId && collected.Revision == 2,
                        "Native collection didn't leave the same device idle.");
                    this.RequireCounts(new[] { (route.Recipe.Output.ItemId, route.Recipe.Output.Quantity) });
                    Require(this.MapMatches(null, null) && this.DebrisUnchanged(), "Collection changed unrelated map/drop state."); break;
                case 6:
                    this.RequireToolProof(route);
                    Require(route.Drop is not null && route.DropEvidence!.DropObserved && route.DropEvidence.RelocatedForObservation
                        && !this.Location.objects.ContainsKey(route.Tile) && this.MapMatches(route.Tile, null)
                        && this.InventoryUnchanged() && this.NewDrops().Length == 1 && ReferenceEquals(this.NewDrops()[0], route.Drop)
                        && route.Drop.Chunks.Count == 1, "Idle hit didn't leave one real item Debris for the separate walking stage.");
                    this.AddToolEvidence(route, false); break;
                case 7:
                    Require(route.Drop is not null && route.DropEvidence!.RelocatedForObservation
                        && this.StageMovementInputs > 0 && this.MovementTrail.Any(m => m.Step == this.Step)
                        && Player.Position != route.RelocatedPosition && Player.Tile != route.RelocatedTile
                        && !this.Location.debris.Any(d => ReferenceEquals(d, route.Drop))
                        && route.Drop.Chunks.Count == 0 && this.MapMatches(null, null)
                        && this.DebrisUnchangedExceptRemoved(route.Drop), "Walk near and let normal game updates consume the real dropped entity.");
                    this.RequireCounts(new[] { (MachineId(route.Key), 1) });
                    SObject picked = Player.Items.OfType<SObject>().Single(i => i.QualifiedItemId == MachineId(route.Key));
                    Require(ReferenceEquals(picked, route.DropPayload) && picked.Stack == 1, "Pickup didn't retain the actual single drop payload.");
                    route.DropEvidence!.PickupObserved = true; route.DropEvidence.PickupPlayerTile = Point(Player.Tile); break;
            }
            string passed = route.Key + "/" + PhaseName(phase);
            this.Passed.Add(passed); this.Monitor.Log("PASS WORLD UI " + this.Step + ": " + passed, LogLevel.Info);
            this.Failure = null; this.Step++; this.Prepare();
        }
        catch (Exception error) { this.Fail(error); }
    }

    private void ObserveInput(object? sender, ButtonPressedEventArgs e)
    {
        this.ObserveControlInput(e);
        if (!this.Started || this.Stopped || this.Step >= CompletedStep || !Context.IsWorldReady
            || Game1.activeClickableMenu is not null || e.IsSuppressed()) return;
        try
        {
            this.VerifyIdentity(); Route route = this.Current;
            if (!ReferenceEquals(Player.currentLocation, this.Location)) return;
            if (this.Phase == 7 && e.Button is SButton.W or SButton.A or SButton.S or SButton.D
                or SButton.Up or SButton.Down or SButton.Left or SButton.Right)
            {
                this.StageMovementInputs++;
                this.InputTrace.Add(new(this.Step, route.Key, "WalkPickup", e.Button.ToString(),
                    Point(TileAt(e.Cursor.AbsolutePixels)), Point(Player.Tile), Point(Player.Tile), Player.CurrentToolIndex, Player.Stamina));
                return;
            }
            if (e.Button is not (SButton.MouseLeft or SButton.MouseRight)) return;
            Vector2 click = e.Cursor.AbsolutePixels, requested = TileAt(click);
            if (requested != route.Tile || !ReferenceEquals(Player.currentLocation, this.Location)
                || !Utility.withinRadiusOfPlayer((int)click.X, (int)click.Y, 1, Player)) return;
            bool tool = this.IsToolPhase() && e.Button == SButton.MouseLeft && !Player.UsingTool && Player.CanMove
                && ReferenceEquals(Player.CurrentTool, this.Pickaxe);
            bool place = this.Phase is 1 or 8 && ReferenceEquals(Player.ActiveObject, this.StageCarrier);
            bool interact = this.Phase is 2 or 5 && e.Button == SButton.MouseRight;
            Vector2 resolved = TileAt(Player.GetToolLocation(click));
            if (!(place || interact || (tool && resolved == route.Tile))) return;
            this.StageTargetClicks++;
            this.InputTrace.Add(new(this.Step, route.Key, PhaseName(this.Phase), e.Button.ToString(),
                Point(requested), Point(resolved), Point(Player.Tile), Player.CurrentToolIndex, Player.Stamina));
        }
        catch (Exception error) { this.Stopped = true; this.Fail(error); }
    }

    /// <summary>
    /// 只读诊断，不参与20步验收：菜单可用不等于角色能移动，也不能用存档里的键位推断运行时绑定。
    /// 事件状态是在SMAPI回调时依次读取，可能早于本帧原生控制更新，不是操作系统输入或原子快照。
    /// 隐私门禁复用已有输入观测策略；最多保留32条，超限只计数，不让长时间等待无限增大报告。
    /// </summary>
    private void ObserveControlInput(ButtonPressedEventArgs e)
    {
        try
        {
            if (!this.Started || this.Stopped || !IsFixedWorld() || !ReferenceEquals(Player.currentLocation, this.Location)
                || e.Button is not (SButton.W or SButton.A or SButton.S or SButton.D or SButton.Escape)) return;
            if (!InputObservationPolicy.CanRecordKeys(Game1.IsChatting, Game1.keyboardDispatcher?.Subscriber is not null)) return;
            if (this.ControlInputEvents.Count >= 32) { this.OmittedControlEvents++; return; }
            this.ControlInputEvents.Add(new { Tick = Game1.ticks, this.Step, Button = e.Button.ToString(),
                SuppressedAtCallback = e.IsSuppressed(), State = CaptureControlState() });
        }
        // 诊断读数失败不抑制输入、不伪造阶段失败或通过；原始异常单独留档供定位。
        catch (Exception error) when (error is not OutOfMemoryException)
        { this.ControlObservationFailure ??= error.ToString(); }
    }

    private static object CaptureControlState()
    {
        bool recordKeys = InputObservationPolicy.CanRecordKeys(Game1.IsChatting, Game1.keyboardDispatcher?.Subscriber is not null);
        Keys[] allowed = { Keys.W, Keys.A, Keys.S, Keys.D, Keys.Escape };
        string[] Filter(KeyboardState state) => allowed.Where(state.IsKeyDown).Select(k => k.ToString()).ToArray();
        return new
        {
            Tick = Game1.ticks, Active = Game1.game1.IsActive, ActiveWithoutOverlay = Game1.game1.IsActiveNoOverlay,
            KeyboardFocus = Game1.game1.HasKeyboardFocus(), Menu = Game1.activeClickableMenu?.GetType().Name,
            Game1.dialogueUp, Game1.freezeControls, Game1.globalFade, Game1.paused, Game1.pauseTime, Game1.newDay, Game1.eventUp,
            LocationRequestPending = Game1.locationRequest is not null, Player.CanMove, Player.UsingTool,
            Player.freezePause, Player.noMovementPause, Eating = Player.isEating, Sitting = Player.IsSitting(),
            InBed = Player.isInBed.Value, AnimationPaused = Player.FarmerSprite.PauseForSingleAnimation,
            ControllerType = Player.controller?.GetType().FullName, MovementDirections = Player.movementDirections.ToArray(),
            Position = Point(Player.Position), Tile = Point(Player.Tile), Player.speed, Player.addedSpeed,
            Game1.options.gamepadControls,
            KeysRedacted = !recordKeys,
            MonoGameKeys = recordKeys ? Filter(Keyboard.GetState()) : null,
            InputStateKeys = recordKeys ? Filter(Game1.input.GetKeyboardState()) : null,
            ChatFilteredKeys = recordKeys ? Filter(Game1.GetKeyboardState()) : null,
            PreviousControlKeys = recordKeys ? Filter(Game1.oldKBState) : null,
            MoveUpKeys = Game1.options.moveUpButton.Select(b => b.key.ToString()).ToArray(),
            MoveRightKeys = Game1.options.moveRightButton.Select(b => b.key.ToString()).ToArray(),
            MoveDownKeys = Game1.options.moveDownButton.Select(b => b.key.ToString()).ToArray(),
            MoveLeftKeys = Game1.options.moveLeftButton.Select(b => b.key.ToString()).ToArray()
        };
    }

    private static void BeforeNativeHit(Farmer who, out float __state)
    {
        // __state 只携带观察快照，不影响原生参数。任何观察异常均不能抛入游戏的工具调用。
        __state = 0;
        try { __state = who?.Stamina ?? 0; }
        catch (Exception error) { if (ActiveObserver is { } observer) observer.ObserverFailure = error.ToString(); }
    }
    private static void AfterNativeHit(Pickaxe __instance, GameLocation location, int x, int y, Farmer who, float __state)
    {
        try
        {
            if (ActiveObserver is not { } observer || !observer.Started || observer.Stopped
                || observer.Step >= CompletedStep || !IsFixedWorld() || !observer.IsToolPhase()) return;
            bool actor = ReferenceEquals(who, Player), tool = ReferenceEquals(__instance, observer.Pickaxe);
            bool target = actor && tool && ReferenceEquals(location, observer.Location)
                && new Vector2(x / 64, y / 64) == observer.Current.Tile;
            observer.HitRecords.Add(new(observer.Step, observer.Current.Key, PhaseName(observer.Phase), x, y,
                Point(new Vector2(x / 64, y / 64)), actor ? Point(who.Tile) : null, actor, tool, target, __state, who.Stamina));
            if (target) observer.StageActualToolHits++;
        }
        catch (Exception error) { if (ActiveObserver is { } observer) observer.ObserverFailure = error.ToString(); }
    }

    private void ObserveActualDrop()
    {
        Route route = this.Current;
        if (route.Drop is not null || this.Location.objects.ContainsKey(route.Tile)) return;
        this.RequireToolProof(route);
        Debris[] added = this.NewDrops();
        Require(added.Length == 1 && added[0].itemId.Value == MachineId(route.Key) && added[0].Chunks.Count == 1
            && added[0].item?.QualifiedItemId == MachineId(route.Key) && added[0].item!.Stack == 1
            && this.InventoryUnchanged() && this.MapMatches(route.Tile, null), "Actual idle removal didn't produce exactly one untouched device Debris.");
        Debris drop = added[0]; Vector2 dropPosition = DropPosition(drop);
        Vector2 far = this.FindObservationTile(dropPosition);
        route.Drop = drop; route.DropPayload = drop.item;
        Player.setTileLocation(far);
        route.RelocatedPosition = Player.Position;
        route.RelocatedTile = Player.Tile;
        Require(OutsideMagnet(dropPosition), "Observation preparation didn't leave normal magnetic range.");
        route.DropEvidence = new DropEvidence
        {
            MachineKey = route.Key, DropObserved = true, RelocatedForObservation = true,
            ObservedDropPosition = Point(dropPosition), ObservationPlayerTile = Point(Player.Tile),
            AppliedMagneticRadius = Player.GetAppliedMagneticRadius()
        };
        this.DropObservations.Add(route.DropEvidence);
        this.Instruction = route.Name + "：真实掉落已观察，玩家仅移远一次。等待镐动画结束后 F8/中键进入实际走近拾取阶段。";
        this.Report();
    }

    private Vector2 FindObservationTile(Vector2 dropPosition)
    {
        Vector2 start = this.Current.Standing;
        Require(this.CanStand(start), "The original adjacent standing tile is no longer valid.");
        var pending = new Queue<Vector2>();
        var visited = new HashSet<Vector2> { start };
        var reachable = new List<Vector2>();
        Vector2[] neighbors = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };
        pending.Enqueue(start);
        // 只读四邻 BFS 保留从原站位连通的空地；不可用隔墙空格或不可达房间保留掉落。
        // 不清家具/设备、不改变地图、不调用 isCollidingPosition，也不移动玩家来试探路径。
        while (pending.Count > 0)
        {
            Vector2 tile = pending.Dequeue();
            reachable.Add(tile);
            foreach (Vector2 offset in neighbors)
            {
                Vector2 next = tile + offset;
                if (visited.Add(next) && this.CanStand(next)) pending.Enqueue(next);
            }
        }
        var candidates = from tile in reachable
                         let center = (tile + new Vector2(0.5f)) * 64
                         where Math.Abs(center.X - dropPosition.X) > Player.GetAppliedMagneticRadius() + 128
                             || Math.Abs(center.Y - dropPosition.Y) > Player.GetAppliedMagneticRadius() + 128
                         orderby Vector2.DistanceSquared(center, dropPosition)
                         select tile;
        Require(candidates.Any(), "No reachable floor observation tile outside magnetic range; preserve the failed scene.");
        return candidates.First();
    }

    private bool OutsideMagnet(Vector2 dropPosition) => Math.Abs(Player.StandingPixel.X - dropPosition.X) > Player.GetAppliedMagneticRadius()
        || Math.Abs(Player.StandingPixel.Y - dropPosition.Y) > Player.GetAppliedMagneticRadius();
    private static Vector2 DropPosition(Debris drop) => drop.Chunks.Count == 0 ? Vector2.Zero
        : new Vector2(drop.Chunks.Average(c => c.position.Value.X), drop.Chunks.Average(c => c.position.Value.Y)) + new Vector2(32);
    private bool IsToolPhase() => this.Phase is 3 or 4 or 6 or 9;
    private void RequireToolProof(Route route) => Require(WorldToolProof.IsSatisfied(this.StageTargetClicks, this.StageActualToolHits,
        this.StageStamina, Player.Stamina, ReferenceEquals(Player.CurrentTool, this.Pickaxe)),
        route.Name + ": no completed native pickaxe DoFunction hit on the selected target was observed.");
    private void AddToolEvidence(Route route, bool protectedHit) => this.Tools.Add(new(route.Key, PhaseName(this.Phase),
        this.StageTargetClicks, this.StageActualToolHits, this.StageSwingObserved, this.StageStamina, Player.Stamina, protectedHit,
        protectedHit && route.Machine!.modData[MachineService.StateKey] == this.StageRaw, this.InventoryUnchanged()));
    private int EmptySlot()
    {
        int slot = Player.Items.ToList().FindIndex(i => i is null); Require(slot >= 0, "Fixture needs an empty selectable slot."); return slot;
    }
    private SObject RequireMachine(Route route)
    {
        Require(this.Location.objects.TryGetValue(route.Tile, out SObject? machine) && ReferenceEquals(machine, route.Machine),
            "Target machine moved or was replaced unexpectedly."); return machine!;
    }
    private static MachineState ReadState(SObject machine)
    {
        Require(MachineService.TryRead(machine, out MachineState? state), "Target state is unreadable."); return state!;
    }
    private bool InventoryUnchanged() => Player.Items.SequenceEqual(this.StageInventory)
        && Player.Items.Select(i => i?.Stack ?? 0).SequenceEqual(this.StageQuantities);
    private Debris[] NewDrops() => this.Location.debris.Where(d => !this.StageDebris.Any(o => ReferenceEquals(o, d))).ToArray();
    private bool DebrisUnchanged() => this.Location.debris.SequenceEqual(this.StageDebris);
    private bool DebrisUnchangedExceptRemoved(Debris drop) => this.Location.debris.SequenceEqual(this.StageDebris.Where(d => !ReferenceEquals(d, drop)));
    private bool MapMatches(Vector2? changedTile, SObject? replacement)
    {
        int removed = changedTile is { } tile && this.StageObjects.Any(p => p.Key == tile) ? 1 : 0;
        if (this.Location.objects.Count() != this.StageObjects.Length - removed + (replacement is null ? 0 : 1)) return false;
        return this.StageObjects.All(p => changedTile == p.Key || (this.Location.objects.TryGetValue(p.Key, out SObject? value)
            && ReferenceEquals(value, p.Value)));
    }
    private void RequireCounts(IEnumerable<(string Id, int Delta)> changes)
    {
        var expected = new Dictionary<string, int>(this.StageCounts);
        foreach ((string id, int delta) in changes)
        {
            int count = expected.GetValueOrDefault(id) + delta;
            Require(count >= 0, "Fixture has insufficient expected inventory.");
            if (count == 0) expected.Remove(id); else expected[id] = count;
        }
        Require(SameCounts(Counts(), expected), "Actual inventory quantities differ from the complete expected change.");
    }
    private static bool SameCounts(Dictionary<string, int> left, Dictionary<string, int> right) => left.Count == right.Count
        && left.All(p => right.TryGetValue(p.Key, out int quantity) && quantity == p.Value);
    private static string PhaseName(int phase) => phase switch
    {
        0 => "Craft", 1 => "Place", 2 => "Start", 3 => "ProcessingPickaxe", 4 => "ReadyPickaxe",
        5 => "Collect", 6 => "IdleKnockDown", 7 => "WalkPickup", 8 => "Replace", 9 => "CorruptPickaxe", _ => "Complete"
    };
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private void Fail(Exception error)
    {
        this.Failure = error.ToString(); this.Monitor.Log(error.Message, LogLevel.Warn); this.Report();
        if (Context.IsWorldReady) Game1.showRedMessage(error.Message);
    }

    private void Report()
    {
        bool ready = Context.IsWorldReady, active = this.Started && this.Step < CompletedStep;
        Route? route = active ? this.Current : null;
        this.Helper.Data.WriteJsonFile("world-interaction-result.json", new
        {
            Completed = this.Step == CompletedStep, this.Step, TotalSteps = CompletedStep, this.Instruction, this.Passed,
            this.Failure, Saved = false, Restored = false, Stopped = this.Stopped,
            Preparation = new { FarmName, SaveFixtureId = FixtureId, MaterialsAndBlueprintsProvided = this.InventoryPrepared,
                BlueprintPurchaseCoveredHere = false, EquipmentCreatedByFixture = false, NaturalGameUpdatesOnly = true,
                SavedMeaning = "Fixture never calls save; verify disk hashes after exit for external saves." },
            PlayerPosition = ready ? Point(Player.Position) : null, PlayerTile = ready ? Point(Player.Tile) : null,
            WorldTargetTile = route is null ? null : Point(route.Tile), StandingTile = route is null ? null : Point(route.Standing),
            PickaxeSlot = ready ? Player.Items.ToList().FindIndex(i => ReferenceEquals(i, this.Pickaxe)) : -1,
            CurrentToolIndex = ready ? Player.CurrentToolIndex : -1, Menu = Game1.activeClickableMenu?.GetType().Name,
            UsingTool = ready && Player.UsingTool, Clock = ready ? Game1.timeOfDay : -1,
            ControlState = this.Started && IsFixedWorld() && ReferenceEquals(Player.currentLocation, this.Location)
                ? CaptureControlState() : null, this.ControlInputEvents,
            this.OmittedControlEvents, this.ControlObservationFailure,
            Viewport = new { Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height },
            ZoomLevel = Game1.options.zoomLevel, UiScale = Game1.options.uiScale,
            DropPosition = route?.Drop is { } drop && drop.Chunks.Count > 0 ? Point(DropPosition(drop)) : null,
            DropCount = ready && this.Location is not null ? this.Location.debris.Count : 0,
            StageTargetClicks = this.StageTargetClicks, this.StageSwingObserved, this.StageStamina,
            this.StageActualToolHits, this.StageMovementInputs, Stamina = ready ? Player.Stamina : -1,
            this.DropObservations, this.Tools, this.Placements, this.InputTrace, this.HitRecords, this.MovementTrail, this.WallProbe,
            Sites = this.Routes.Select(r => new { r.Key, TargetTile = Point(r.Tile), StandingTile = Point(r.Standing), r.FirstInstanceId, r.ReplacementInstanceId }),
            Inventory = ready ? Player.Items.Select((item, slot) => item is null ? null : new { Slot = slot, item.QualifiedItemId, item.Stack, item.Quality }).ToArray() : null
        });
    }

    private sealed class Route
    {
        public readonly string Key, Name;
        public readonly MachineRecipe Recipe;
        public readonly Dictionary<string, int> CraftingCosts;
        public Vector2 Tile, Standing, RelocatedPosition, RelocatedTile;
        public int Facing;
        public SObject? Machine;
        public Guid FirstInstanceId, ReplacementInstanceId;
        public string? RestoreIdleRaw;
        public Debris? Drop;
        public Item? DropPayload;
        public DropEvidence? DropEvidence;
        public Route(string key, string name, MachineRecipe recipe, Dictionary<string, int> costs)
        { this.Key = key; this.Name = name; this.Recipe = recipe; this.CraftingCosts = costs; }
    }
    private sealed record InputEvidence(int Step, string MachineKey, string Phase, string Button, float[] RequestedTile,
        float[] ResolvedToolTile, float[] PlayerTile, int SelectedSlot, float Stamina);
    private sealed record ToolEvidence(string MachineKey, string Phase, int TargetClicks, int ActualTargetHits, bool AnimationObserved,
        float StaminaBefore, float StaminaAfter, bool ProtectedHit, bool RawUnchanged, bool InventoryUnchanged);
    private sealed record NativeHitEvidence(int Step, string MachineKey, string Phase, int X, int Y, float[] TargetTile,
        float[]? PlayerTile, bool LocalPlayer, bool FixturePickaxe, bool ExpectedTarget, float StaminaBefore, float StaminaAfter);
    private sealed record MovementEvidence(int Step, string MachineKey, float[] Position, float[] Tile);
    private sealed record WallProbeEvidence(float[] Tile, bool NativeItemPrecheckAllows, bool NativeWall, bool ObservationEligible);
    private sealed record PlacementEvidence(string MachineKey, string Phase, float[] Tile, Guid? PreviousInstanceId,
        Guid NewInstanceId, long Revision, bool CarrierConsumed);
    private sealed class DropEvidence
    {
        public string MachineKey { get; init; } = "";
        public bool DropObserved { get; init; }
        public bool RelocatedForObservation { get; init; }
        public float[] ObservedDropPosition { get; init; } = Array.Empty<float>();
        public float[] ObservationPlayerTile { get; init; } = Array.Empty<float>();
        public int AppliedMagneticRadius { get; init; }
        public bool PickupObserved { get; set; }
        public float[]? PickupPlayerTile { get; set; }
    }
}
