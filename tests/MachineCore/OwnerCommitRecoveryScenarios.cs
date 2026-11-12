using XiangyeShiling.Networking;
using static XiangyeShiling.Networking.OwnerReservationStatus;
using static XiangyeShiling.Networking.OwnerRecoveryAction;

internal static class OwnerCommitRecoveryScenarios
{
    public static void Run(Action<string, Action> test)
    {
        const ReservedMachineObservation beforeMachine = ReservedMachineObservation.Before;
        const ReservedMachineObservation afterMachine = ReservedMachineObservation.After;
        const OwnerInventoryObservation beforeInventory = OwnerInventoryObservation.Before;
        const OwnerInventoryObservation afterInventory = OwnerInventoryObservation.After;
        const OwnerJournalObservation missingJournal = OwnerJournalObservation.Absent;
        const OwnerJournalObservation matchingJournal = OwnerJournalObservation.Matching;

        test("owner recovery requires inventory and journal to arrive before host commit", () =>
        {
            Expect(Wait, Reserved, beforeMachine, beforeInventory, matchingJournal);
            Expect(Wait, Reserved, beforeMachine, afterInventory, missingJournal);
            Expect(Wait, Reserved, beforeMachine, OwnerInventoryObservation.Partial, matchingJournal);
            Expect(CommitMachine, Reserved, beforeMachine, afterInventory, matchingJournal);
        });

        test("owner recovery fences a provably untouched grant before cancelling", () =>
        {
            Expect(Wait, Reserved, beforeMachine, beforeInventory, missingJournal);
            Expect(CancelReservation, Reserved, beforeMachine, beforeInventory, missingJournal, settled: true);
            Expect(Isolate, Reserved, beforeMachine, OwnerInventoryObservation.Partial, matchingJournal, settled: true);
            Expect(Isolate, Reserved, beforeMachine, afterInventory, missingJournal, settled: true);
        });

        test("owner recovery replays the result without a second inventory or machine write", () =>
        {
            Expect(RecordAppliedReceipt, Reserved, afterMachine, afterInventory, matchingJournal, settled: true);
            Expect(ReplayApplied, Applied, afterMachine, afterInventory, matchingJournal, settled: true);
            Expect(ReplayCancelled, Cancelled, beforeMachine, beforeInventory, missingJournal);
        });

        test("owner recovery isolates inconsistent machine, owner or terminal records", () =>
        {
            Expect(Isolate, Reserved, afterMachine, afterInventory, missingJournal);
            Expect(Isolate, Applied, beforeMachine, afterInventory, matchingJournal);
            Expect(Isolate, Applied, afterMachine, afterInventory, missingJournal);
            Expect(Isolate, Cancelled, beforeMachine, afterInventory, matchingJournal);
            Expect(Isolate, Reserved, ReservedMachineObservation.Conflict, afterInventory, matchingJournal);
            Expect(Isolate, Reserved, beforeMachine, OwnerInventoryObservation.Conflict, matchingJournal);
            Expect(Isolate, Reserved, beforeMachine, afterInventory, OwnerJournalObservation.Foreign);
        });
    }

    private static void Expect(OwnerRecoveryAction expected, OwnerReservationStatus reservation,
        ReservedMachineObservation machine, OwnerInventoryObservation inventory, OwnerJournalObservation journal,
        bool settled = false)
    {
        var actual = OwnerCommitRecovery.Decide(reservation, machine, inventory, journal, settled);
        if (actual != expected)
            throw new InvalidOperationException($"Expected {expected}, got {actual}: {reservation}/{machine}/{inventory}/{journal}.");
    }
}
