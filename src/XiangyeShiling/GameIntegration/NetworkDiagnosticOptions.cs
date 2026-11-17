namespace XiangyeShiling.GameIntegration;

/// <summary>
/// 局域网设备操作和专用诊断各自默认关闭。专用探针还绑定独立世界；本配置不加载、创建或保存农场。
/// </summary>
public sealed class NetworkDiagnosticOptions
{
    public bool EnableReadOnlyNetworkProbes { get; set; }
    public string NetworkProbeFarmName { get; set; } = "XYSNET02";
    public ulong NetworkProbeSaveFixtureId { get; set; }
    public bool EnableLanTransactionFixture { get; set; }
    public bool EnableLanMachineOperations { get; set; }
}
