using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// P1 原生工具、掉落吸取与重新放置的组件集成回归，不模拟物理鼠标输入。
/// Pickaxe.DoFunction 自行创建真实 Debris；只驱动其 updateChunks，禁止直接调用 collect 绕过吸取。
/// Utility.tryToPlaceItem 自行扣除手持设备；其 bool 不保证落地成功，必须同时核验地图和库存。
/// 传入格仅作为搜索原点；原生 placementAction 不检查全部家具碰撞，不能据其成功认定场地可放置。
/// BUG-2026-010：选择原生前置检查通过的空格和相邻站立格，不移除家具/原对象；finally 恢复玩家与库存。
/// 只删除本用例的设备与新增掉落实体，保留测试前对象/掉落，并留下不含个人信息的选址证据。
/// </summary>
internal static class ToolRemovalScenarios
{
    internal sealed record SelectionEvidence(string MachineKey, int RequestedX, int RequestedY,
        string[] RequestedFurnitureIds, int SelectedX, int SelectedY, int StandingX, int StandingY, bool NativePrecheck);

    internal static List<SelectionEvidence> LastSelections { get; } = new();

    public static void Run(Farmer player, GameLocation location, Vector2 tile, MachineService service,
        Action<string, Action> test)
    {
        LastSelections.Clear();
        test("bean native pickaxe protects batches and pickup replacement rejects old requests",
            () => CheckKind("BeanWorkshop", MachineKind.BeanWorkshop));
        test("crock native pickaxe protects batches and pickup replacement rejects old requests",
            () => CheckKind("FermentationCrock", MachineKind.FermentationCrock));

        void CheckKind(string key, MachineKind kind)
        {
            Require(ReferenceEquals(player, Game1.player) && ReferenceEquals(location, player.currentLocation)
                && ReferenceEquals(location, Game1.currentLocation) && !Game1.eventUp,
                "Tool fixture requires the current local player/location without an event");
            Item[] originalInventory = player.Items.ToArray();
            int[] originalQuantities = Quantities(originalInventory);
            location.objects.TryGetValue(tile, out SObject? originalTarget);
            var originalTargetData = originalTarget?.modData.Pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
            Debris[] originalDebris = location.debris.ToArray();
            Vector2 originalPosition = player.Position;
            int originalFacing = player.FacingDirection, originalIndex = player.CurrentToolIndex, originalPower = player.toolPower.Value;
            bool originalUsingTool = player.UsingTool, originalCanMove = player.CanMove, originalStowed = player.netItemStowed.Value;
            float originalStamina = player.Stamina, originalDebrisSound = Game1.debrisSoundInterval;
            int originalClock = Game1.timeOfDay;
            var originalMenu = Game1.activeClickableMenu;
            Item? originalGrabbed = player.mostRecentlyGrabbedItem;
            var originalHud = Game1.hudMessages;
            Random originalRandom = Game1.random, originalMultiplayerRandom = Game1.recentMultiplayerRandom;
            uint originalRocks = Game1.stats.RocksCrushed, originalPersonalRocks = player.stats.RocksCrushed;
            string itemId = "(BC)" + MachineService.Prefix + "_" + key;
            MachineRecipe recipe = service.Recipes.Single(r => r.Machine == kind);
            var pickaxe = new Pickaxe();
            Vector2 fixtureTile = default;
            bool fixtureSelected = false;
            int hitX = 0, hitY = 0;
            try
            {
                // 隔离随机源与提示列表，既让弹跳可复跑，也不改变测试前的随机状态/合并已有 HUD 消息。
                Game1.random = new Random(20261001 + (int)kind);
                Game1.hudMessages = new();
                Game1.activeClickableMenu = null;
                player.UsingTool = false;
                player.CanMove = true;
                player.toolPower.Value = 0;
                player.Stamina = Math.Max(20, originalStamina);
                fixtureTile = SelectFixture();
                fixtureSelected = true;
                hitX = (int)fixtureTile.X * 64 + 32;
                hitY = (int)fixtureTile.Y * 64 + 32;

                foreach (string phase in new[] { "Processing", "Ready", "Corrupt" })
                {
                    SObject target = Fresh();
                    Stock(pickaxe, Soy(), Wheat());
                    player.CurrentToolIndex = 0;
                    if (phase == "Corrupt") target.modData[MachineService.StateKey] = "{invalid";
                    else
                    {
                        MachineState before = State(target);
                        OperationResult started = service.Execute(target, player, RequestFor(before), recipe.Id);
                        Require(started.Applied, key + "/" + phase + ": start fixture failed: " + started.Message);
                        if (phase == "Ready")
                        {
                            int minutes = Game1.timeOfDay / 100 * 60 + Game1.timeOfDay % 100 + recipe.Minutes;
                            Game1.timeOfDay = minutes / 60 * 100 + minutes % 60;
                        }
                        Require(State(target).PhaseAt(MachineService.Now()) == (phase == "Ready" ? MachinePhase.Ready : MachinePhase.Processing),
                            key + "/" + phase + ": incorrect fixture phase");
                    }
                    string raw = target.modData[MachineService.StateKey];
                    Item[] inventory = player.Items.ToArray(); int[] quantities = Quantities(inventory);
                    Debris[] beforeDebris = location.debris.ToArray();
                    pickaxe.DoFunction(location, hitX, hitY, 0, player);
                    Require(location.objects.TryGetValue(fixtureTile, out var placed) && ReferenceEquals(placed, target)
                        && target.modData[MachineService.StateKey] == raw && InventoryMatches(inventory, quantities)
                        && SameReferences(location.debris, beforeDebris), key + "/" + phase + ": native pickaxe changed a protected device");
                }

                SObject idle = Fresh();
                MachineState oldState = State(idle);
                Request oldRequest = RequestFor(oldState);
                Stock(pickaxe); player.CurrentToolIndex = 0;
                int otherDevices = location.objects.Values.Count(o => o.QualifiedItemId == itemId) - 1;
                pickaxe.DoFunction(location, hitX, hitY, 0, player);
                Require(!location.objects.ContainsKey(fixtureTile) && player.Items.CountId(itemId) == 0,
                    key + ": native pickaxe didn't remove the idle device into a drop");
                Debris[] created = location.debris.Where(d => !originalDebris.Any(o => ReferenceEquals(o, d))).ToArray();
                Require(created.Length == 1 && created[0].itemId.Value == itemId && created[0].Chunks.Count == 1
                    && created[0].item?.QualifiedItemId == itemId && created[0].item!.Stack == 1,
                    key + ": tool removal didn't create exactly one matching item Debris");
                Debris drop = created[0]; Item payload = drop.item!;
                bool completed = false;
                // 按正常帧长驱动真实弹跳/磁吸/collect；完成值与 GameLocation 的 RemoveWhere 契约相同。
                // 不修改 chunks、player、timeSinceDoneBouncing，也不以直接库存加物绕过掉落逻辑。
                for (int frame = 1; frame <= 1200 && !completed; frame++)
                {
                    completed = drop.updateChunks(new GameTime(TimeSpan.FromMilliseconds(frame * 16), TimeSpan.FromMilliseconds(16)), location);
                    if (completed) Require(location.debris.Remove(drop), "Completed item Debris wasn't in its location");
                }
                Require(completed && drop.Chunks.Count == 0 && !location.debris.Any(d => ReferenceEquals(d, drop))
                    && SameReferences(location.debris, originalDebris) && player.Items.CountId(itemId) == 1,
                    key + ": native automatic pickup didn't consume exactly one completed drop");
                SObject carrier = (SObject)player.Items.Single(i => i?.QualifiedItemId == itemId);
                Require(ReferenceEquals(carrier, payload) && carrier.Stack == 1,
                    key + ": pickup replaced the actual drop payload or changed its quantity");
                int carrierIndex = player.Items.ToList().FindIndex(i => ReferenceEquals(i, carrier));
                Require(carrierIndex >= 0, "Picked-up device is not in the real player inventory");
                player.CurrentToolIndex = carrierIndex;
                Require(Utility.playerCanPlaceItemHere(location, carrier, hitX, hitY, player),
                    key + ": actual picked-up carrier failed the selected tile's native placement precheck");
                Require(Utility.tryToPlaceItem(location, carrier, hitX, hitY), key + ": native placement was rejected");
                Require(location.objects.TryGetValue(fixtureTile, out var replacement) && replacement.QualifiedItemId == itemId
                    && !ReferenceEquals(replacement, idle) && !ReferenceEquals(replacement, carrier)
                    && player.Items.CountId(itemId) == 0 && carrier.Stack == 0
                    && location.objects.Values.Count(o => o.QualifiedItemId == itemId) == otherDevices + 1,
                    key + ": replacement placement didn't create one map device and consume the carrier");
                MachineState newState = State(replacement!);
                Require(newState.CanMove && newState.InstanceId != Guid.Empty && newState.InstanceId != oldState.InstanceId
                    && newState.Revision == 0, key + ": replacement didn't receive a fresh idle identity");

                Stock(pickaxe, Soy(), Wheat()); player.CurrentToolIndex = 0;
                Require(!player.UsingTool && Math.Abs(player.Tile.X - fixtureTile.X) <= 1 && Math.Abs(player.Tile.Y - fixtureTile.Y) <= 1,
                    "Old-request check must satisfy tool/range prerequisites");
                string newRaw = replacement!.modData[MachineService.StateKey];
                Item[] beforeReplay = player.Items.ToArray(); int[] beforeReplayQuantities = Quantities(beforeReplay);
                Require(!service.Execute(replacement, player, oldRequest, recipe.Id).Applied
                    && replacement.modData[MachineService.StateKey] == newRaw && InventoryMatches(beforeReplay, beforeReplayQuantities),
                    key + ": removed device's old request changed the replacement or inventory");
                OperationResult freshStart = service.Execute(replacement, player, RequestFor(newState), recipe.Id);
                Require(freshStart.Applied && State(replacement).Batch is not null && State(replacement).InstanceId == newState.InstanceId
                    && recipe.Inputs.All(input => player.Items.CountId(input.ItemId) == 0),
                    key + ": fresh replacement request didn't start and consume its complete recipe");
                // 镐的矿石统计只在原生 stone 分支增加；两种独立 BC ID 不应进入该分支。
                Require(Game1.stats.RocksCrushed == originalRocks && player.stats.RocksCrushed == originalPersonalRocks,
                    "Machine tool test unexpectedly changed stone statistics");
            }
            finally
            {
                foreach (Debris added in location.debris.Where(d => !originalDebris.Any(o => ReferenceEquals(o, d))).ToArray())
                    location.debris.Remove(added);
                // 搜索阶段不动地图；只有选定且原本空闲的格才能成为本用例清理的范围。
                if (fixtureSelected) location.objects.Remove(fixtureTile);
                // 索引 setter 会通知旧选中物品停止持有；先对临时库存切换，避免触碰原工具属性。
                player.CurrentToolIndex = originalIndex;
                player.Items.OverwriteWith(originalInventory);
                player.Position = originalPosition;
                player.faceDirection(originalFacing);
                player.UsingTool = originalUsingTool;
                player.CanMove = originalCanMove;
                player.toolPower.Value = originalPower;
                player.netItemStowed.Value = originalStowed;
                player.Stamina = originalStamina;
                player.mostRecentlyGrabbedItem = originalGrabbed!;
                Game1.timeOfDay = originalClock;
                Game1.activeClickableMenu = originalMenu;
                Game1.random = originalRandom;
                Game1.recentMultiplayerRandom = originalMultiplayerRandom;
                Game1.hudMessages = originalHud;
                Game1.debrisSoundInterval = originalDebrisSound;
                if (Game1.stats.RocksCrushed != originalRocks) Game1.stats.RocksCrushed = originalRocks;
                if (player.stats.RocksCrushed != originalPersonalRocks) player.stats.RocksCrushed = originalPersonalRocks;
                Require(InventoryMatches(originalInventory, originalQuantities) && SameReferences(location.debris, originalDebris)
                    && (!fixtureSelected || !location.objects.ContainsKey(fixtureTile))
                    && (originalTarget is null ? !location.objects.ContainsKey(tile)
                        : location.objects.TryGetValue(tile, out var restored) && ReferenceEquals(restored, originalTarget)
                            && originalTarget.modData.Pairs.OrderBy(p => p.Key, StringComparer.Ordinal).SequenceEqual(originalTargetData!))
                    && player.Position == originalPosition && player.FacingDirection == originalFacing
                    && player.CurrentToolIndex == originalIndex && player.UsingTool == originalUsingTool && player.CanMove == originalCanMove
                    && player.toolPower.Value == originalPower && player.netItemStowed.Value == originalStowed && player.Stamina == originalStamina
                    && ReferenceEquals(player.mostRecentlyGrabbedItem, originalGrabbed) && Game1.timeOfDay == originalClock
                    && ReferenceEquals(Game1.activeClickableMenu, originalMenu) && ReferenceEquals(Game1.random, originalRandom)
                    && ReferenceEquals(Game1.recentMultiplayerRandom, originalMultiplayerRandom) && ReferenceEquals(Game1.hudMessages, originalHud)
                    && Game1.debrisSoundInterval == originalDebrisSound && Game1.stats.RocksCrushed == originalRocks
                    && player.stats.RocksCrushed == originalPersonalRocks, key + ": tool fixture restoration failed");
            }

            SObject Fresh()
            {
                Game1.timeOfDay = originalClock;
                location.objects.Remove(fixtureTile);
                var item = ItemRegistry.Create<SObject>(itemId);
                Require(item.placementAction(location, hitX, hitY, player), key + ": fixture placement failed");
                SObject placed = location.objects[fixtureTile];
                Require(placed.Type == "Crafting" && placed.fragility.Value != 2 && !placed.IsBreakableStone() && !placed.Name.Contains("Boulder"),
                    "Fixture must use the normal movable crafting-object branch");
                return placed;
            }
            Vector2 SelectFixture()
            {
                var requestedBounds = new Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64);
                string[] requestedFurniture = location.furniture
                    .Where(f => f.GetBoundingBox().Intersects(requestedBounds))
                    .Select(f => f.QualifiedItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                var probe = ItemRegistry.Create<SObject>(itemId);
                Vector2[] approaches = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };
                int[] directions = { 1, 3, 2, 0 };
                // 同步测试最多搜索附近八格；不换地图、不移动家具，也不忽略目标格的任何原生碰撞。
                for (int radius = 0; radius <= 8; radius++)
                    for (int y = -radius; y <= radius; y++)
                        for (int x = -radius; x <= radius; x++)
                        {
                            if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
                            Vector2 candidate = tile + new Vector2(x, y);
                            if (!location.isTileOnMap(candidate) || location.objects.ContainsKey(candidate)) continue;
                            var bounds = new Rectangle((int)candidate.X * 64, (int)candidate.Y * 64, 64, 64);
                            // Tool.DoFunction 基类会伤害这一范围内的怪物，不能混入经验/角色变化。
                            if (location.characters.OfType<Monster>().Any(m => m.GetBoundingBox().Intersects(bounds))) continue;
                            for (int approach = 0; approach < approaches.Length; approach++)
                            {
                                Vector2 standing = candidate + approaches[approach];
                                // 站立格也须为空地；只在此检查排除自己的 Farmer 碰撞，其他玩家单独保留。
                                if (!location.CanItemBePlacedHere(standing, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)) continue;
                                var standingBounds = new Rectangle((int)standing.X * 64, (int)standing.Y * 64, 64, 64);
                                if (location.farmers.Any(f => !ReferenceEquals(f, player) && f.GetBoundingBox().Intersects(standingBounds))) continue;
                                // Position 是精灵左上角，Tile 来自包围盒中心；原生方法才能把真实 StandingPixel 对齐到格中心。
                                player.setTileLocation(standing);
                                player.faceDirection(directions[approach]);
                                int candidateX = (int)candidate.X * 64 + 32, candidateY = (int)candidate.Y * 64 + 32;
                                bool nativePrecheck = Utility.playerCanPlaceItemHere(location, probe, candidateX, candidateY, player);
                                if (player.Tile != standing || !nativePrecheck) continue;
                                LastSelections.Add(new(key, (int)tile.X, (int)tile.Y, requestedFurniture,
                                    (int)candidate.X, (int)candidate.Y, (int)standing.X, (int)standing.Y, nativePrecheck));
                                return candidate;
                            }
                        }
                throw new InvalidOperationException(key + ": no empty tile with a valid adjacent standing tile passed native placement within eight tiles");
            }
            Item Soy() => ItemRegistry.Create("(O)" + MachineService.Prefix + "_Soybean", 3);
            Item Wheat() => ItemRegistry.Create("(O)262", 2);
            void Stock(params Item[] items)
            {
                var slots = new Item[player.MaxItems];
                Array.Copy(items, slots, items.Length);
                player.Items.OverwriteWith(slots);
            }
            MachineState State(SObject machine)
            {
                Require(MachineService.TryRead(machine, out var state), "Fixture machine state isn't readable");
                return state!;
            }
            Request RequestFor(MachineState state) => new(Guid.NewGuid(), state.InstanceId, state.Revision, player.UniqueMultiplayerID);
            bool InventoryMatches(Item[] references, int[] quantities) => player.Items.SequenceEqual(references)
                && Quantities(player.Items).SequenceEqual(quantities);
        }
    }

    private static int[] Quantities(IEnumerable<Item> inventory) => inventory.Select(i => i?.Stack ?? 0).ToArray();
    private static bool SameReferences(IEnumerable<Debris> actual, Debris[] expected)
    {
        Debris[] current = actual.ToArray();
        return current.Length == expected.Length && current.Zip(expected).All(pair => ReferenceEquals(pair.First, pair.Second));
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
