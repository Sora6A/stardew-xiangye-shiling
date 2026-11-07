using XiangyeShiling.Networking;
using XiangyeShiling.Transactions;

/// <summary>验证真实纯账本的拒绝、意图绑定与回执生命周期，不替代双客户端或远端库存实测。</summary>
internal static class LedgerScenarios
{
    public static void Run(Action<string, Action> test)
    {
        test("ledger rejects invalid identity session and foreign threads without binding", () =>
        {
            var ledger = NewLedger(); var request = NewRequest(ledger);
            Require(ledger.TryBegin(null, request.ActorId).Status == HostRequestBeginStatus.InvalidRequest);
            Require(ledger.TryBegin(request with { ProtocolVersion = 999 }, request.ActorId).Status == HostRequestBeginStatus.InvalidRequest);
            Require(ledger.TryBegin(request, request.ActorId + 1).Status == HostRequestBeginStatus.WrongActor);
            Require(ledger.TryBegin(request, 0).Status == HostRequestBeginStatus.WrongActor);
            Require(ledger.TryBegin(request with { SessionId = Guid.NewGuid() }, request.ActorId).Status == HostRequestBeginStatus.WrongSession);

            var failures = new List<Exception>();
            var thread = new Thread(() =>
            {
                foreach (Action action in new Action[] {
                    () => ledger.TryBegin(request, request.ActorId),
                    () => ledger.Complete(null, CommitResult.Applied, 1),
                    () => ledger.ReleaseForRetry(null), () => ledger.Quarantine(),
                    () => { _ = ledger.BoundRequestCount; }, () => { _ = ledger.IsQuarantined; }
                })
                {
                    try { action(); failures.Add(new Exception("Foreign thread was accepted.")); }
                    catch (InvalidOperationException) { }
                    catch (Exception ex) { failures.Add(ex); }
                }
            });
            thread.Start(); thread.Join();
            Require(failures.Count == 0 && ledger.BoundRequestCount == 0 && !ledger.IsQuarantined);
        });

        test("ledger binds every command field and rejects historical revision rebinding", () =>
        {
            var ledger = NewLedger(); var request = NewRequest(ledger);
            var lease = Start(ledger, request);
            Require(ledger.ReleaseForRetry(lease) == HostRequestCompletionStatus.Retryable);
            foreach (MachineRequestEnvelope changed in new[] {
                request with { LocationName = "Farm" }, request with { TileX = request.TileX + 1 },
                request with { TileY = request.TileY + 1 }, request with { InstanceId = Guid.NewGuid() },
                request with { ExpectedRevision = request.ExpectedRevision + 1 }, request with { RecipeId = "other-recipe" },
                request with { Action = MachineRequestAction.Collect, RecipeId = null }
            }) Require(ledger.TryBegin(changed, request.ActorId).Status == HostRequestBeginStatus.IntentMismatch);

            Require(ledger.Complete(Start(ledger, request), CommitResult.Applied, 1) == HostRequestCompletionStatus.Recorded);
            Require(ledger.TryBegin(request with { ExpectedRevision = 2 }, request.ActorId).Status == HostRequestBeginStatus.IntentMismatch);
            Require(ledger.BoundRequestCount == 1);
        });

        test("ledger replays the complete success receipt without another execution or actor collision", () =>
        {
            var ledger = NewLedger(); var request = NewRequest(ledger); int writes = 0;
            HostRequestReceipt Apply(MachineRequestEnvelope command)
            {
                var started = ledger.TryBegin(command, command.ActorId);
                Require(started.Status == HostRequestBeginStatus.Started && started.Receipt is null);
                writes++;
                Require(ledger.Complete(started.Lease, CommitResult.Applied, command.ExpectedRevision + 1) == HostRequestCompletionStatus.Recorded);
                return ledger.TryBegin(command, command.ActorId).Receipt!;
            }
            HostRequestReceipt original = Apply(request);
            Apply(request with { RequestId = Guid.NewGuid(), ExpectedRevision = 1 });
            Apply(request with { ActorId = request.ActorId + 1 });
            var replay = ledger.TryBegin(request with { }, request.ActorId);
            Require(replay.Status == HostRequestBeginStatus.Replayed && replay.Lease is null && ReferenceEquals(replay.Receipt, original));
            Require(original.Request == request && original.AppliedRevision == 1
                && original.RequestId == request.RequestId && original.ActorId == request.ActorId
                && original.SessionId == request.SessionId && original.InstanceId == request.InstanceId);
            Require(writes == 3 && ledger.BoundRequestCount == 3);
            Require(ledger.TryBegin(request, request.ActorId + 1).Status == HostRequestBeginStatus.WrongActor);
        });

        test("ledger retries unchanged failures but rejects reentry stale and foreign leases", () =>
        {
            var ledger = NewLedger(); var request = NewRequest(ledger); var old = Start(ledger, request);
            Require(ledger.TryBegin(request, request.ActorId).Status == HostRequestBeginStatus.InProgress);
            Require(ledger.ReleaseForRetry(old) == HostRequestCompletionStatus.Retryable);
            var current = Start(ledger, request);
            var foreign = Start(new HostRequestLedger(ledger.SessionId), request);
            foreach (HostRequestLease invalid in new[] { old, foreign })
            {
                Require(ledger.Complete(invalid, CommitResult.Applied, 1) == HostRequestCompletionStatus.InvalidLease);
                Require(ledger.ReleaseForRetry(invalid) == HostRequestCompletionStatus.InvalidLease);
            }
            Require(ledger.TryBegin(request, request.ActorId).Status == HostRequestBeginStatus.InProgress);
            foreach (CommitResult failure in new[] { CommitResult.Conflict, CommitResult.Reentrant, CommitResult.PreparationFailed, CommitResult.RolledBack })
            {
                Require(ledger.Complete(current, failure) == HostRequestCompletionStatus.Retryable);
                current = Start(ledger, request);
                Require(!ReferenceEquals(current, old));
            }
            Require(ledger.Complete(current, CommitResult.Applied, 1) == HostRequestCompletionStatus.Recorded);
            Require(ledger.Complete(current, CommitResult.Applied, 1) == HostRequestCompletionStatus.InvalidLease);
            Require(ledger.ReleaseForRetry(current) == HostRequestCompletionStatus.InvalidLease);
            Require(ledger.TryBegin(request, request.ActorId).Status == HostRequestBeginStatus.Replayed && !ledger.IsQuarantined);
        });

        test("ledger capacity preserves receipts and existing retries without eviction", () =>
        {
            var ledger = new HostRequestLedger(Guid.NewGuid(), 2); var applied = NewRequest(ledger);
            Require(ledger.Complete(Start(ledger, applied), CommitResult.Applied, 1) == HostRequestCompletionStatus.Recorded);
            var retry = NewRequest(ledger); var lease = Start(ledger, retry);
            Require(ledger.ReleaseForRetry(lease) == HostRequestCompletionStatus.Retryable);
            Require(ledger.TryBegin(NewRequest(ledger), applied.ActorId).Status == HostRequestBeginStatus.CapacityExceeded);
            Require(ledger.TryBegin(applied, applied.ActorId).Status == HostRequestBeginStatus.Replayed);
            Require(ledger.Complete(Start(ledger, retry), CommitResult.Applied, 1) == HostRequestCompletionStatus.Recorded);
            Require(ledger.BoundRequestCount == 2 && ledger.TryBegin(retry, retry.ActorId).Status == HostRequestBeginStatus.Replayed);
        });

        test("ledger quarantines uncertain completion and never releases applied work for retry", () =>
        {
            foreach ((CommitResult result, long? revision) in new (CommitResult, long?)[] {
                (CommitResult.Applied, null), (CommitResult.Applied, 0), (CommitResult.Applied, 2),
                (CommitResult.Applied, long.MaxValue), (CommitResult.RolledBack, 1),
                (CommitResult.RecoveryFailed, null), (CommitResult.Quarantined, null), ((CommitResult)999, null)
            })
            {
                var ledger = NewLedger(); var request = NewRequest(ledger); var lease = Start(ledger, request);
                var pending = Start(ledger, NewRequest(ledger));
                Require(ledger.Complete(lease, result, revision) == HostRequestCompletionStatus.Quarantined);
                Require(ledger.IsQuarantined && ledger.BoundRequestCount == 2);
                Require(ledger.TryBegin(request, request.ActorId).Status == HostRequestBeginStatus.Quarantined);
                Require(ledger.TryBegin(NewRequest(ledger), request.ActorId).Status == HostRequestBeginStatus.Quarantined);
                Require(ledger.ReleaseForRetry(pending) == HostRequestCompletionStatus.Quarantined);
                Require(ledger.Complete(pending, CommitResult.Applied, 1) == HostRequestCompletionStatus.Quarantined);
            }
            var terminal = NewLedger(); var successful = NewRequest(terminal);
            Require(terminal.Complete(Start(terminal, successful), CommitResult.Applied, 1) == HostRequestCompletionStatus.Recorded);
            Require(terminal.Quarantine() == HostRequestCompletionStatus.Quarantined);
            Require(terminal.TryBegin(successful, successful.ActorId).Status == HostRequestBeginStatus.Quarantined);
        });
    }

    private static HostRequestLedger NewLedger() => new(Guid.NewGuid());
    private static MachineRequestEnvelope NewRequest(HostRequestLedger ledger) => new(
        MachineRequestEnvelope.CurrentProtocol, ledger.SessionId, Guid.NewGuid(), 10,
        "FarmHouse", 4, 6, Guid.NewGuid(), 0, MachineRequestAction.Start, "tofu");
    private static HostRequestLease Start(HostRequestLedger ledger, MachineRequestEnvelope request)
    {
        var result = ledger.TryBegin(request, request.ActorId);
        Require(result.Status == HostRequestBeginStatus.Started && result.Lease is not null && result.Receipt is null);
        return result.Lease!;
    }
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Host request ledger assertion failed.");
    }
}
