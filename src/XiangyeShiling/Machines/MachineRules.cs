namespace XiangyeShiling.Machines;

/// <summary>Shared, game-independent transaction planning for the two custom machines.</summary>
public static class MachineRules
{
    /// <summary>依据主机快照生成完整开工计划；任一原料不足时不返回部分扣料。</summary>
    /// <param name="state">本次操作读取的设备状态，必须与提交时的实例/修订号一致。</param>
    /// <param name="request">身份来自主机认证上下文；请求 ID 不能在重试时重新生成。</param>
    /// <param name="recipe">主机查出的合法配方；不能信任客户端传来的数量、产物或工时。</param>
    /// <param name="recipeUnlocked">由主机核验的解锁结果。</param>
    /// <param name="inventory">稳定的槽位快照；调用期间及计划提交前不能由调用者修改。</param>
    /// <param name="totalMinutes">主机转换后的跨日连续分钟，不是游戏 HHMM 格式。</param>
    /// <returns>拒绝原因，或包含所有槽位变化与新状态的计划；这里不修改游戏数据。</returns>
    public static Decision Start(MachineState state, Request request, MachineRecipe recipe,
        bool recipeUnlocked, IReadOnlyList<InventorySlot?> inventory, long totalMinutes)
    {
        Rejection rejection = ValidateRequest(state, request, totalMinutes);
        if (rejection != Rejection.None)
            return Decision.Reject(rejection);
        if (state.Batch is not null)
            return Decision.Reject(Rejection.Busy);
        if (!ValidRecipe(recipe) || totalMinutes > long.MaxValue - recipe.Minutes)
            return Decision.Reject(Rejection.InvalidRecipe);
        if (recipe.Machine != state.Kind)
            return Decision.Reject(Rejection.WrongMachine);
        if (!recipeUnlocked)
            return Decision.Reject(Rejection.LockedRecipe);
        if (!ValidInventory(inventory))
            return Decision.Reject(Rejection.InvalidInventory);

        var consumed = new List<ConsumedStack>();
        var changes = new List<SlotChange>();
        // Input IDs are unique (validated above). Each slot can therefore be consumed only once.
        foreach (Ingredient input in recipe.Inputs)
        {
            int remaining = input.Quantity;
            for (int slotIndex = 0; slotIndex < inventory.Count && remaining > 0; slotIndex++)
            {
                InventorySlot? slot = inventory[slotIndex];
                if (slot is null || slot.ItemId != input.ItemId)
                    continue;
                if (!ValidQuality(slot.Quality))
                    return Decision.Reject(Rejection.InvalidInventory);
                int quantity = Math.Min(remaining, slot.Quantity);
                consumed.Add(new(slotIndex, slot.ItemId, quantity, slot.Quality));
                changes.Add(new(slotIndex, slot, slot.Quantity - quantity));
                remaining -= quantity;
            }
            if (remaining > 0)
                return Decision.Reject(Rejection.MissingIngredients);
        }

        var batch = new Batch(request.Id, recipe.Id, consumed.AsReadOnly(), recipe.Output,
            totalMinutes, totalMinutes + recipe.Minutes);
        MachineState next = state with
        {
            Revision = state.Revision + 1, Batch = batch,
            LastRequestId = request.Id, LastActorId = request.ActorId
        };
        return Decision.Accept(new(state, next, changes.AsReadOnly()));
    }

    /// <summary>计划完整领取固定批次；背包满或尚未完成时保留原批次，不给部分产品。</summary>
    /// <remarks>
    /// CanStackOutput 必须来自游戏对本批次产物的 canStackWith 判断。
    /// 规则不提供互斥锁；适配器须在提交前重新核对快照并保证库存/设备一起生效。
    /// </remarks>
    public static Decision Collect(MachineState state, Request request,
        IReadOnlyList<InventorySlot?> inventory, long totalMinutes)
    {
        Rejection rejection = ValidateRequest(state, request, totalMinutes);
        if (rejection != Rejection.None)
            return Decision.Reject(rejection);
        if (state.Batch is null)
            return Decision.Reject(Rejection.Empty);
        if (state.PhaseAt(totalMinutes) != MachinePhase.Ready)
            return Decision.Reject(Rejection.NotReady);
        if (!ValidInventory(inventory))
            return Decision.Reject(Rejection.InvalidInventory);

        Product output = state.Batch.Output;
        int remaining = output.Quantity;
        var changes = new List<SlotChange>();
        // Fill compatible stacks first, then empty slots. Never return a partial collection.
        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            InventorySlot? slot = inventory[i];
            if (slot is null || !slot.CanStackOutput || slot.ItemId != output.ItemId || slot.Quality != output.Quality)
                continue;
            int free = Math.Max(0, Math.Min(slot.MaxStack, output.MaxStack) - slot.Quantity);
            int quantity = Math.Min(remaining, free);
            if (quantity == 0)
                continue;
            changes.Add(new(i, slot, slot.Quantity + quantity));
            remaining -= quantity;
        }
        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            if (inventory[i] is not null)
                continue;
            int quantity = Math.Min(remaining, output.MaxStack);
            changes.Add(new(i, null, quantity));
            remaining -= quantity;
        }
        if (remaining > 0)
            return Decision.Reject(Rejection.InventoryFull);

        MachineState next = state with
        {
            Revision = state.Revision + 1, Batch = null,
            LastRequestId = request.Id, LastActorId = request.ActorId
        };
        return Decision.Accept(new(state, next, changes.AsReadOnly()));
    }

    private static Rejection ValidateRequest(MachineState state, Request request, long totalMinutes)
    {
        if (!MachineStateCodec.IsValid(state) || state.Revision == long.MaxValue || totalMinutes < 0)
            return Rejection.InvalidState;
        if (request is null || request.Id == Guid.Empty || request.ActorId == 0 || request.ExpectedRevision < 0)
            return Rejection.InvalidRequest;
        if (request.InstanceId != state.InstanceId)
            return Rejection.WrongInstance;
        if (state.LastRequestId == request.Id)
            return state.LastActorId == request.ActorId ? Rejection.DuplicateRequest : Rejection.InvalidRequest;
        // LastRequest 只能识别最近一笔；更早的重放依靠永不回退的 Revision 拒绝。
        if (request.ExpectedRevision != state.Revision)
            return Rejection.StaleRevision;
        return Rejection.None;
    }

    internal static bool ValidProduct(Product? output) => output is not null
        && ValidItemId(output.ItemId) && output.Quantity > 0 && ValidQuality(output.Quality) && output.MaxStack > 0;

    internal static bool ValidQuality(int quality) => quality is 0 or 1 or 2 or 4;
    internal static bool ValidItemId(string? id) => id is not null && id.StartsWith("(O)", StringComparison.Ordinal)
        && id.Length > 3 && !id.Any(char.IsWhiteSpace);

    private static bool ValidRecipe(MachineRecipe? recipe) => recipe is not null
        && !string.IsNullOrWhiteSpace(recipe.Id) && Enum.IsDefined(typeof(MachineKind), recipe.Machine)
        && recipe.Minutes > 0 && ValidProduct(recipe.Output)
        && recipe.Inputs is { Count: > 0 }
        && recipe.Inputs.All(i => i is not null && ValidItemId(i.ItemId) && i.Quantity > 0)
        && recipe.Inputs.Select(i => i.ItemId).Distinct(StringComparer.Ordinal).Count() == recipe.Inputs.Count;

    private static bool ValidInventory(IReadOnlyList<InventorySlot?>? inventory) => inventory is not null
        && inventory.All(s => s is null || (!string.IsNullOrWhiteSpace(s.ItemId) && s.Quantity > 0
            && s.Quality >= 0 && s.MaxStack > 0));
}
