using Microsoft.Xna.Framework;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// BUG-2026-009 的原生放置边界回归。临时占用传入的测试格，始终恢复原对象引用、背包和时钟。
/// 故意直接调用 placementAction：Utility 的碰撞检查会提前拒绝，掩盖补丁在原生返回 true 后
/// 重置既有设备的漏洞。这是组件/API 回归，不代表已经用普通鼠标复现占用格覆盖。
/// </summary>
internal static class PlacementScenarios
{
    public static void Run(Farmer player, GameLocation location, Vector2 tile, MachineService service,
        Action<string, Action> test)
    {
        test("bean placement rejects occupied processing ready corrupt and idle targets",
            () => CheckKind("BeanWorkshop", MachineKind.BeanWorkshop));
        test("crock placement rejects occupied processing ready corrupt and idle targets",
            () => CheckKind("FermentationCrock", MachineKind.FermentationCrock));

        void CheckKind(string key, MachineKind kind)
        {
            Item[] originalInventory = player.Items.ToArray();
            int originalClock = Game1.timeOfDay;
            location.objects.TryGetValue(tile, out SObject? originalTarget);
            string itemId = "(BC)" + MachineService.Prefix + "_" + key;
            MachineRecipe recipe = service.Recipes.Single(r => r.Machine == kind);
            try
            {
                foreach (string phase in new[] { "Processing", "Ready", "Corrupt", "Idle" })
                {
                    Game1.timeOfDay = originalClock;
                    location.objects.Remove(tile);
                    var initial = ItemRegistry.Create<SObject>(itemId);
                    Require(initial.placementAction(location, (int)tile.X * 64, (int)tile.Y * 64, player),
                        key + "/" + phase + ": fixture placement failed");
                    SObject target = location.objects[tile];
                    Require(MachineService.TryRead(target, out var state), "Fixture state was not initialized");
                    if (phase is "Processing" or "Ready")
                    {
                        Stock(ItemRegistry.Create("(O)" + MachineService.Prefix + "_Soybean", 3),
                            ItemRegistry.Create("(O)262", 2));
                        var request = new Request(Guid.NewGuid(), state!.InstanceId, state.Revision, player.UniqueMultiplayerID);
                        OperationResult started = service.Execute(target, player, request, recipe.Id);
                        Require(started.Applied, key + "/" + phase + ": fixture start failed: " + started.Message);
                        if (phase == "Ready")
                        {
                            int minutes = Game1.timeOfDay / 100 * 60 + Game1.timeOfDay % 100 + recipe.Minutes;
                            Game1.timeOfDay = minutes / 60 * 100 + minutes % 60;
                        }
                        Require(MachineService.TryRead(target, out state)
                            && state!.PhaseAt(MachineService.Now()) == (phase == "Ready" ? MachinePhase.Ready : MachinePhase.Processing),
                            key + "/" + phase + ": incorrect fixture phase");
                    }
                    else if (phase == "Corrupt") target.modData[MachineService.StateKey] = "{invalid";

                    var carrier = ItemRegistry.Create<SObject>(itemId);
                    Stock(carrier);
                    Item[] beforeInventory = player.Items.ToArray();
                    int[] beforeQuantities = beforeInventory.Select(i => i?.Stack ?? 0).ToArray();
                    string beforeRaw = target.modData[MachineService.StateKey];
                    Vector2 beforeCarrierTile = carrier.TileLocation;
                    GameLocation beforeCarrierLocation = carrier.Location;
                    long beforeCarrierOwner = carrier.owner.Value;
                    int beforeCarrierHealth = carrier.getHealth();
                    var beforeCarrierData = carrier.modData.Pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();

                    bool accepted = carrier.placementAction(location, (int)tile.X * 64, (int)tile.Y * 64, player);
                    Require(!accepted, key + "/" + phase + ": occupied placement returned success; target JSON unchanged="
                        + (target.modData[MachineService.StateKey] == beforeRaw));
                    Require(location.objects.TryGetValue(tile, out var remaining) && ReferenceEquals(remaining, target)
                        && target.modData[MachineService.StateKey] == beforeRaw,
                        key + "/" + phase + ": occupied target reference or JSON changed");
                    Require(player.Items.SequenceEqual(beforeInventory)
                        && player.Items.Select(i => i?.Stack ?? 0).SequenceEqual(beforeQuantities)
                        && ReferenceEquals(player.Items[0], carrier) && carrier.Stack == 1
                        && carrier.TileLocation == beforeCarrierTile && ReferenceEquals(carrier.Location, beforeCarrierLocation)
                        && carrier.owner.Value == beforeCarrierOwner && carrier.getHealth() == beforeCarrierHealth
                        && carrier.modData.Pairs.OrderBy(p => p.Key, StringComparer.Ordinal).SequenceEqual(beforeCarrierData),
                        key + "/" + phase + ": rejected placement changed carrier or inventory");
                }
            }
            finally
            {
                location.objects.Remove(tile);
                if (originalTarget is not null) location.objects.Add(tile, originalTarget);
                player.Items.OverwriteWith(originalInventory);
                Game1.timeOfDay = originalClock;
            }

            void Stock(params Item[] items)
            {
                var slots = new Item[player.MaxItems];
                Array.Copy(items, slots, items.Length);
                player.Items.OverwriteWith(slots);
            }
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
