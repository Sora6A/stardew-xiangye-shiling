namespace GameHarness;

/// <summary>
/// P1输入故障定位的共享安全边界；由真实SDK夹具调用，CLI只测试这些纯判据。
/// 不推断“键盘已恢复”，不包含玩家或游戏引用，也不产生游戏写入。
/// </summary>
internal static class InputObservationPolicy
{
    public const long DurationMs = 180_000;
    public const long SampleIntervalMs = 200;
    public const int MaxSamples = 900;
    public const int MaxEvents = 120;

    // XYSNET专用命名隔离旧源/UI/Stage3档；仍须同时核配置的非零ID及实际保存目录。
    public static bool ScopeMatches(bool worldReady, bool mainPlayer, bool splitScreen, int screenId,
        bool openPreview, string? configuredFarm, ulong configuredId, string? actualFarm,
        ulong actualId, string? saveFolder) => worldReady && mainPlayer && !splitScreen && screenId == 0
        && !openPreview && configuredFarm is { Length: >= 6 and <= 20 }
        && configuredFarm.StartsWith("XYSNET", StringComparison.Ordinal)
        && configuredId != 0 && configuredId == actualId
        && string.Equals(configuredFarm, actualFarm, StringComparison.Ordinal)
        && string.Equals(saveFolder, $"{configuredFarm}_{configuredId}", StringComparison.Ordinal);

    // 只记录故障所需的六键；聊天或任意文本订阅者存在时，连允许键也不记录。
    public static bool CanRecordKeys(bool chatting, bool hasTextSubscriber) => !chatting && !hasTextSubscriber;
    public static bool IsAllowedKey(string key) => key is "W" or "A" or "S" or "D" or "E" or "Escape";

    public static bool CanSample(long elapsedMs, long previousMs, int count) => elapsedMs >= 0
        && elapsedMs < DurationMs && count < MaxSamples
        && (previousMs < 0 || (elapsedMs >= previousMs && elapsedMs - previousMs >= SampleIntervalMs));
    public static bool CanRecordEvent(long elapsedMs, int count) => elapsedMs >= 0
        && elapsedMs < DurationMs && count < MaxEvents;
}
