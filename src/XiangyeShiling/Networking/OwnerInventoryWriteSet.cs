namespace XiangyeShiling.Networking;

internal enum OwnerInventoryObservation { Before, After, Partial, Conflict }

// Each non-empty value is a canonical SHA-256 of one native slot, including its stack.
// A null value identifies an empty slot, never an unavailable or unsupported item.
internal readonly record struct OwnerSlotChange(int Index, string? Before, string? After);

/// <summary>
/// An immutable subset of the host-authorized inventory plan. The native adapter supplies fingerprints from
/// its own observations. Asynchronous replication can expose either side of a slot while another is in flight.
/// An observation does not authorize a write or prove the farmer journal was received or persisted.
/// </summary>
internal sealed class OwnerInventoryWriteSet
{
    public int Capacity { get; }
    public IReadOnlyList<OwnerSlotChange> Changes { get; }

    private OwnerInventoryWriteSet(int capacity, OwnerSlotChange[] changes)
    {
        this.Capacity = capacity;
        this.Changes = Array.AsReadOnly(changes);
    }

    public static OwnerInventoryWriteSet? Create(int capacity, IReadOnlyList<OwnerSlotChange>? changes)
    {
        if (capacity <= 0 || changes is null || changes.Count == 0 || changes.Count > capacity)
            return null;

        var copy = changes.ToArray();
        var indices = new HashSet<int>();
        foreach (var change in copy)
        {
            if (change.Index < 0 || change.Index >= capacity || !indices.Add(change.Index) ||
                change.Before == change.After || !ValidFingerprint(change.Before) || !ValidFingerprint(change.After))
                return null;
        }
        return new OwnerInventoryWriteSet(capacity, copy);
    }

    /// <summary>
    /// Observe only affected slots. An adapter must still validate the owner, plan identity and live references
    /// before applying a mutation. Partial and conflict observations cannot certify an owner commit.
    /// </summary>
    public OwnerInventoryObservation Observe(IReadOnlyList<string?>? inventory)
    {
        if (inventory is null || inventory.Count != this.Capacity)
            return OwnerInventoryObservation.Conflict;

        bool before = false, after = false;
        foreach (var change in this.Changes)
        {
            string? observed = inventory[change.Index];
            if (observed == change.Before) before = true;
            else if (observed == change.After) after = true;
            else return OwnerInventoryObservation.Conflict;
        }

        if (before && after) return OwnerInventoryObservation.Partial;
        return before ? OwnerInventoryObservation.Before : OwnerInventoryObservation.After;
    }

    private static bool ValidFingerprint(string? value)
        => value is null || (value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
}
