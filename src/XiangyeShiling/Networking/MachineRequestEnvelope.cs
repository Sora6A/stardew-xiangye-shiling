namespace XiangyeShiling.Networking;

public enum MachineRequestAction { Start, Collect }

/// <summary>
/// P1 协议草案中的客户端操作意图，只包含身份和定位；不能提供库存、配方数值、产物或时钟。
/// ActorId 是客户端声称的身份。只读桥仅核对路由元数据；未来写入须另取得可信身份，不能据此选任意背包。
/// </summary>
/// <remarks>
/// SessionId 由主机为本次世界会话生成，不持久化到设备状态；断线重连不清请求账本。
/// 重试必须保留全部字段。协议版本与 MachineState.SchemaVersion 独立，NET-02 只读接收仍拒绝所有写入。
/// </remarks>
public sealed record MachineRequestEnvelope(int ProtocolVersion, Guid SessionId, Guid RequestId,
    long ActorId, string LocationName, int TileX, int TileY, Guid InstanceId,
    long ExpectedRevision, MachineRequestAction Action, string? RecipeId)
{
    public const int CurrentProtocol = 1;
}
