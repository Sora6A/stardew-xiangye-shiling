using System.Reflection;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

internal static class RemoteHostPlanPreparationScenarios
{
    internal static void Run(Action<string, Action> test)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags shared = BindingFlags.Static | BindingFlags.NonPublic;
        var production = typeof(MachineService).Assembly;
        var planner = production.GetType("XiangyeShiling.GameIntegration.RemoteHostPlanPreparation", throwOnError: true)!;
        var inventory = production.GetType("XiangyeShiling.GameIntegration.OwnerInventoryPlanning", throwOnError: true)!;

        test("native remote host planner requires the bound game thread and a ready host world", () =>
        {
            object adapter = planner.GetConstructor(instance, null, new[] { typeof(IReadOnlyList<MachineRecipe>) }, null)!
                .Invoke(new object[] { Array.Empty<MachineRecipe>() });
            var prepare = planner.GetMethod("TryPrepare", instance)!;
            var request = new MachineRequestEnvelope(1, Guid.NewGuid(), Guid.NewGuid(), -42, "Farm", 8, 9,
                Guid.NewGuid(), 0, MachineRequestAction.Collect, null);
            bool Rejects(long actor)
            {
                object?[] args = { 15UL, actor, request.SessionId, MachineRequestCodec.Write(request), null };
                return !(bool)prepare.Invoke(adapter, args)! && args[^1] is null;
            }
            Check(Rejects(request.ActorId), "A native request cannot prepare a grant without a loaded host world.");
            Check(Rejects(88), "A claimed owner cannot override the verified sender.");
            Check(Task.Run(() => Rejects(request.ActorId)).GetAwaiter().GetResult(),
                "A different thread cannot prepare a native reservation.");
        });

        test("native owner inventory snapshot uses the game's stack compatibility", () =>
        {
            var ordinary = Wheat();
            var ordered = Wheat();
            ordered.orderData.Value = "different-native-order";
            var output = Wheat();
            Item[] items = { ordinary, ordered, null! };
            var slots = (InventorySlot?[])inventory.GetMethod("Snapshot", shared)!.Invoke(null, new object[] { items, output })!;
            Check(slots.Length == 3 && slots[0]?.CanStackOutput == true && slots[1]?.CanStackOutput == false
                && slots[2] is null && slots[0]?.ItemId == slots[1]?.ItemId && slots[0]?.Quality == slots[1]?.Quality,
                "Matching item ID and quality do not make a different native order stack compatible.");
            Check(ordinary.Stack == 2 && ordered.Stack == 2 && output.Stack == 2 && ordered.orderData.Value == "different-native-order",
                "Planning must leave the original stacks and native attributes intact.");
        });

        test("native owner write set rejects a changed slot or a noncanonical affected item without editing inventory", () =>
        {
            var marked = Wheat();
            marked.modData["Other.Mod/test"] = "preserve";
            Item[] items = { marked, null! };
            var describe = inventory.GetMethod("TryDescribeChanges", shared)!;
            foreach (int expectedQuantity in new[] { 1, 2 })
            {
                var before = new InventorySlot(marked.QualifiedItemId, expectedQuantity, marked.Quality, marked.maximumStackSize());
                object?[] args = { items, new[] { new SlotChange(0, before, 0) }, null, null, null };
                Check(!(bool)describe.Invoke(null, args)! && args[^2] is null && args[^1] is null,
                    "A different quantity or a marked affected stack cannot certify a reservation.");
            }
            Check(ReferenceEquals(items[0], marked) && items[1] is null && marked.Stack == 2
                && marked.modData["Other.Mod/test"] == "preserve",
                "A rejected plan must leave both native slots and item metadata intact.");
        });
    }

    private static SObject Wheat() => new() { ItemId = "262", Name = "Wheat", Stack = 2 };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
