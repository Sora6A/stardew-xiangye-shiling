using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using XiangyeShiling.Transactions;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// Execute an authenticated host grant on the local farmhand's main thread. The caller binds the live
/// host session and authorization before invoking this adapter. No machine or remote inventory is written.
/// </summary>
internal sealed class OwnerInventoryCommit
{
    internal const string JournalKey = MachineService.Prefix + "/OwnerCommitJournal";
    private readonly IMonitor Monitor;
    private readonly int GameThread = Environment.CurrentManagedThreadId;
    private CommitCoordinator Coordinator = new();
    private bool Saving;
    private bool Applying;
    private bool JournalQuarantined;

    internal OwnerInventoryCommit(IMonitor monitor) => this.Monitor = monitor;

    internal void OnSaving() => this.Saving = true;
    internal void OnSaved() => this.Saving = false;

    internal void Reset()
    {
        this.Saving = false;
        this.JournalQuarantined = false;
        this.Coordinator = new();
    }

    /// <summary>Compare only the given farmer's planned slots on the game thread, including on the host's copy.</summary>
    internal static OwnerInventoryObservation Observe(Farmer owner, OwnerInventoryWriteSet writeSet)
    {
        IInventory items = owner.Items;
        if (writeSet.Capacity != owner.MaxItems || writeSet.Capacity != items.Count)
            return OwnerInventoryObservation.Conflict;

        var digests = new string?[writeSet.Capacity];
        foreach (OwnerSlotChange change in writeSet.Changes)
            if (!OwnerInventoryFingerprint.TryReadObserved(items[change.Index], out digests[change.Index]))
                return OwnerInventoryObservation.Conflict;
        return writeSet.Observe(digests);
    }

    internal static OwnerJournalObservation ObserveJournal(Farmer owner, OwnerCommitReservation reservation)
        => !owner.modData.TryGetValue(JournalKey, out string raw) ? OwnerJournalObservation.Absent
            : raw == reservation.OwnerJournal ? OwnerJournalObservation.Matching : OwnerJournalObservation.Foreign;

    internal static string SeenJournal(OwnerCommitReservation reservation) => reservation.OwnerJournal + ":Seen";

    /// <summary>The caller has authenticated the terminal native host record for this owner.</summary>
    internal bool TryMarkSeen(Farmer owner, OwnerCommitReservation reservation)
    {
        bool applied = reservation.Status == OwnerReservationStatus.Applied;
        if (!applied && reservation.Status != OwnerReservationStatus.Cancelled) return false;
        return this.TryWriteJournal(owner, applied ? reservation.OwnerJournal : null, SeenJournal(reservation),
            () => this.IsCurrentOwner(owner, reservation), () => Observe(owner, reservation.WriteSet)
                == (applied ? OwnerInventoryObservation.After : OwnerInventoryObservation.Before));
    }

    /// <summary>The caller has read the matching native host receipt and confirmed retirement of its record.</summary>
    internal bool TryClearSeen(Farmer owner, string expectedSeen)
    {
        if (!expectedSeen.EndsWith(":Seen", StringComparison.Ordinal) || !this.CanStart() || !this.IsCurrentOwner(owner))
            return false;
        ulong world = Game1.uniqueIDForThisGame;
        long host = Game1.MasterPlayer.UniqueMultiplayerID;
        return this.TryWriteJournal(owner, expectedSeen, null, () => this.IsCurrentOwner(owner)
            && Game1.uniqueIDForThisGame == world && Game1.MasterPlayer.UniqueMultiplayerID == host);
    }

    /// <summary>
    /// True means the local planned slots and owner journal match the grant. It includes exact replay
    /// and says nothing about synchronization, native save completion or the host machine's state.
    /// </summary>
    internal bool TryApply(Farmer owner, OwnerCommitReservation reservation)
    {
        if (!this.CanStart()
            || reservation.Status != OwnerReservationStatus.Reserved || !this.IsCurrentOwner(owner, reservation))
            return false;

        this.Applying = true;
        try
        {
            OwnerJournalObservation journal = ObserveJournal(owner, reservation);
            if (journal == OwnerJournalObservation.Foreign) return false;
            OwnerInventoryObservation inventory = Observe(owner, reservation.WriteSet);
            if (journal == OwnerJournalObservation.Matching)
                return inventory == OwnerInventoryObservation.After && !this.Saving && this.IsCurrentOwner(owner, reservation)
                    && ObserveJournal(owner, reservation) == OwnerJournalObservation.Matching;
            if (inventory != OwnerInventoryObservation.Before) return false;

            IInventory items = owner.Items;
            Item[] originals = items.ToArray();
            if (!TryPrepare(reservation, originals, out var changes, out var replacements)) return false;
            var target = new PreparedCommit(this, owner, reservation, items, originals, changes!, replacements!);
            return this.Coordinator.Commit(() => target, this.Report) == CommitResult.Applied;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Report(error);
            return false;
        }
        finally { this.Applying = false; }
    }

    private bool CanStart() => !this.Applying && !this.Saving && !this.JournalQuarantined && !this.Coordinator.IsQuarantined;

    private bool IsCurrentOwner(Farmer owner)
        => Environment.CurrentManagedThreadId == this.GameThread && Context.IsWorldReady && Context.IsMultiplayer
            && !Context.IsMainPlayer && !Context.IsSplitScreen && Context.ScreenId == 0
            && ReferenceEquals(owner, Game1.player) && owner.UniqueMultiplayerID != 0 && Game1.uniqueIDForThisGame != 0
            && Game1.MasterPlayer is { UniqueMultiplayerID: not 0 } host && host.UniqueMultiplayerID != owner.UniqueMultiplayerID;

    private bool IsCurrentOwner(Farmer owner, OwnerCommitReservation reservation) => this.IsCurrentOwner(owner)
        && owner.UniqueMultiplayerID == reservation.Request.ActorId && Game1.uniqueIDForThisGame == reservation.WorldId
        && Game1.MasterPlayer.UniqueMultiplayerID == reservation.HostId;

    private bool TryWriteJournal(Farmer owner, string? expected, string? value, Func<bool> owns,
        Func<bool>? matchesInventory = null)
    {
        if (!this.CanStart() || !owns()) return false;
        this.Applying = true;
        bool attempted = false;
        try
        {
            if (matchesInventory?.Invoke() == false || this.Saving || !owns()) return false;
            if (value is not null && Matches(value)) return true;
            if (!Matches(expected)) return false;

            // The native net dictionary setter can notify after writing. Keep the actual value and stop
            // further owner commits when a callback leaves the terminal handoff ambiguous.
            attempted = true;
            if (value is null) owner.modData.Remove(JournalKey);
            else owner.modData[JournalKey] = value;
            if (owns() && matchesInventory?.Invoke() != false && Matches(value)) return true;
            this.JournalQuarantined = true;
            return false;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (attempted) this.JournalQuarantined = true;
            this.Report(error);
            return false;
        }
        finally { this.Applying = false; }

        bool Matches(string? raw) => owner.modData.TryGetValue(JournalKey, out string current)
            ? raw is not null && current == raw : raw is null;
    }

    private void Report(Exception error)
        => this.Monitor.Log("Owner inventory transaction failed: " + error.GetType().Name, LogLevel.Error);

    private static bool TryPrepare(OwnerCommitReservation reservation, Item[] originals,
        out IReadOnlyList<SlotChange>? changes, out Item?[]? replacements)
    {
        changes = null;
        replacements = null;
        if (!MachineStateCodec.TryRead(reservation.BeforeRaw, out MachineState? before)
            || !MachineStateCodec.TryRead(reservation.AfterRaw, out MachineState? after)) return false;

        Item? output = null;
        if (reservation.Request.Action == MachineRequestAction.Start)
        {
            // The host resolved the recipe and froze exact per-slot consumption in the next machine state.
            InventorySlot?[] slots = OwnerInventoryPlanning.Snapshot(originals, null);
            var consumption = new List<SlotChange>();
            foreach (ConsumedStack input in after!.Batch!.Inputs)
            {
                if (input.Slot >= slots.Length || slots[input.Slot] is not { } slot || slot.ItemId != input.ItemId
                    || slot.Quality != input.Quality || slot.Quantity < input.Quantity) return false;
                consumption.Add(new SlotChange(input.Slot, slot, slot.Quantity - input.Quantity));
            }
            changes = consumption;
        }
        else
        {
            Batch batch = before!.Batch!;
            if (!OwnerInventoryPlanning.TryCreateOutput(batch.Output, out output)) return false;
            var request = reservation.Request;
            // The authenticated host checks readiness with its clock. Replay the frozen ready batch without
            // consulting the farmhand's potentially delayed clock, and require the exact authorized result.
            var decision = MachineRules.Collect(before, new Request(request.RequestId, request.InstanceId,
                request.ExpectedRevision, request.ActorId), OwnerInventoryPlanning.Snapshot(originals, output), batch.ReadyAt);
            if (!decision.Accepted || MachineStateCodec.Write(decision.Transaction!.After) != reservation.AfterRaw) return false;
            changes = decision.Transaction.Slots;
        }

        return OwnerInventoryPlanning.TryDescribeChanges(originals, changes, output, out var actual, out replacements)
            && actual!.Capacity == reservation.WriteSet.Capacity
            && actual.Changes.OrderBy(change => change.Index)
                .SequenceEqual(reservation.WriteSet.Changes.OrderBy(change => change.Index));
    }

    private sealed class PreparedCommit : IPreparedCommit
    {
        private readonly OwnerInventoryCommit Adapter;
        private readonly Farmer Owner;
        private readonly OwnerCommitReservation Reservation;
        private readonly IInventory Items;
        private readonly Item[] Originals;
        private readonly IReadOnlyList<SlotChange> Changes;
        private readonly Item?[] Replacements;
        private int AttemptedSlots;
        private bool AttemptedJournal;

        internal PreparedCommit(OwnerInventoryCommit adapter, Farmer owner, OwnerCommitReservation reservation,
            IInventory items, Item[] originals, IReadOnlyList<SlotChange> changes, Item?[] replacements)
        {
            this.Adapter = adapter;
            this.Owner = owner;
            this.Reservation = reservation;
            this.Items = items;
            this.Originals = originals;
            this.Changes = changes;
            this.Replacements = replacements;
        }

        private bool OwnsInventory() => this.Adapter.IsCurrentOwner(this.Owner, this.Reservation)
            && ReferenceEquals(this.Owner.Items, this.Items) && this.Owner.MaxItems == this.Originals.Length
            && this.Items.Count == this.Originals.Length;

        public bool MatchesOriginal() => !this.Adapter.Saving && this.IsRestored();

        public void ApplyInventory()
        {
            // The coordinator checks every original immediately before writing. Recheck a slot's native
            // digest before its setter because an earlier slot's callback can mutate an upcoming item in place.
            foreach (SlotChange change in this.Changes)
            {
                int index = change.Slot;
                OwnerSlotChange expected = this.Reservation.WriteSet.Changes.Single(slot => slot.Index == index);
                if (this.Adapter.Saving || !this.OwnsInventory()
                    || !ReferenceEquals(this.Items[index], this.Originals[index])
                    || !OwnerInventoryFingerprint.TryReadObserved(this.Items[index], out string? digest) || digest != expected.Before)
                    throw new InvalidOperationException("The owner slot changed during the transaction.");
                int next = this.AttemptedSlots++;
                this.Items[index] = this.Replacements[next];
            }
        }

        public void ApplyState()
        {
            if (this.Adapter.Saving || !this.OwnsInventory()
                || Observe(this.Owner, this.Reservation.WriteSet) != OwnerInventoryObservation.After
                || ObserveJournal(this.Owner, this.Reservation) != OwnerJournalObservation.Absent)
                throw new InvalidOperationException("The owner inventory or journal changed before marking the grant.");
            this.AttemptedJournal = true;
            this.Owner.modData[JournalKey] = this.Reservation.OwnerJournal;
        }

        public bool IsApplied() => this.OwnsInventory()
            && Observe(this.Owner, this.Reservation.WriteSet) == OwnerInventoryObservation.After
            && ObserveJournal(this.Owner, this.Reservation) == OwnerJournalObservation.Matching;

        public void RestoreInventory()
        {
            Exception? firstError = null;
            for (int i = this.AttemptedSlots - 1; i >= 0 && this.OwnsInventory(); i--)
            {
                int slot = this.Changes[i].Slot;
                // A native callback can throw after assignment. Restore only our own installed references;
                // the final verification quarantines the adapter if another callback took over a slot.
                if (!ReferenceEquals(this.Items[slot], this.Replacements[i])) continue;
                try { this.Items[slot] = this.Originals[slot]; }
                catch (Exception error) { firstError ??= error; }
            }
            if (firstError is not null) throw firstError;
        }

        public void RestoreState()
        {
            if (this.AttemptedJournal && this.OwnsInventory()
                && ObserveJournal(this.Owner, this.Reservation) == OwnerJournalObservation.Matching)
                this.Owner.modData.Remove(JournalKey);
        }

        public bool IsRestored() => this.OwnsInventory()
            && this.Changes.All(change => ReferenceEquals(this.Items[change.Slot], this.Originals[change.Slot]))
            && Observe(this.Owner, this.Reservation.WriteSet) == OwnerInventoryObservation.Before
            && ObserveJournal(this.Owner, this.Reservation) == OwnerJournalObservation.Absent;
    }
}
