using XiangyeShiling.Transactions;

internal static class CommitScenarios
{
    public static void Run(Action<string, Action> test)
    {
        test("commit applies both sides once", () =>
        {
            var c = new CommitCoordinator(); var target = new FakeCommit();
            Require(c.Commit(() => target, _ => { }) == CommitResult.Applied && target.IsApplied());
            Require(target.InventoryWrites == 1 && target.StateWrites == 1 && target.Restores == 0);
        });
        test("final snapshot conflict and preparation failure do not write", () =>
        {
            var c = new CommitCoordinator(); var target = new FakeCommit { Conflict = true };
            Require(c.Commit(() => target, _ => { }) == CommitResult.Conflict);
            Require(c.Commit(() => throw new InvalidOperationException(), _ => { }) == CommitResult.PreparationFailed);
            Require(target.InventoryWrites == 0 && target.StateWrites == 0 && target.Restores == 0);
        });
        test("inventory setter throwing after mutation restores both sides", () =>
        {
            var target = new FakeCommit { InventoryCallback = () => throw new InvalidOperationException() };
            Require(new CommitCoordinator().Commit(() => target, _ => { }) == CommitResult.RolledBack);
            Require(target.IsRestored() && target.Restores == 2 && target.StateWrites == 0);
        });
        test("state setter throwing after mutation restores both sides", () =>
        {
            var target = new FakeCommit { StateCallback = () => throw new InvalidOperationException() };
            Require(new CommitCoordinator().Commit(() => target, _ => { }) == CommitResult.RolledBack);
            Require(target.IsRestored() && target.Restores == 2);
        });
        test("nested callback cannot reenter commit", () =>
        {
            var c = new CommitCoordinator(); var nested = new FakeCommit();
            var target = new FakeCommit { InventoryCallback = () =>
                Require(c.Commit(() => nested, _ => { }) == CommitResult.Reentrant) };
            Require(c.Commit(() => target, _ => { }) == CommitResult.Applied);
            Require(nested.InventoryWrites == 0);
        });
        test("logging exception cannot interrupt compensation", () =>
        {
            var target = new FakeCommit { StateCallback = () => throw new InvalidOperationException() };
            Require(new CommitCoordinator().Commit(() => target, _ => throw new Exception()) == CommitResult.RolledBack);
            Require(target.IsRestored());
        });
        test("failed inventory recovery still restores state and quarantines session", () =>
        {
            var c = new CommitCoordinator(); var target = new FakeCommit {
                StateCallback = () => throw new InvalidOperationException(), FailInventoryRestore = true };
            Require(c.Commit(() => target, _ => { }) == CommitResult.RecoveryFailed);
            Require(c.IsQuarantined && target.State == 0 && target.Restores == 2);
            Require(c.Commit(() => throw new Exception("Must not prepare"), _ => { }) == CommitResult.Quarantined);
        });
        test("silent callback corruption is rejected and compensated", () =>
        {
            // BUG-2026-003: a successful setter return doesn't prove inventory + state match the plan.
            var target = new FakeCommit(); target.StateCallback = () => target.Inventory = 123;
            Require(new CommitCoordinator().Commit(() => target, _ => { }) == CommitResult.RolledBack);
            Require(target.IsRestored());
        });
    }
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Commit boundary assertion failed.");
    }
    private sealed class FakeCommit : IPreparedCommit
    {
        public int Inventory, State, InventoryWrites, StateWrites, Restores;
        public bool Conflict, FailInventoryRestore;
        public Action? InventoryCallback, StateCallback;
        public bool MatchesOriginal() => !this.Conflict && this.IsRestored();
        public void ApplyInventory() { this.InventoryWrites++; this.Inventory = 1; this.InventoryCallback?.Invoke(); }
        public void ApplyState() { this.StateWrites++; this.State = 1; this.StateCallback?.Invoke(); }
        public bool IsApplied() => this.Inventory == 1 && this.State == 1;
        public void RestoreInventory()
        {
            this.Restores++;
            if (this.FailInventoryRestore) throw new InvalidOperationException();
            this.Inventory = 0;
        }
        public void RestoreState() { this.Restores++; this.State = 0; }
        public bool IsRestored() => this.Inventory == 0 && this.State == 0;
    }
}
