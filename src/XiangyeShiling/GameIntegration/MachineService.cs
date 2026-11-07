using System.Diagnostics.CodeAnalysis;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.Machines;
using XiangyeShiling.Transactions;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

public sealed record OperationResult(bool Applied, string Message);

/// <summary>
/// P1 单人主机适配器。输入来自当前主线程的真实对象；尚未验证远端背包协议，联机一律拒绝。
/// 单笔提交前准备所有克隆与 JSON，最后核对原快照；失败恢复两侧，恢复失败隔离整个会话。
/// </summary>
public sealed class MachineService
{
    public const string Prefix = "Sora6A.XiangyeShiling";
    public const string StateKey = Prefix + "/MachineState";
    private readonly IMonitor Monitor;
    private readonly ITranslationHelper Text;
    private CommitCoordinator Coordinator = new();
    private readonly int MainThread = Environment.CurrentManagedThreadId;
    public IReadOnlyList<MachineRecipe> Recipes { get; }
    private ReadOnlyProtocolService? Network;

    // Preserve GetApi's existing MachineService identity for all four old harness modes.
    internal void BindNetworkDiagnostics(ReadOnlyProtocolService service) => this.Network = service;
    /// <summary>仅本地主线程诊断快照，不提供库存授权；未绑定时抛错，不伪造“零错误”报告。</summary>
    public ProtocolDiagnosticSnapshot ReadNetworkDiagnostics() => this.Network?.ReadDiagnostics()
        ?? throw new InvalidOperationException("Read-only protocol diagnostics are unavailable.");
    /// <summary>固定合成只读probe，另需主模块显式配置及专用世界；不委托Execute，true不表示对端收到。</summary>
    public bool TrySendNetworkProbe(ProtocolProbeKind kind, Guid requestId) => this.Network?.TrySendProbe(kind, requestId) ?? false;
    /// <summary>默认禁用、主线程、专用世界的固定旧Welcome重放；不能传入载荷或目标，true不表示对端拒绝。</summary>
    public bool TryReplayPreviousNetworkWelcome(Guid expectedConnectionId) => this.Network?.TryReplayPreviousWelcome(expectedConnectionId) ?? false;

    public MachineService(IMonitor monitor, ITranslationHelper text, IReadOnlyList<MachineRecipe> recipes)
    {
        this.Monitor = monitor;
        this.Text = text;
        this.Recipes = recipes;
    }

    public void ResetSession() => this.Coordinator = new();
    /// <summary>
    /// 只识别本模块两种设备；原生 NPC 清路会将空格查询得到的 null 传到移除补丁。
    /// 空对象/其他物品返回 false 且不写状态，成功时才保证 item 非空；失败的 kind 不可使用。
    /// BUG-2026-019：此边界必须先拒绝空对象，避免中断整个游戏更新循环。
    /// </summary>
    public static bool TryGetKind([NotNullWhen(true)] SObject? item, out MachineKind kind)
    {
        kind = MachineKind.BeanWorkshop;
        if (item is null) return false;
        if (item.QualifiedItemId == "(BC)" + Prefix + "_BeanWorkshop") return true;
        kind = MachineKind.FermentationCrock;
        return item.QualifiedItemId == "(BC)" + Prefix + "_FermentationCrock";
    }

    /// <summary>空目标/其他物品/损坏或未知状态拒绝读取并保留原值；返回 false 时 state 不可使用。</summary>
    public static bool TryRead([NotNullWhen(true)] SObject? machine, out MachineState? state)
    {
        state = null;
        return TryGetKind(machine, out var kind) && machine.modData.TryGetValue(StateKey, out string raw)
            && MachineStateCodec.TryRead(raw, out state) && state!.Kind == kind;
    }

    /// <summary>
    /// 空格和非设备放行原生移除判断；已识别设备缺失/损坏状态或不可移动时继续保护。
    /// 此判断只读，不初始化设备、不修复 JSON，也不授予联机写入权限。
    /// </summary>
    public static bool BlocksRemoval(SObject? machine) => TryGetKind(machine, out _)
        && (!TryRead(machine, out var state) || !state!.CanMove);

    /// <summary>仅新放置或缺少状态的设备初始化；已有但损坏的数据绝不覆盖。</summary>
    public static void Initialize(SObject machine, bool newlyPlaced = false)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || Context.IsMultiplayer || !TryGetKind(machine, out var kind)) return;
        if (!newlyPlaced && machine.modData.ContainsKey(StateKey)) return;
        machine.modData[StateKey] = MachineStateCodec.Write(MachineState.Empty(Guid.NewGuid(), kind));
    }

    public static long Now() => checked((long)Game1.Date.TotalDays * 1440 + Game1.timeOfDay / 100 * 60 + Game1.timeOfDay % 100);

    public OperationResult Execute(SObject machine, Farmer actor, Request request, string? recipeId)
    {
        OperationResult Fail(string key) => new(false, this.Text.Get(key));
        if (Environment.CurrentManagedThreadId != this.MainThread)
            return new(false, "Machine operations must run on the game thread.");
        if (!Context.IsWorldReady || !Context.IsMainPlayer || Context.IsMultiplayer || actor != Game1.player)
            return Fail("machine.single-player-only");
        if (Game1.eventUp || actor.UsingTool || request.ActorId != actor.UniqueMultiplayerID
            || !actor.currentLocation.objects.TryGetValue(machine.TileLocation, out SObject current) || !ReferenceEquals(current, machine)
            || Math.Abs(actor.Tile.X - machine.TileLocation.X) > 1 || Math.Abs(actor.Tile.Y - machine.TileLocation.Y) > 1)
            return Fail("machine.out-of-range");
        if (!TryRead(machine, out MachineState? state)) return Fail("machine.invalid-state");

        string beforeJson = machine.modData[StateKey];
        MachineRecipe? recipe = recipeId is null ? null : this.Recipes.SingleOrDefault(r => r.Id == recipeId);
        if (recipeId is not null && recipe is null) return Fail("machine.invalid-recipe");
        Product? output = recipe?.Output ?? state!.Batch?.Output;
        try
        {
            Item? outputItem = output is null ? null : ItemRegistry.Create(output.ItemId, 1, output.Quality);
            if (output is not null && (ItemRegistry.GetData(output.ItemId) is null || outputItem!.maximumStackSize() != output.MaxStack))
                return Fail("machine.invalid-recipe");
            var snapshot = new InventorySnapshot(actor, outputItem);
            Decision decision = recipe is null
                ? MachineRules.Collect(state!, request, snapshot.Slots, Now())
                : MachineRules.Start(state!, request, recipe, true, snapshot.Slots, Now());
            if (!decision.Accepted) return Fail("rejection." + decision.Rejection);
            MachineTransaction transaction = decision.Transaction!;
            CommitResult result = this.Coordinator.Commit(
                () => new GameCommit(machine, beforeJson, snapshot, transaction, outputItem),
                error => this.Monitor.Log($"Machine transaction exception: {error}", LogLevel.Error));
            if (result != CommitResult.Applied) return Fail("commit." + result);
            return new(true, this.Text.Get(recipe is null ? "machine.collected" : "machine.started"));
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Machine preparation failed before commit: {ex}", LogLevel.Error);
            return Fail("commit.PreparationFailed");
        }
    }

    /// <summary>保留原物品引用；只克隆被改数量的对象，未参与事务的工具/附魔/元数据不重建。</summary>
    private sealed class InventorySnapshot
    {
        public Farmer Actor { get; }
        public Item[] Items { get; }
        public InventorySlot?[] Slots { get; }
        private readonly string[] Metadata;
        private readonly Item? Output;
        private readonly int Capacity;
        public InventorySnapshot(Farmer actor, Item? output, Item[]? expectedItems = null)
        {
            this.Actor = actor;
            this.Output = output;
            this.Capacity = actor.MaxItems;
            this.Items = expectedItems ?? actor.Items.ToArray();
            this.Slots = this.Items.Select(i => Slot(i, output)).ToArray();
            this.Metadata = this.Items.Select(Meta).ToArray();
            // Never silently extend/cut the actual inventory: that could hide modded capacity changes.
            if (this.Items.Length != this.Capacity) throw new InvalidOperationException("Inventory capacity doesn't match its slot count.");
        }
        public bool Matches() => this.Actor.MaxItems == this.Capacity && this.Actor.Items.Count == this.Items.Length
            && Enumerable.Range(0, this.Items.Length).All(i => ReferenceEquals(this.Actor.Items[i], this.Items[i])
                && Slot(this.Actor.Items[i], this.Output) == this.Slots[i] && Meta(this.Actor.Items[i]) == this.Metadata[i]);
        private static InventorySlot? Slot(Item? item, Item? output) => item is null ? null
            : new(item.QualifiedItemId, item.Stack, item.Quality, item.maximumStackSize(), output is not null && item.canStackWith(output));
        private static string Meta(Item? item) => item is null ? "" : System.Text.Json.JsonSerializer.Serialize(
            item.modData.Pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray());
    }

    private sealed class GameCommit : IPreparedCommit
    {
        private readonly SObject Machine;
        private readonly string BeforeJson;
        private readonly string AfterJson;
        private readonly InventorySnapshot Before;
        private readonly Item[] After;
        private readonly InventorySnapshot Expected;
        public GameCommit(SObject machine, string beforeJson, InventorySnapshot before, MachineTransaction transaction, Item? output)
        {
            this.Machine = machine;
            this.BeforeJson = beforeJson;
            this.AfterJson = MachineStateCodec.Write(transaction.After);
            this.Before = before;
            this.After = before.Items.ToArray();
            foreach (SlotChange change in transaction.Slots)
            {
                if (change.NewQuantity == 0) this.After[change.Slot] = null!;
                else
                {
                    Item copy = (before.Items[change.Slot] ?? output ?? throw new InvalidOperationException("No output prototype.")).getOne();
                    copy.Stack = change.NewQuantity;
                    this.After[change.Slot] = copy;
                }
            }
            this.Expected = new InventorySnapshot(before.Actor, output, this.After);
        }
        public bool MatchesOriginal() => this.Before.Matches()
            && this.Before.Actor.currentLocation.objects.TryGetValue(this.Machine.TileLocation, out SObject placed)
            && ReferenceEquals(placed, this.Machine)
            && this.Machine.modData.TryGetValue(StateKey, out string raw) && raw == this.BeforeJson;
        // OverwriteWith 内部逐槽清空/填充，并非原子替换；同步监听器可抛错，协调器必须补偿。
        public void ApplyInventory() => this.Before.Actor.Items.OverwriteWith(this.After);
        public void ApplyState() => this.Machine.modData[StateKey] = this.AfterJson;
        public bool IsApplied() => this.Expected.Matches()
            && this.Before.Actor.currentLocation.objects.TryGetValue(this.Machine.TileLocation, out SObject placed)
            && ReferenceEquals(placed, this.Machine)
            && this.Machine.modData.TryGetValue(StateKey, out string raw) && raw == this.AfterJson;
        public void RestoreInventory() => this.Before.Actor.Items.OverwriteWith(this.Before.Items);
        public void RestoreState() => this.Machine.modData[StateKey] = this.BeforeJson;
        public bool IsRestored() => this.Before.Matches() && this.Machine.modData[StateKey] == this.BeforeJson;
    }
}
