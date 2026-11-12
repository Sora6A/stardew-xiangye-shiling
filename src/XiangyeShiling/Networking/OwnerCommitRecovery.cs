namespace XiangyeShiling.Networking;

internal enum OwnerReservationStatus { Reserved, Applied, Cancelled }
internal enum OwnerJournalObservation { Absent, Matching, Foreign }
internal enum ReservedMachineObservation { Before, After, Conflict }

internal enum OwnerRecoveryAction
{
    Wait, CommitMachine, RecordAppliedReceipt, ReplayApplied, CancelReservation, ReplayCancelled, Isolate
}

/// <summary>
/// Reconcile one outstanding host reservation and observations already bound to its world, actor and immutable
/// intent. This class never authorizes an actor, mutates a slot or resolves older acknowledged requests.
/// </summary>
internal static class OwnerCommitRecovery
{
    /// <param name="reservation">Status from the host's own save-backed record.</param>
    /// <param name="machine">Comparison with both exact machine states in that record.</param>
    /// <param name="inventory">Classification of the planned slots from the actual farmer inventory.</param>
    /// <param name="journal">Comparison with the owner record in the same observed farmer.</param>
    /// <param name="settledCheckpoint">
    /// True only when inventory, journal and machine were read from one authoritative checkpoint and the old
    /// grant can no longer execute. A disconnect, deadline or missing message alone does not set this.
    /// </param>
    public static OwnerRecoveryAction Decide(OwnerReservationStatus reservation, ReservedMachineObservation machine,
        OwnerInventoryObservation inventory, OwnerJournalObservation journal, bool settledCheckpoint)
    {
        if (machine is not (ReservedMachineObservation.Before or ReservedMachineObservation.After) ||
            journal is not (OwnerJournalObservation.Absent or OwnerJournalObservation.Matching) ||
            inventory is not (OwnerInventoryObservation.Before or OwnerInventoryObservation.After or OwnerInventoryObservation.Partial))
            return OwnerRecoveryAction.Isolate;

        bool ownerApplied = inventory == OwnerInventoryObservation.After && journal == OwnerJournalObservation.Matching;
        bool ownerUntouched = inventory == OwnerInventoryObservation.Before && journal == OwnerJournalObservation.Absent;
        return reservation switch
        {
            OwnerReservationStatus.Reserved when ownerApplied => machine == ReservedMachineObservation.Before
                ? OwnerRecoveryAction.CommitMachine : OwnerRecoveryAction.RecordAppliedReceipt,
            OwnerReservationStatus.Reserved when machine == ReservedMachineObservation.After => OwnerRecoveryAction.Isolate,
            OwnerReservationStatus.Reserved when settledCheckpoint => ownerUntouched
                ? OwnerRecoveryAction.CancelReservation : OwnerRecoveryAction.Isolate,
            OwnerReservationStatus.Reserved => OwnerRecoveryAction.Wait,
            OwnerReservationStatus.Applied when machine == ReservedMachineObservation.After && ownerApplied
                => OwnerRecoveryAction.ReplayApplied,
            OwnerReservationStatus.Cancelled when machine == ReservedMachineObservation.Before && ownerUntouched
                => OwnerRecoveryAction.ReplayCancelled,
            _ => OwnerRecoveryAction.Isolate
        };
    }
}
