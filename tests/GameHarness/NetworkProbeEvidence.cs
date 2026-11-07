using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>本地证据绑定，不驱动网络。上下文来自发probe之前的真实诊断快照。</summary>
public sealed record ProbeEvidenceContext(Guid RunId, Guid SessionId, long WorldGeneration,
    long DisconnectGeneration, long HostPlayerId, Guid HostConnectionId, long QueueRejected,
    long Evicted, int OwnerThreadId);

/// <summary>共用纯判据，避免迟到/换连接的拒绝被拼成当前probe成功。</summary>
public static class NetworkProbeEvidence
{
    public static bool ContextMatches(ProtocolDiagnosticSnapshot snapshot, ProbeEvidenceContext context) =>
        snapshot.Active && !snapshot.IsHost && !snapshot.Faulted && !snapshot.WritesEnabled
        && snapshot.ProbeAllowed && snapshot.RunId == context.RunId && snapshot.SessionId == context.SessionId
        && snapshot.WorldGeneration == context.WorldGeneration && snapshot.DisconnectGeneration == context.DisconnectGeneration
        && snapshot.QueueRejected == context.QueueRejected && snapshot.Evicted == context.Evicted
        && snapshot.Peers.Count(p => p.IsHost && p.Ready) == 1
        && snapshot.Peers.Any(p => p.IsHost && p.Ready && p.PlayerId == context.HostPlayerId && p.ConnectionId == context.HostConnectionId);

    public static bool HasRejection(ProtocolDiagnosticSnapshot snapshot, ProbeEvidenceContext context,
        long sentAfterSequence, Guid? requestId, ProtocolRejection reason) => ContextMatches(snapshot, context) && snapshot.Trace.Any(t =>
        t.Sequence > sentAfterSequence && t.Kind == ProtocolTraceKind.Processed
        && t.WorldGeneration == context.WorldGeneration && t.DisconnectGeneration == context.DisconnectGeneration
        && t.PeerId == context.HostPlayerId && t.MessageType == ReadOnlyProtocolCodec.RejectedType && t.ThreadId == context.OwnerThreadId
        && t.Observation is { Kind: ProtocolReceiveKind.RejectionAccepted, BodyReadAttempted: true } obs
        && obs.RequestId == requestId && obs.Reason == reason);
}
