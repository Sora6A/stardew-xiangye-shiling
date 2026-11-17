using XiangyeShiling.Machines;

namespace XiangyeShiling.Menus;

/// <summary>
/// 一次菜单打开期间的操作意图。失败重试复用绑定原设备/修订的请求，不能悄悄接受更新状态。
/// 不执行库存或机器写入；调用者只在 Execute 返回 Applied 后确认本次操作完成。
/// </summary>
public sealed class MenuActionSession
{
    private Request? Pending;
    private string? PendingRecipe;
    public bool IsComplete { get; private set; }
    public Request? Acquire(MachineState state, long actorId) => this.Acquire(state, actorId, null);

    internal Request? Acquire(MachineState state, long actorId, string? recipeId)
    {
        if (this.IsComplete) return null;
        if (this.Pending is { } previous)
        {
            if (this.PendingRecipe == recipeId) return previous;
            // 只有显式点击同一修订上的另一配方才创建新意图，避免将旧请求 ID 改发为不同操作。
            if (previous.InstanceId != state.InstanceId || previous.ExpectedRevision != state.Revision
                || previous.ActorId != actorId)
                return null;
        }
        this.PendingRecipe = recipeId;
        return this.Pending = new(Guid.NewGuid(), state.InstanceId, state.Revision, actorId);
    }

    internal void ReleaseRefusedRequest() => this.Pending = null;

    // 一次已提交的开工只锁住菜单操作，批次仍按自己的时钟加工。
    internal string DisplayActionKey(MachinePhase phase, bool awaitingHost, bool missing) => awaitingHost ? "machine.network-pending"
        : phase == MachinePhase.Processing ? "phase.Processing" : this.IsComplete ? "machine.done"
        : phase == MachinePhase.Ready ? "machine.collect" : missing ? "machine.materials-missing" : "machine.start";

    public void Complete(bool applied)
    {
        // BUG-2026-007：领取后的同位置连点不能被重新解释为开工；新意图须重新打开菜单。
        // 不用毫秒防抖，保护效果不依赖帧率，也不会给失败的缺料/满包重试增加等待。
        if (applied) this.IsComplete = true;
    }
}
