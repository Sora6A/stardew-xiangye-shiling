using XiangyeShiling.Machines;
using XiangyeShiling.Networking;

internal static class OwnerCommitReservationScenarios
{
    private const ulong WorldId = 1024;
    private const long HostId = 101;
    private const long ActorId = 202;
    private const string Soybean = "(O)Sora6A.XiangyeShiling_Soybean";
    private const string Wheat = "(O)262";
    private const string SoySauce = "(O)Sora6A.XiangyeShiling_SoySauce";
    private static readonly Guid GrantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid InstanceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StartId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CollectId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static void Run(Action<string, Action> test)
    {
        var recipe = new MachineRecipe("soy-sauce-test", MachineKind.FermentationCrock,
            new[] { new Ingredient(Soybean, 3), new Ingredient(Wheat, 2) }, new(SoySauce, 4, 0, 999), 240);
        MachineState idle = MachineState.Empty(InstanceId, recipe.Machine);
        MachineState batch = MachineRules.Start(idle, new Request(StartId, InstanceId, 0, ActorId), recipe, true,
            new InventorySlot?[] { new(Soybean, 3, 0, 999), new(Wheat, 5, 0, 999), null }, 100).Transaction!.After;
        MachineState collected = MachineRules.Collect(batch, new Request(CollectId, InstanceId, 1, ActorId),
            new InventorySlot?[3], 340).Transaction!.After;
        string idleRaw = "\n" + MachineStateCodec.Write(idle);
        string batchRaw = MachineStateCodec.Write(batch);
        string collectedRaw = MachineStateCodec.Write(collected);
        var start = new MachineRequestEnvelope(1, SessionId, StartId, ActorId, "FarmHouse", 1, 2, InstanceId, 0,
            MachineRequestAction.Start, recipe.Id);
        var collect = start with { RequestId = CollectId, ExpectedRevision = 1, Action = MachineRequestAction.Collect, RecipeId = null };
        string startRaw = MachineRequestCodec.Write(start);
        string collectRaw = MachineRequestCodec.Write(collect);
        OwnerSlotChange[] changed = { new(0, new string('a', 64), null), new(1, new string('b', 64), new string('c', 64)) };
        OwnerInventoryWriteSet cost = OwnerInventoryWriteSet.Create(3, changed)!;
        OwnerInventoryWriteSet output = OwnerInventoryWriteSet.Create(3, new[] { new OwnerSlotChange(0, null, new string('d', 64)) })!;
        OwnerCommitReservation plan = Create(startRaw, idleRaw, batchRaw, cost);

        test("host reservation preserves the exact request and machine states through a save round trip", () =>
        {
            string raw = OwnerCommitReservationCodec.Write(plan);
            Assert(OwnerCommitReservationCodec.TryRead(raw, out var loaded));
            Assert(loaded!.WorldId == WorldId && loaded.HostId == HostId && loaded.GrantId == GrantId
                && loaded.Request == start && loaded.RequestRaw == startRaw && loaded.BeforeRaw == idleRaw && loaded.AfterRaw == batchRaw
                && loaded.Status == OwnerReservationStatus.Reserved && loaded.WriteSet.Changes.SequenceEqual(changed)
                && loaded.OwnerJournal == plan.OwnerJournal && OwnerCommitReservationCodec.Write(loaded) == raw);
        });

        test("host reservation terminal records keep the same owner journal and accept a collection plan", () =>
        {
            OwnerCommitReservation collectPlan = Create(collectRaw, batchRaw, collectedRaw, output);
            foreach (OwnerReservationStatus status in new[] { OwnerReservationStatus.Applied, OwnerReservationStatus.Cancelled })
            {
                var terminal = collectPlan.WithTerminalStatus(status);
                string raw = OwnerCommitReservationCodec.Write(terminal);
                Assert(OwnerCommitReservationCodec.TryRead(raw, out var loaded) && loaded.Status == status
                    && loaded.PlanKey == collectPlan.PlanKey && loaded.OwnerJournal == collectPlan.OwnerJournal);
            }
            Assert(collectPlan.OwnerJournal != plan.OwnerJournal);
        });

        test("host reservation binds owner, action, recipe, target revision and affected slots", () =>
        {
            Reject(MachineRequestCodec.Write(start with { ActorId = HostId }), idleRaw, batchRaw, cost);
            Reject(MachineRequestCodec.Write(start with { ActorId = ActorId + 1 }), idleRaw, batchRaw, cost);
            Reject(MachineRequestCodec.Write(start with { InstanceId = Guid.NewGuid() }), idleRaw, batchRaw, cost);
            Reject(MachineRequestCodec.Write(start with { ExpectedRevision = 1 }), idleRaw, batchRaw, cost);
            Reject(MachineRequestCodec.Write(start with { RecipeId = "other-recipe" }), idleRaw, batchRaw, cost);
            Reject(collectRaw, idleRaw, batchRaw, cost);
            Reject(startRaw, idleRaw, batchRaw, OwnerInventoryWriteSet.Create(3, new[] { changed[0] }));
            Reject(collectRaw, batchRaw, collectedRaw, cost);
        });

        test("host reservation plan key binds exact content and ignores slot ordering", () =>
        {
            var reordered = Create(startRaw, idleRaw, batchRaw, OwnerInventoryWriteSet.Create(3, changed.Reverse().ToArray())!);
            Assert(reordered.PlanKey == plan.PlanKey);
            Assert(Create(startRaw, idleRaw.TrimStart(), batchRaw, cost).PlanKey != plan.PlanKey);
            Assert(OwnerCommitReservation.TryCreate(WorldId + 1, HostId, GrantId, startRaw, idleRaw, batchRaw, cost, out var otherWorld)
                && otherWorld.OwnerJournal != plan.OwnerJournal);
            Assert(OwnerCommitReservation.TryCreate(WorldId, HostId, Guid.NewGuid(), startRaw, idleRaw, batchRaw, cost, out var otherGrant)
                && otherGrant.OwnerJournal != plan.OwnerJournal);
        });

        test("host reservation codec rejects malformed unknown and oversized save records", () =>
        {
            string raw = OwnerCommitReservationCodec.Write(plan);
            foreach (string invalid in new[]
            {
                raw.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2"),
                raw.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":1,\"SchemaVersion\":1"),
                raw.Replace("\"Status\":\"Reserved\"", "\"Status\":\"reserved\""),
                raw.Replace("\"Capacity\":3", "\"Capacity\":129"),
                raw.Replace("\"Index\":1", "\"Index\":0"),
                raw.Replace("\"Before\":", "\"Unexpected\":0,\"Before\":"),
                raw.Replace("\"Before\":", "\"Before\":null,\"Before\":"),
                raw.Replace("\"WorldId\":1024", "\"WorldId\":-1"),
                raw.Replace("\"WorldId\":1024", "\"WorldId\":\"\\ud800\""),
                "\ud800" + raw,
                new string(' ', OwnerCommitReservationCodec.MaxUtf8Bytes) + raw
            })
                Assert(invalid != raw && !OwnerCommitReservationCodec.TryRead(invalid, out var rejected) && rejected is null);
            Reject(startRaw, idleRaw + new string(' ', 16 * 1024), batchRaw, cost);
        });
    }

    private static OwnerCommitReservation Create(string request, string before, string after, OwnerInventoryWriteSet changes)
        => OwnerCommitReservation.TryCreate(WorldId, HostId, GrantId, request, before, after, changes, out var value)
            ? value : throw new InvalidOperationException("Expected valid reservation fixture.");

    private static void Reject(string request, string before, string after, OwnerInventoryWriteSet? changes)
        => Assert(!OwnerCommitReservation.TryCreate(WorldId, HostId, GrantId, request, before, after, changes, out var value) && value is null);

    private static void Assert(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Unexpected reservation codec or plan validation result.");
    }
}
