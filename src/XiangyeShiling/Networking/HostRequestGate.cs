using XiangyeShiling.Machines;

namespace XiangyeShiling.Networking;

/// <summary>
/// 由未来游戏主线程适配器从可信上下文构造，不能反序列化客户端 JSON 作为这个快照。
/// AuthenticatedActorId 必须来自已建立信任的身份来源；仅复制 SMAPI FromPlayerID 不满足此前置条件。
/// InventoryOwnerId 来自主机解析出的实际 Farmer；P1-NET-02 只做元数据路由，不构造这个可执行快照。
/// Actor 连接状态、位置与忙碌标志也来自主机。此 DTO 本身不提供网络认证或背包锁。
/// </summary>
public sealed record HostActorSnapshot(Guid SessionId, bool IsOnGameThread, bool IsMainPlayer,
    bool IsWorldReady, bool ActorConnected, long AuthenticatedActorId, long InventoryOwnerId,
    string LocationName, int TileX, int TileY, bool ActorBusy);

/// <summary>主机按请求地址查出的实际地图对象状态；损坏原 JSON 应由适配器保留，并传入 null 状态拒绝。</summary>
public sealed record HostTargetSnapshot(string LocationName, int TileX, int TileY, MachineState? State);

public enum HostRequestRejection
{
    None, InvalidRequest, InvalidContext, NotGameThread, NotHost, WorldUnavailable, WrongSession,
    ActorOffline, ActorMismatch, InventoryOwnerMismatch, ActorBusy, TargetMissing,
    TargetMismatch, WrongLocation, OutOfRange, InvalidTargetState
}

/// <summary>Authorized 只代表准入核验通过；规则计划、账本、完整事务与网络确认都尚未执行。</summary>
public sealed record HostAuthorization(HostRequestRejection Rejection, Request? RuleRequest)
{
    public bool Authorized => this.Rejection == HostRequestRejection.None && this.RuleRequest is not null;
}

/// <summary>
/// P1 无游戏依赖的主机准入边界。所有拒绝都无计划/无写入；不能以这个纯函数代替实际 Farmer
/// 所有权解析、地图对象引用核对或提交前再次核对。生产联机入口目前仍关闭。
/// </summary>
public static class HostRequestGate
{
    public static HostAuthorization Authorize(MachineRequestEnvelope? intent,
        HostActorSnapshot? actor, HostTargetSnapshot? target)
    {
        static HostAuthorization Reject(HostRequestRejection reason) => new(reason, null);
        if (!MachineRequestCodec.IsValid(intent)) return Reject(HostRequestRejection.InvalidRequest);
        if (actor is null || actor.SessionId == Guid.Empty || actor.AuthenticatedActorId == 0
            || !MachineRequestCodec.ValidName(actor.LocationName) || actor.TileX < 0 || actor.TileY < 0)
            return Reject(HostRequestRejection.InvalidContext);
        if (!actor.IsOnGameThread) return Reject(HostRequestRejection.NotGameThread);
        if (!actor.IsMainPlayer) return Reject(HostRequestRejection.NotHost);
        if (!actor.IsWorldReady) return Reject(HostRequestRejection.WorldUnavailable);
        if (intent!.SessionId != actor.SessionId) return Reject(HostRequestRejection.WrongSession);
        if (!actor.ActorConnected) return Reject(HostRequestRejection.ActorOffline);
        if (intent.ActorId != actor.AuthenticatedActorId) return Reject(HostRequestRejection.ActorMismatch);
        if (actor.InventoryOwnerId != actor.AuthenticatedActorId)
            return Reject(HostRequestRejection.InventoryOwnerMismatch);
        if (actor.ActorBusy) return Reject(HostRequestRejection.ActorBusy);
        if (target is null) return Reject(HostRequestRejection.TargetMissing);
        if (target.LocationName != intent.LocationName || target.TileX != intent.TileX || target.TileY != intent.TileY)
            return Reject(HostRequestRejection.TargetMismatch);
        if (actor.LocationName != target.LocationName) return Reject(HostRequestRejection.WrongLocation);
        // Use long differences: untrusted extreme tile coordinates must not overflow into a near distance.
        if (Math.Abs((long)actor.TileX - target.TileX) > 1 || Math.Abs((long)actor.TileY - target.TileY) > 1)
            return Reject(HostRequestRejection.OutOfRange);
        if (!MachineStateCodec.IsValid(target.State)) return Reject(HostRequestRejection.InvalidTargetState);
        // Instance/revision/replay, recipe resolution, inventory and host time remain MachineRules responsibilities.
        // The authenticated ID is copied instead of turning the claimed ActorId into an authoritative identity.
        return new(HostRequestRejection.None, new(intent.RequestId, intent.InstanceId,
            intent.ExpectedRevision, actor.AuthenticatedActorId));
    }
}
