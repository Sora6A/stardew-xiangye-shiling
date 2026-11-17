using XiangyeShiling.Networking;

internal static class OwnerInventoryWriteSetScenarios
{
    public static void Run(Action<string, Action> test)
    {
        string original = new('a', 64);
        string replacement = new('b', 64);
        string unrelated = new('c', 64);
        var removal = new OwnerSlotChange(0, original, null);
        var delivery = new OwnerSlotChange(2, null, replacement);
        var plan = OwnerInventoryWriteSet.Create(3, new[] { removal, delivery })
            ?? throw new InvalidOperationException("Unable to construct the fixture write set.");

        test("owner inventory distinguishes prepared and fully replicated slot changes", () =>
        {
            Assert(plan.Observe(new[] { original, unrelated, null }) == OwnerInventoryObservation.Before);
            Assert(plan.Observe(new[] { null, unrelated, replacement }) == OwnerInventoryObservation.After);
        });

        test("owner inventory never certifies partial replication or conflicts", () =>
        {
            Assert(plan.Observe(new[] { null, unrelated, null }) == OwnerInventoryObservation.Partial);
            Assert(plan.Observe(new[] { original, unrelated, replacement }) == OwnerInventoryObservation.Partial);
            Assert(plan.Observe(new[] { unrelated, original, replacement }) == OwnerInventoryObservation.Conflict);
            Assert(plan.Observe(new[] { original, unrelated }) == OwnerInventoryObservation.Conflict);
        });

        test("owner inventory rejects ambiguous plans and retains its original write set", () =>
        {
            Assert(OwnerInventoryWriteSet.Create(3, new[] { removal, removal with { After = replacement } }) is null);
            Assert(OwnerInventoryWriteSet.Create(3, new[] { delivery with { Index = 3 } }) is null);
            Assert(OwnerInventoryWriteSet.Create(3, new[] { removal with { After = original } }) is null);
            Assert(OwnerInventoryWriteSet.Create(3, new[] { removal with { Before = "not-a-slot-hash" } }) is null);
            var supplied = new[] { removal, delivery };
            var stable = OwnerInventoryWriteSet.Create(3, supplied)!;
            supplied[0] = removal with { Before = unrelated };
            Assert(stable.Observe(new[] { original, unrelated, null }) == OwnerInventoryObservation.Before);
        });
    }

    private static void Assert(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Unexpected owner inventory evidence classification.");
    }
}
