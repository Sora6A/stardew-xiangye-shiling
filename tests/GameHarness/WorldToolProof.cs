namespace GameHarness;

/// <summary>
/// 世界输入夹具的无游戏依赖验收判据。鼠标目标、原生实际命中数和体力来自同一阶段的只读观察；
/// 调用者必须在目标格、玩家身份及镐引用核对后才计入 completedTargetHits，不能把发送输入当作完成动作。
/// BUG-2026-011：设备没变本身不足以证明曾被击中，背景与负例见对应 bugfix。
/// </summary>
internal static class WorldToolProof
{
    internal static bool IsSatisfied(int targetMouseClicks, int completedTargetHits,
        float beforeStamina, float afterStamina, bool correctPickaxe)
        => targetMouseClicks > 0 && completedTargetHits > 0 && afterStamina < beforeStamina && correctPickaxe;
}
