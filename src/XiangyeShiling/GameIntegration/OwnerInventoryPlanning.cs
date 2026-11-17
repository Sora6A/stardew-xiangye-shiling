using System.Diagnostics.CodeAnalysis;
using StardewValley;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// Builds the native portion of an owner plan on the game thread. References in the input snapshot
/// are borrowed for reading. The replacements are isolated registry objects in the order of the changes.
/// </summary>
internal static class OwnerInventoryPlanning
{
    internal static bool TryCreateOutput(Product? product, [NotNullWhen(true)] out Item? output)
    {
        output = null;
        if (!MachineRules.ValidProduct(product)) return false;
        Item? item = ItemRegistry.Create(product!.ItemId, 1, product.Quality, allowNull: true);
        if (item is null || item.QualifiedItemId != product.ItemId || item.Stack != 1
            || item.Quality != product.Quality || item.maximumStackSize() != product.MaxStack
            || !OwnerInventoryFingerprint.TryReadObserved(item, out _))
            return false;
        output = item;
        return true;
    }

    internal static InventorySlot?[] Snapshot(IReadOnlyList<Item> items, Item? output) => items.Select(item =>
        item is null ? null : new InventorySlot(item.QualifiedItemId, item.Stack, item.Quality, item.maximumStackSize(),
            output is not null && item.QualifiedItemId == output.QualifiedItemId && item.Quality == output.Quality
                && item.canStackWith(output))).ToArray();

    /// <summary>Certifies only the changed slots. An unrepresentable affected item rejects the entire plan.</summary>
    internal static bool TryDescribeChanges(IReadOnlyList<Item> items, IReadOnlyList<SlotChange> changes, Item? output,
        [NotNullWhen(true)] out OwnerInventoryWriteSet? set, [NotNullWhen(true)] out Item?[]? replacements)
    {
        set = null;
        replacements = null;
        if (items.Count is <= 0 or > OwnerCommitReservation.MaxCapacity || changes.Count == 0 || changes.Count > items.Count)
            return false;

        var proof = new OwnerSlotChange[changes.Count];
        var prepared = new Item?[changes.Count];
        for (int i = 0; i < changes.Count; i++)
        {
            SlotChange change = changes[i];
            if (change.Slot < 0 || change.Slot >= items.Count || change.NewQuantity < 0) return false;
            Item? original = items[change.Slot];
            InventorySlot? before = change.Before;
            if (before is null ? original is not null
                : original is null || original.QualifiedItemId != before.ItemId || original.Stack != before.Quantity
                    || original.Quality != before.Quality || original.maximumStackSize() != before.MaxStack)
                return false;
            if (!OwnerInventoryFingerprint.TryRead(original, out string? beforeFingerprint)) return false;

            string? afterFingerprint = null;
            if (change.NewQuantity > 0)
            {
                Item? exemplar = original ?? output;
                if (exemplar is null || change.NewQuantity > exemplar.maximumStackSize()) return false;
                Item? replacement = ItemRegistry.Create(exemplar.QualifiedItemId, change.NewQuantity, exemplar.Quality, allowNull: true);
                if (replacement is null || ReferenceEquals(replacement, original) || ReferenceEquals(replacement, output)
                    || replacement.QualifiedItemId != exemplar.QualifiedItemId || replacement.Stack != change.NewQuantity
                    || replacement.Quality != exemplar.Quality || replacement.maximumStackSize() != exemplar.maximumStackSize())
                    return false;
                // Orientation is excluded from the fingerprint, but an existing stack keeps its native visual state.
                if (original is SObject beforeObject && replacement is SObject afterObject)
                    afterObject.Flipped = beforeObject.Flipped;
                if (!OwnerInventoryFingerprint.TryReadObserved(replacement, out afterFingerprint)) return false;
                prepared[i] = replacement;
            }
            proof[i] = new(change.Slot, beforeFingerprint, afterFingerprint);
        }

        set = OwnerInventoryWriteSet.Create(items.Count, proof);
        if (set is null) return false;
        replacements = prepared;
        return true;
    }
}
