using XiangyeShiling.Machines;

namespace XiangyeShiling.Menus;

/// <summary>
/// 一次菜单打开期间的操作意图。失败重试复用绑定原设备/修订的请求，不能悄悄接受更新状态。
/// 不执行库存或机器写入；调用者只在 Execute 返回 Applied 后确认本次操作完成。
/// </summary>
public sealed class MenuActionSession
{
    private Request? Pending;
    public bool IsComplete { get; private set; }
    public Request? Acquire(MachineState state, long actorId) => this.IsComplete ? null :
        this.Pending ??= new(Guid.NewGuid(), state.InstanceId, state.Revision, actorId);
    public void Complete(bool applied)
    {
        // BUG-2026-007：领取后的同位置连点不能被重新解释为开工；新意图须重新打开菜单。
        // 不用毫秒防抖，保护效果不依赖帧率，也不会给失败的缺料/满包重试增加等待。
        if (applied) this.IsComplete = true;
    }
}
