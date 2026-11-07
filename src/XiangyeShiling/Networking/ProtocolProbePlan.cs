namespace XiangyeShiling.Networking;

public enum ProtocolProbeKind { Start, Collect, ActorMismatch, WrongSession, Malformed }

/// <summary>
/// 为默认禁用的 NET-02 诊断夹具生成固定探针；只返回文本，不读取游戏、发送消息或创建事务。
/// 真实发包须由受控只读适配器使用本模组的 SMAPI Helper 完成，不能把纯会话配对称为网络实测。
/// </summary>
public static class ProtocolProbePlan
{
    /// <summary>
    /// 目标仅为格式合法的合成地址，不证明 FarmHouse 的 (0,0) 有设备或操作者有有效范围/库存。
    /// NET-02 在任何机器准入或写入前恒拒绝；本探针不为后续可写协议提供合法交易输入。
    /// </summary>
    public const bool TargetIsSynthetic = true;

    /// <summary>
    /// 身份和请求 ID 来自诊断运行上下文；原始载荷、库存、时间和产物不能由调用者传入。
    /// 负例仍校验这些参数，避免在无效上下文中制造似乎有效的诊断。Malformed 仅返回固定负例。
    /// </summary>
    public static string Create(ProtocolProbeKind kind, Guid sessionId, Guid requestId, long actorId)
    {
        if (!Enum.IsDefined(typeof(ProtocolProbeKind), kind)) throw new ArgumentException("Unknown protocol probe kind.", nameof(kind));
        if (sessionId == Guid.Empty) throw new ArgumentException("A probe must identify its host session.", nameof(sessionId));
        if (requestId == Guid.Empty) throw new ArgumentException("A probe must identify its request.", nameof(requestId));
        if (actorId == 0) throw new ArgumentException("A probe must identify its actual actor.", nameof(actorId));
        if (kind == ProtocolProbeKind.Malformed) return "{invalid";

        Guid targetSession = sessionId;
        if (kind == ProtocolProbeKind.WrongSession)
        {
            do { targetSession = Guid.NewGuid(); }
            while (targetSession == Guid.Empty || targetSession == sessionId);
        }
        // A fixed alternative avoids overflow at long.MaxValue and the zero produced by incrementing -1.
        long claimedActor = kind == ProtocolProbeKind.ActorMismatch ? (actorId == 1 ? 2 : 1) : actorId;
        MachineRequestAction action = kind == ProtocolProbeKind.Collect ? MachineRequestAction.Collect : MachineRequestAction.Start;
        return MachineRequestCodec.Write(new MachineRequestEnvelope(MachineRequestEnvelope.CurrentProtocol,
            targetSession, requestId, claimedActor, "FarmHouse", 0, 0, requestId, 0, action,
            action == MachineRequestAction.Start ? "tofu" : null));
    }

    /// <summary>
    /// 预期匹配 peer 已同步、当前握手完成且未超额；不满足前提时可以无响应或返回 HandshakeRequired。
    /// Malformed 回执的 RequestId 为 null，不能用传入 requestId 关联它；原因映射不证明收到网络回包。
    /// </summary>
    public static ProtocolRejection ExpectedReason(ProtocolProbeKind kind) => kind switch
    {
        ProtocolProbeKind.Start or ProtocolProbeKind.Collect => ProtocolRejection.WritesDisabled,
        ProtocolProbeKind.ActorMismatch => ProtocolRejection.ActorMismatch,
        ProtocolProbeKind.WrongSession => ProtocolRejection.WrongSession,
        ProtocolProbeKind.Malformed => ProtocolRejection.InvalidRequest,
        _ => throw new ArgumentException("Unknown protocol probe kind.", nameof(kind))
    };
}
