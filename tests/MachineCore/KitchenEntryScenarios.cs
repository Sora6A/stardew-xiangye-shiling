using GameHarness;

internal static class KitchenEntryScenarios
{
    internal static void Run(Action<string, Action> test)
    {
        // 真实红的GrabTile=(3,5)、准备灶台=(3,4)；原版可由指针/面向回退到真正Action格。
        var fallback = new KitchenActionSample(3, 4, 3, 5, 3, 4, "kitchen", true, true, true);
        test("kitchen native fallback action is accepted despite different grab tile", () =>
            Require(KitchenEntryProof.Matches(fallback), "A real kitchen action was rejected based on an unrelated GrabTile."));
        test("kitchen configured grab tile cannot stand in for a different actual action", () =>
            Require(!KitchenEntryProof.Matches(fallback with { GrabY = 4, ActualY = 6 }),
                "An intended click was substituted for the actual native action tile."));
        test("kitchen source proof rejects failed foreign and non-kitchen native actions", () =>
        {
            foreach (var bad in new[] { fallback with { Succeeded = false }, fallback with { LocalActor = false },
                fallback with { CurrentLocation = false }, fallback with { Action = null }, fallback with { Action = "Shop SeedShop" } })
                Require(!KitchenEntryProof.Matches(bad), "Unrelated or unsuccessful action entered the kitchen evidence.");
        });
    }
    private static void Require(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }
}
