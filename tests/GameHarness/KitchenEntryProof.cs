namespace GameHarness;

/// <summary>BUG-2026-023：夹具的原生Action来源判据；不依赖游戏程序集，也不授予操作权限。</summary>
internal sealed record KitchenActionSample(int FixtureX, int FixtureY, int GrabX, int GrabY,
    int ActualX, int ActualY, string? Action, bool Succeeded, bool LocalActor, bool CurrentLocation);

internal static class KitchenEntryProof
{
    internal static bool Matches(KitchenActionSample sample) => sample.Succeeded && sample.LocalActor && sample.CurrentLocation
        && sample.Action is "kitchen" or "Kitchen" && sample.ActualX == sample.FixtureX && sample.ActualY == sample.FixtureY;
}
