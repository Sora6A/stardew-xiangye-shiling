using XiangyeShiling.Machines;

const string Soy = "(O)Sora6A.XiangyeShiling_Soybean";
const string Wheat = "(O)262";
const string Sauce = "(O)Sora6A.XiangyeShiling_SoySauce";
var recipe = new MachineRecipe("soy-sauce-test", MachineKind.FermentationCrock,
    new[] { new Ingredient(Soy, 3), new Ingredient(Wheat, 2) }, new(Sauce, 4, 0, 999), 120);
var empty = MachineState.Empty(Guid.NewGuid(), MachineKind.FermentationCrock);
InventorySlot?[] stocked = { new(Soy, 2, 2, 999), new(Wheat, 2, 0, 999), new(Soy, 4, 0, 999), null };
int passed = 0;

void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
void Test(string name, Action action)
{
    action();
    Console.WriteLine($"PASS {name}");
    passed++;
}
Request NewRequest(MachineState state, long actor = 10) => new(Guid.NewGuid(), state.InstanceId, state.Revision, actor);
MachineState Started() => MachineRules.Start(empty, NewRequest(empty), recipe, true, stocked, 100).Transaction!.After;
void Reject(Decision decision, Rejection expected)
{
    Assert(!decision.Accepted && decision.Rejection == expected && decision.Transaction is null,
        $"Expected {expected} with no inventory or machine mutation, got {decision.Rejection}.");
}

Test("multi-input shortage yields no partial deduction", () =>
{
    InventorySlot?[] inventory = { new(Soy, 99, 0, 999), new(Wheat, 1, 0, 999) };
    var before = inventory.ToArray();
    Reject(MachineRules.Start(empty, NewRequest(empty), recipe, true, inventory, 100), Rejection.MissingIngredients);
    Assert(inventory.SequenceEqual(before) && empty.Batch is null && empty.Revision == 0, "Input snapshots were mutated.");
});

Test("exact costs span slots and qualities in inventory order", () =>
{
    Decision result = MachineRules.Start(empty, NewRequest(empty), recipe, true, stocked, 100);
    Assert(result.Accepted, "Start failed.");
    var changes = result.Transaction!.Slots;
    Assert(changes.Count == 3 && changes.Single(c => c.Slot == 0).NewQuantity == 0
        && changes.Single(c => c.Slot == 2).NewQuantity == 3
        && changes.Single(c => c.Slot == 1).NewQuantity == 0, "Wrong consumed quantities.");
    Assert(result.Transaction.After.Batch!.Inputs.First().Quality == 2, "Input quality was not retained.");
    Assert(stocked[0]!.Quantity == 2 && stocked[2]!.Quantity == 4, "Planning modified the inventory.");
});

Test("duplicate start and competing stale request cannot deduct again", () =>
{
    Request first = NewRequest(empty);
    Request competing = NewRequest(empty, 20);
    MachineState running = MachineRules.Start(empty, first, recipe, true, stocked, 100).Transaction!.After;
    Reject(MachineRules.Start(running, first, recipe, true, stocked, 100), Rejection.DuplicateRequest);
    Reject(MachineRules.Start(running, competing, recipe, true, stocked, 100), Rejection.StaleRevision);
    Reject(MachineRules.Start(running, first with { ActorId = 20 }, recipe, true, stocked, 100), Rejection.InvalidRequest);
});

Test("busy and ready machines retain their batch and block movement", () =>
{
    MachineState running = Started();
    Reject(MachineRules.Start(running, NewRequest(running), recipe, true, stocked, 101), Rejection.Busy);
    Reject(MachineRules.Start(running, NewRequest(running), recipe, true, stocked, 9999), Rejection.Busy);
    Assert(!running.CanMove && empty.CanMove, "Movement gate is wrong.");
    Assert(running.PhaseAt(219) == MachinePhase.Processing && running.PhaseAt(220) == MachinePhase.Ready,
        "Readiness is not based on the fixed deadline.");
});

Test("locked and wrong-machine recipes yield no transaction", () =>
{
    Reject(MachineRules.Start(empty, NewRequest(empty), recipe, false, stocked, 100), Rejection.LockedRecipe);
    Reject(MachineRules.Start(empty, NewRequest(empty), recipe with { Machine = MachineKind.BeanWorkshop }, true, stocked, 100),
        Rejection.WrongMachine);
});

Test("duplicate ingredient IDs and invalid numeric values fail closed", () =>
{
    foreach (MachineRecipe invalid in new[] {
        recipe with { Inputs = new[] { new Ingredient(Soy, 3), new Ingredient(Soy, 3) } },
        recipe with { Inputs = new[] { new Ingredient(Soy, 0) } },
        recipe with { Minutes = 0 }, recipe with { Output = recipe.Output with { Quantity = -1 } },
        recipe with { Output = recipe.Output with { Quality = 3 } }
    })
        Reject(MachineRules.Start(empty, NewRequest(empty), invalid, true, stocked, 100), Rejection.InvalidRecipe);
    Reject(MachineRules.Start(empty, NewRequest(empty), recipe, true, stocked, long.MaxValue), Rejection.InvalidRecipe);
});

Test("full inventory retains the entire ready batch", () =>
{
    MachineState ready = Started();
    InventorySlot?[] inventory = { new(Sauce, 997, 0, 999, true), new(Wheat, 999, 0, 999) };
    string before = MachineStateCodec.Write(ready);
    Reject(MachineRules.Collect(ready, NewRequest(ready), inventory, 220), Rejection.InventoryFull);
    Assert(inventory[0]!.Quantity == 997 && MachineStateCodec.Write(ready) == before, "Partial product was granted.");
});

Test("collection combines compatible stacks and empty slots atomically", () =>
{
    MachineState ready = Started();
    InventorySlot?[] inventory = { new(Sauce, 997, 0, 999, true), new(Sauce, 5, 2, 999, true), null };
    var transaction = MachineRules.Collect(ready, NewRequest(ready), inventory, 220).Transaction!;
    Assert(transaction.Slots.Count == 2 && transaction.Slots[0].Slot == 0 && transaction.Slots[0].NewQuantity == 999
        && transaction.Slots[1].Slot == 2 && transaction.Slots[1].NewQuantity == 2, "Wrong output allocation.");
    Assert(transaction.After.Batch is null && transaction.After.Revision == 2 && transaction.After.CanMove,
        "Machine did not become idle exactly once.");
});

Test("game stacking veto prevents merging into same-ID special items", () =>
{
    MachineState ready = Started();
    InventorySlot?[] inventory = { new(Sauce, 1, 0, 999, false) };
    Reject(MachineRules.Collect(ready, NewRequest(ready), inventory, 220), Rejection.InventoryFull);
});

Test("large output splits across empty slots without exceeding stack limits", () =>
{
    var large = recipe with { Output = new(Sauce, 2000, 0, 999) };
    MachineState ready = MachineRules.Start(empty, NewRequest(empty), large, true, stocked, 100).Transaction!.After;
    var transaction = MachineRules.Collect(ready, NewRequest(ready), new InventorySlot?[3], 220).Transaction!;
    Assert(transaction.Slots.Select(s => s.NewQuantity).SequenceEqual(new[] { 999, 999, 2 }), "Output stack sizes are wrong.");
    Reject(MachineRules.Collect(ready, NewRequest(ready), new InventorySlot?[2], 220), Rejection.InventoryFull);
});

Test("early and empty collections yield no products", () =>
{
    MachineState running = Started();
    Reject(MachineRules.Collect(running, NewRequest(running), new InventorySlot?[3], 219), Rejection.NotReady);
    Reject(MachineRules.Collect(empty, NewRequest(empty), new InventorySlot?[3], 9999), Rejection.Empty);
});

Test("collection replay stays rejected after save/reload and a new batch", () =>
{
    MachineState ready = Started();
    Request collect = NewRequest(ready);
    MachineState idle = MachineRules.Collect(ready, collect, new InventorySlot?[3], 220).Transaction!.After;
    Assert(MachineStateCodec.TryRead(MachineStateCodec.Write(idle), out var loaded), "Could not reload idle state.");
    Reject(MachineRules.Collect(loaded!, collect, new InventorySlot?[3], 220), Rejection.DuplicateRequest);
    MachineState next = MachineRules.Start(loaded!, NewRequest(loaded!), recipe, true, stocked, 300).Transaction!.After;
    Reject(MachineRules.Collect(next, collect, new InventorySlot?[3], 9999), Rejection.StaleRevision);
});

Test("replacement machine rejects requests addressed to a removed instance", () =>
{
    MachineState replacement = MachineState.Empty(Guid.NewGuid(), empty.Kind);
    Reject(MachineRules.Start(replacement, NewRequest(empty), recipe, true, stocked, 100), Rejection.WrongInstance);
});

Test("persisted batch freezes consumed qualities, yield and completion across days", () =>
{
    MachineState running = Started();
    string saved = MachineStateCodec.Write(running);
    Assert(MachineStateCodec.TryRead(saved, out var loaded) && MachineStateCodec.Write(loaded!) == saved, "Round-trip changed the batch.");
    Assert(loaded!.Batch!.Output.Quantity == 4 && loaded.Batch.Inputs.First().Quality == 2
        && loaded.Batch.ReadyAt == 220 && loaded.PhaseAt(2880) == MachinePhase.Ready, "Batch data or cross-day readiness changed.");
});

Test("corrupt or future save schema is rejected without replacement state", () =>
{
    string saved = MachineStateCodec.Write(Started());
    foreach (string invalid in new[] { "null", "{}", "{", saved.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2"),
        saved.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":1,\"SchemaVersion\":1"),
        saved.Replace("\"Quantity\":4", "\"Quantity\":0"), saved.Replace("\"ReadyAt\":220", "\"ReadyAt\":100"),
        saved.Replace("\"Kind\":\"FermentationCrock\",", ""), saved.Replace(",\"Quality\":2", ""),
        saved.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":1,\"Unexpected\":true") })
    {
        // BUG-2026-002: a failed Replace must not turn a valid save into a supposed negative case.
        Assert(invalid != saved, "The malformed fixture did not change the original JSON.");
        Assert(!MachineStateCodec.TryRead(invalid, out var state) && state is null, "Corrupt save was accepted.");
    }
});

Test("invalid identity and exhausted revisions cannot start", () =>
{
    Reject(MachineRules.Start(empty, NewRequest(empty) with { Id = Guid.Empty }, recipe, true, stocked, 100), Rejection.InvalidRequest);
    Reject(MachineRules.Start(empty, NewRequest(empty) with { ActorId = 0 }, recipe, true, stocked, 100), Rejection.InvalidRequest);
    MachineState exhausted = Started() with { Revision = long.MaxValue };
    Reject(MachineRules.Start(exhausted, NewRequest(exhausted), recipe, true, stocked, 100), Rejection.InvalidState);
});

Test("menu failure retains the original request and revision for safe retry", () =>
{
    var session = new XiangyeShiling.Menus.MenuActionSession();
    Request? first = session.Acquire(empty, 10);
    session.Complete(false);
    Assert(!session.IsComplete && first == session.Acquire(empty with { Revision = 1 }, 10),
        "A failed click must not silently acquire a request for a new revision.");
});

Test("successful menu action cannot acquire another request until reopened", () =>
{
    var session = new XiangyeShiling.Menus.MenuActionSession();
    Request? first = session.Acquire(empty, 10);
    session.Complete(true);
    MachineState next = empty with { Revision = 1 };
    Assert(session.IsComplete && session.Acquire(next, 10) is null,
        "BUG-2026-007: a second click after collection must not create a new start request.");
    Request? reopened = new XiangyeShiling.Menus.MenuActionSession().Acquire(next, 10);
    Assert(reopened is not null && reopened.Id != first!.Id && reopened.ExpectedRevision == 1,
        "Explicit reopening must allow a new intent bound to the new revision.");
});

Test("world tool protection rejects unrelated swing followed by ignored target click", () =>
{
    // Real swing on empty ground costs stamina; a target click during that animation is ignored by the game.
    // This is the revealing BUG-2026-011 sequence: target input exists, but the target had zero completed hits.
    Assert(!GameHarness.WorldToolProof.IsSatisfied(1, 0, 270, 268, true),
        "BUG-2026-011: unrelated stamina loss plus an ignored target click must not pass protection.");
});

Test("world tool protection requires completed target hit and matching pickaxe", () =>
{
    Assert(GameHarness.WorldToolProof.IsSatisfied(1, 1, 270, 268, true), "A genuine completed target hit was rejected.");
    Assert(!GameHarness.WorldToolProof.IsSatisfied(0, 1, 270, 268, true), "Non-mouse fixture call must not count as UI input.");
    Assert(!GameHarness.WorldToolProof.IsSatisfied(1, 1, 270, 270, true), "Uncompleted action must not pass.");
    Assert(!GameHarness.WorldToolProof.IsSatisfied(1, 1, 270, 268, false), "A different selected tool must not pass.");
});

CommitScenarios.Run(Test);
ProtocolScenarios.Run(Test);
LedgerScenarios.Run(Test);
HostPipelineScenarios.Run(Test);
InboxScenarios.Run(Test);
ReadOnlyCodecScenarios.Run(Test);
ReadOnlyTransportScenarios.Run(Test);
ProbePlanScenarios.Run(Test);
ProbeEvidenceScenarios.Run(Test);
DiagnosticScenarios.Run(Test);
QuotaDiagnosticScenarios.Run(Test);
InputObservationScenarios.Run(Test);
NetworkLifecycleScenarios.Run(Test);
NetworkLifecycleReaderScenarios.Run(Test);
WelcomeReplayScenarios.Run(Test);
KitchenEntryScenarios.Run(Test);
Console.WriteLine($"{passed} rule/commit scenarios passed. Actual game inventory and multiplayer transport require the game harness.");
