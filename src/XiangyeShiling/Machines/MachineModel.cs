using System.Text.Json.Serialization;

namespace XiangyeShiling.Machines;

public enum MachineKind { BeanWorkshop, FermentationCrock }
public enum MachinePhase { Idle, Processing, Ready }

/// <summary>All IDs are qualified item IDs, independent of translated display names.</summary>
public sealed record Ingredient(string ItemId, int Quantity);
public sealed record Product(string ItemId, int Quantity, int Quality, int MaxStack);
public sealed record ConsumedStack(int Slot, string ItemId, int Quantity, int Quality);

/// <summary>A host-resolved recipe, never a recipe supplied by a client's message.</summary>
public sealed record MachineRecipe(string Id, MachineKind Machine, IReadOnlyList<Ingredient> Inputs,
    Product Output, int Minutes);

/// <summary>Values are fixed at start, so changing recipe balance cannot change a running batch.</summary>
public sealed record Batch(Guid Id, string RecipeId, IReadOnlyList<ConsumedStack> Inputs,
    Product Output, long StartedAt, long ReadyAt);

/// <summary>Keep the revision when collecting. Assign a new instance ID when placing a new machine.</summary>
public sealed record MachineState(int SchemaVersion, Guid InstanceId, MachineKind Kind, long Revision,
    Batch? Batch, Guid? LastRequestId, long? LastActorId)
{
    public const int CurrentSchema = 1;

    public static MachineState Empty(Guid instanceId, MachineKind kind) =>
        new(CurrentSchema, instanceId, kind, 0, null, null, null);

    public MachinePhase PhaseAt(long totalMinutes) => this.Batch is null ? MachinePhase.Idle
        : totalMinutes >= this.Batch.ReadyAt ? MachinePhase.Ready : MachinePhase.Processing;

    // Readiness doesn't make a machine movable. A successful collection does.
    [JsonIgnore]
    public bool CanMove => this.Batch is null;
}

/// <summary>Actor identity and time must come from the host's authenticated game context.</summary>
public sealed record Request(Guid Id, Guid InstanceId, long ExpectedRevision, long ActorId);

/// <summary>
/// An occupied inventory slot. The adapter computes CanStackOutput using the game's actual
/// canStackWith result for THIS batch's output, preserving mod data and other stacking rules.
/// Unknown or incompatible items must set it to false.
/// </summary>
public sealed record InventorySlot(string ItemId, int Quantity, int Quality, int MaxStack,
    bool CanStackOutput = false);

/// <summary>
/// A plan against an exact inventory snapshot. A zero NewQuantity empties a slot; an empty
/// Before creates the batch output. Existing item instances and metadata must be preserved.
/// </summary>
public sealed record SlotChange(int Slot, InventorySlot? Before, int NewQuantity);

public enum Rejection
{
    None, InvalidState, InvalidRequest, DuplicateRequest, StaleRevision, WrongInstance,
    InvalidInventory, InvalidRecipe, WrongMachine, LockedRecipe, Busy, MissingIngredients,
    Empty, NotReady, InventoryFull
}

/// <summary>
/// A pure plan, not an applied game mutation. The host adapter must revalidate inventory and
/// machine snapshots under its transaction lock, then commit both together or neither.
/// Never apply the slot changes in an event handler without that adapter.
/// </summary>
public sealed record MachineTransaction(MachineState Before, MachineState After,
    IReadOnlyList<SlotChange> Slots);

public sealed record Decision(Rejection Rejection, MachineTransaction? Transaction)
{
    public bool Accepted => this.Rejection == Rejection.None && this.Transaction is not null;
    public static Decision Reject(Rejection reason) => new(reason, null);
    public static Decision Accept(MachineTransaction transaction) => new(Rejection.None, transaction);
}
