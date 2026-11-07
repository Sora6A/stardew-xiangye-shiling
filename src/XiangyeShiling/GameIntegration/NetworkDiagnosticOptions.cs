namespace XiangyeShiling.GameIntegration;

/// <summary>
/// 本地NET-02诊断配置，默认不允许发测试意图。必须显式指定专用XYSNET农场名及独立非零世界ID，
/// 与GameHarness同一配置身份核对；不打开机器事务，也不自动加载/创建/保存农场。
/// </summary>
public sealed class NetworkDiagnosticOptions
{
    public bool EnableReadOnlyNetworkProbes { get; set; }
    public string NetworkProbeFarmName { get; set; } = "XYSNET02";
    public ulong NetworkProbeSaveFixtureId { get; set; }
}
