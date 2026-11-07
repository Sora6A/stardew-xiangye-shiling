using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using XiangyeShiling.Transactions;

/// <summary>
/// 用内存写集串联实际准入/账本/规则/协调器，验证确认丢失和竞争，而不是模拟网络已接入。
/// Farmer 所有权、远端同步、地图引用和真正的背包 setter 仍需游戏适配与双客户端验收。
/// </summary>
internal static class HostPipelineScenarios
{
    public static void Run(Action<string, Action> test)
    {
        test("host core rejects forged ownership before preparation and serializes competing actors", () =>
        {
            var host = new Fixture(); var first = host.Intent(10); var second = host.Intent(20);
            Result forged = host.Dispatch(first, 20, 20);
            Require(forged.Gate == HostRequestRejection.ActorMismatch && host.Preparations == 0 && host.Ledger.BoundRequestCount == 0);
            Result wrongOwner = host.Dispatch(first, 10, 20);
            Require(wrongOwner.Gate == HostRequestRejection.InventoryOwnerMismatch && host.Preparations == 0);
            Require(host.Dispatch(first, 10, 10).Applied);
            Require(host.Dispatch(second, 20, 20).Rule == Rejection.StaleRevision);
            Require(host.Inventory[10][0] is null && host.Inventory[20][0]!.Quantity == 3
                && host.State.Revision == 1 && host.Preparations == 1 && host.InventoryWrites == 1 && host.StateWrites == 1);
            Require(host.Dispatch(second with { ExpectedRevision = 1 }, 20, 20).Begin == HostRequestBeginStatus.IntentMismatch);
        });
        test("host core lost confirmations replay original receipts without rewinding newer state", () =>
        {
            var host = new Fixture(); var start = host.Intent(10);
            Require(host.Dispatch(start, 10, 10).Applied);
            host.Minutes = 220;
            var collect = host.Intent(10, MachineRequestAction.Collect);
            Require(host.Dispatch(collect, 10, 10).Applied);
            MachineState current = host.State; var before = host.Inventory[10].ToArray();
            Result late = host.Dispatch(start, 10, 10);
            Require(late.Begin == HostRequestBeginStatus.Replayed && late.Receipt!.Request == start
                && late.Receipt.AppliedRevision == 1 && host.State == current && current.Revision == 2 && current.CanMove);
            Require(host.Dispatch(collect, 10, 10).Begin == HostRequestBeginStatus.Replayed);
            Require(host.Inventory[10].SequenceEqual(before) && host.Inventory[10][0]!.Quantity == 2
                && host.InventoryWrites == 2 && host.StateWrites == 2 && host.Preparations == 2);
            Require(host.Dispatch(start with { ExpectedRevision = 2 }, 10, 10).Begin == HostRequestBeginStatus.IntentMismatch);
        });
        test("host core full inventory retry retains ready batch and credits only authenticated owner", () =>
        {
            var host = new Fixture(); Require(host.Dispatch(host.Intent(10), 10, 10).Applied);
            host.Minutes = 220;
            host.Inventory[20] = new InventorySlot?[] { new("(O)390", 999, 0, 999), new("(O)388", 999, 0, 999) };
            var collect = host.Intent(20, MachineRequestAction.Collect);
            MachineState ready = host.State;
            Require(host.Dispatch(collect, 20, 20).Rule == Rejection.InventoryFull
                && host.State == ready && host.Preparations == 1);
            host.Inventory[20][1] = null;
            Require(host.Dispatch(collect, 20, 20).Applied && host.State.CanMove && host.State.Revision == 2);
            Require(host.Inventory[20][1]!.ItemId == Fixture.Tofu && host.Inventory[20][1]!.Quantity == 2
                && host.Inventory[10].All(item => item is null) && host.InventoryWrites == 2);
            Require(host.Dispatch(collect, 20, 20).Begin == HostRequestBeginStatus.Replayed && host.InventoryWrites == 2);
        });
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Host core pipeline assertion failed.");
    }

    private sealed record Result(HostRequestRejection Gate = HostRequestRejection.None,
        HostRequestBeginStatus? Begin = null, Rejection? Rule = null, bool Applied = false,
        HostRequestReceipt? Receipt = null);

    private sealed class Fixture
    {
        public const string Soy = "(O)Sora6A.XiangyeShiling_Soybean";
        public const string Tofu = "(O)Sora6A.XiangyeShiling_Tofu";
        public HostRequestLedger Ledger { get; } = new(Guid.NewGuid());
        public MachineState State = MachineState.Empty(Guid.NewGuid(), MachineKind.BeanWorkshop);
        public readonly Dictionary<long, InventorySlot?[]> Inventory = new()
        {
            [10] = new InventorySlot?[] { new(Soy, 3, 0, 999), null },
            [20] = new InventorySlot?[] { new(Soy, 3, 0, 999), null }
        };
        public long Minutes = 100;
        public int Preparations, InventoryWrites, StateWrites;
        private readonly CommitCoordinator Coordinator = new();
        private readonly MachineRecipe Recipe = new("tofu", MachineKind.BeanWorkshop,
            new[] { new Ingredient(Soy, 3) }, new(Tofu, 2, 0, 999), 120);

        public MachineRequestEnvelope Intent(long actor, MachineRequestAction action = MachineRequestAction.Start) =>
            new(1, this.Ledger.SessionId, Guid.NewGuid(), actor, "FarmHouse", 8, 7, this.State.InstanceId,
                this.State.Revision, action, action == MachineRequestAction.Start ? "tofu" : null);

        public Result Dispatch(MachineRequestEnvelope intent, long authenticatedActor, long owner)
        {
            // A real adapter must obtain these fields from the host's actual connected Farmer/map, never JSON.
            var actor = new HostActorSnapshot(this.Ledger.SessionId, true, true, true, true,
                authenticatedActor, owner, "FarmHouse", 7, 7, false);
            HostAuthorization authorization = HostRequestGate.Authorize(intent, actor, new("FarmHouse", 8, 7, this.State));
            if (!authorization.Authorized) return new(Gate: authorization.Rejection);
            HostRequestBeginResult admission = this.Ledger.TryBegin(intent, authenticatedActor);
            if (admission.Status != HostRequestBeginStatus.Started)
                return new(Begin: admission.Status, Receipt: admission.Receipt);
            var snapshot = this.Inventory[owner].ToArray();
            Decision plan = intent.Action == MachineRequestAction.Start
                ? MachineRules.Start(this.State, authorization.RuleRequest!, this.Recipe, true, snapshot, this.Minutes)
                : MachineRules.Collect(this.State, authorization.RuleRequest!, snapshot, this.Minutes);
            if (!plan.Accepted)
            {
                Require(this.Ledger.ReleaseForRetry(admission.Lease) == HostRequestCompletionStatus.Retryable);
                return new(Begin: admission.Status, Rule: plan.Rejection);
            }
            CommitResult committed = this.Coordinator.Commit(() =>
            {
                this.Preparations++;
                return new MemoryCommit(this, owner, snapshot, plan.Transaction!, this.Recipe.Output);
            }, error => throw new InvalidOperationException("Unexpected simulated commit error", error));
            Require(committed == CommitResult.Applied);
            Require(this.Ledger.Complete(admission.Lease, committed, this.State.Revision) == HostRequestCompletionStatus.Recorded);
            return new(Begin: admission.Status, Applied: true,
                Receipt: this.Ledger.TryBegin(intent, authenticatedActor).Receipt);
        }
    }

    /// <summary>仅测试内存写集；未声称它是游戏库存或网络原子提交实现。</summary>
    private sealed class MemoryCommit : IPreparedCommit
    {
        private readonly Fixture Host;
        private readonly long Actor;
        private readonly InventorySlot?[] Before, After;
        private readonly MachineTransaction Plan;
        public MemoryCommit(Fixture host, long actor, InventorySlot?[] before, MachineTransaction plan, Product output)
        {
            this.Host = host; this.Actor = actor; this.Before = before; this.Plan = plan; this.After = before.ToArray();
            foreach (SlotChange change in plan.Slots)
                this.After[change.Slot] = change.NewQuantity == 0 ? null : change.Before is null
                    ? new(output.ItemId, change.NewQuantity, output.Quality, output.MaxStack)
                    : change.Before with { Quantity = change.NewQuantity };
        }
        public bool MatchesOriginal() => this.Host.State == this.Plan.Before && this.Host.Inventory[this.Actor].SequenceEqual(this.Before);
        public void ApplyInventory() { this.Host.Inventory[this.Actor] = this.After; this.Host.InventoryWrites++; }
        public void ApplyState() { this.Host.State = this.Plan.After; this.Host.StateWrites++; }
        public bool IsApplied() => this.Host.State == this.Plan.After && this.Host.Inventory[this.Actor].SequenceEqual(this.After);
        public void RestoreInventory() => this.Host.Inventory[this.Actor] = this.Before;
        public void RestoreState() => this.Host.State = this.Plan.Before;
        public bool IsRestored() => this.MatchesOriginal();
    }
}
