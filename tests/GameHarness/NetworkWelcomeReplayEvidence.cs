using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>
/// 独立schema1重放报告，Lifecycle冻结在任何重放前；不得改被动报告的ProbeSent/未测能力声明。
/// 只有严格配对工具计算OldWelcomeReplayRejected，单端Completed不是完整NET02或网络送达证明。
/// </summary>
public sealed record NetworkWelcomeReplayReport(int SchemaVersion, NetworkLifecycleReport Lifecycle,
    string Status, string? Failure, bool Saved, bool ReplayAttempted, bool FullNet02Accepted,
    bool LoadedQueueLifecycleVerified, LifecycleCheckpoint? BeforeReplay, LifecycleCheckpoint? AfterReplay);

/// <summary>
/// 同一新握手成立后，主机将真实旧Welcome发到新连接、客户端读入旧challenge但不接受的双端证据。
/// 本地序号/时钟/连接GUID只在各端比较；跨端只比Farmer/世界/session/实际挑战及同版DLL。
/// 不涵盖未Ready前SDK重放、其他旧报文、第三位peer、带负载队列或任何库存写入授权。
/// </summary>
public static class NetworkWelcomeReplayEvidence
{
    public static bool PairMatches(NetworkWelcomeReplayReport host, NetworkWelcomeReplayReport client) =>
        host is not null && client is not null && host.Lifecycle is not null && client.Lifecycle is not null
        && NetworkLifecycleEvidence.PairMatches(host.Lifecycle, client.Lifecycle)
        && Endpoint(host, true) && Endpoint(client, false);

    /// <summary>单端阶段判据也供SDK夹具使用，不能代替PairMatches的真实双方关联。</summary>
    public static bool Endpoint(NetworkWelcomeReplayReport report, bool host)
    {
        if (report is null || report.SchemaVersion != 1 || report.Lifecycle is not { } life
            || life.NetworkRole != (host ? "Host" : "Client") || life.Status != "Completed" || life.Failure is not null
            || report.Status != "Completed" || report.Failure is not null || report.Saved
            || report.FullNet02Accepted || report.LoadedQueueLifecycleVerified || report.ReplayAttempted != host
            || life.InitialReady is not { } initial || life.DisconnectOrTitle is not { } gap || life.Reconnected is not { } current
            || report.BeforeReplay is not { } before || report.AfterReplay is not { } after
            || !NetworkLifecycleEvidence.Ready(before.Protocol, host)
            || !Stable(current, before, false) || !Stable(before, after, true)) return false;
        var oldHandshake = NetworkLifecycleEvidence.Handshake(initial.Protocol, 0);
        var newHandshake = NetworkLifecycleEvidence.Handshake(current.Protocol, gap.Protocol.LastSequence);
        if (oldHandshake?.Observation?.Challenge is not Guid oldChallenge || oldChallenge == Guid.Empty
            || newHandshake?.Observation?.Challenge is not Guid newChallenge || oldChallenge == newChallenge) return false;
        var delta = after.Protocol.Trace.Skip(before.Protocol.Trace.Count).ToArray();
        var peer = before.Protocol.Peers[0];
        bool Current(ProtocolTrace trace) => trace.PeerId == peer.PlayerId && trace.ConnectionId == peer.ConnectionId
            && trace.WorldGeneration == before.Protocol.WorldGeneration && trace.DisconnectGeneration == before.Protocol.DisconnectGeneration
            && trace.MessageType == ReadOnlyProtocolCodec.WelcomeType;
        if (host) return delta.Length == 2 && delta.All(Current)
            && delta[0].Kind == ProtocolTraceKind.WelcomeReplayAttempt && delta[0].Quota is null
            && delta[0].Observation is { Kind: ProtocolReceiveKind.Ignored, BodyReadAttempted: false, RequestId: null, Reason: null } planned
            && planned.Challenge == oldChallenge && delta[1].Kind == ProtocolTraceKind.SendAttempt
            && delta[1].Observation is null && delta[1].Quota is null;
        return delta.Length == 1 && Current(delta[0]) && delta[0].Kind == ProtocolTraceKind.Processed
            && delta[0].Observation is { Kind: ProtocolReceiveKind.WelcomeRejected, BodyReadAttempted: true, RequestId: null, Reason: null } rejected
            && rejected.Challenge == oldChallenge && delta[0].Quota is { Allowed: true, Count: > 0 and <= ReadOnlyProtocolSession.MessagesPerWindow };
    }

    /// <summary>同一次load的引用和值/raw不变；新的游戏连接、Reset、队列丢弃或trace缺失均不能跨阶段拼接。</summary>
    private static bool Stable(LifecycleCheckpoint before, LifecycleCheckpoint after, bool allowReplay) =>
        before.Protocol is not null && after.Protocol is not null
        && NetworkLifecycleEvidence.Healthy(after.Protocol, allowReplay) && after.Protocol.Active && after.Protocol.ProbeAllowed
        && before.FixtureId == after.FixtureId && before.FarmName == after.FarmName && before.LoadCount == after.LoadCount
        && before.InventorySha256 == after.InventorySha256 && before.VisibleMachineRawSha256 == after.VisibleMachineRawSha256
        && before.VisibleMachineCount == after.VisibleMachineCount && before.InventoryReferencesUnchanged is true && after.InventoryReferencesUnchanged is true
        && before.Protocol.RunId == after.Protocol.RunId && before.Protocol.SessionId == after.Protocol.SessionId
        && before.Protocol.LocalPlayerId == after.Protocol.LocalPlayerId && before.Protocol.IsHost == after.Protocol.IsHost
        && before.Protocol.WorldGeneration == after.Protocol.WorldGeneration && before.Protocol.DisconnectGeneration == after.Protocol.DisconnectGeneration
        && before.Protocol.Peers.SequenceEqual(after.Protocol.Peers) && before.Protocol.LastSequence <= after.Protocol.LastSequence
        && before.Protocol.Trace.SequenceEqual(after.Protocol.Trace.Take(before.Protocol.Trace.Count));
}
