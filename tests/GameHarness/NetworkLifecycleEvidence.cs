using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>来自一次真实API读取的检查点。标题不读取玩家/物品，相关字段为null/0。</summary>
public sealed record LifecycleCheckpoint(ProtocolDiagnosticSnapshot Protocol, int LoadCount,
    ulong FixtureId, string? FarmName, string? InventorySha256, string? VisibleMachineRawSha256,
    int? VisibleMachineCount, bool? InventoryReferencesUnchanged);

/// <summary>
/// 一个进程的唯一只读重连观察。三检查点在同一对象内保留，不由新SaveLoaded清空；
/// Host只加载一次，Client通过标题后加载第二次。Saved/ProbeSent/Full及未测能力必须false。
/// </summary>
public sealed record NetworkLifecycleReport(int SchemaVersion, Guid HarnessRunId, int ProcessId,
    string NetworkRole, ulong ExpectedFixtureId, string ConfiguredFarmName, string ProductionDllSha256,
    string HarnessDllSha256, string Status, string? Failure, bool Saved, bool ProbeSent,
    bool FullNet02Accepted, bool OldPacketReplayVerified, bool LoadedQueueLifecycleVerified,
    LifecycleCheckpoint? InitialReady, LifecycleCheckpoint? DisconnectOrTitle,
    LifecycleCheckpoint? Reconnected, LifecycleCheckpoint? ClosingTitle);

/// <summary>
/// SDK无关的实际观察判据，同时用于夹具和离线配对。仅证明同ID新连接重新握手及主机会话保持；
/// 不发送旧报文，不证明带负载队列、恶意身份认证或可写权。各端时钟/线程/连接GUID不跨端比较。
/// </summary>
public static class NetworkLifecycleEvidence
{
    public const long DurationMilliseconds = 300000;

    public static bool ScopeMatches(bool ready, bool mainPlayer, bool splitScreen, int screenId,
        bool preview, string role, string? configuredFarm, ulong configuredId, string? farm, ulong actualId) =>
        ready && !splitScreen && screenId == 0 && !preview
        && (role == "Host" ? mainPlayer : role == "Client" && !mainPlayer)
        && configuredFarm is { Length: >= 6 and <= 20 } && configuredFarm.StartsWith("XYSNET", StringComparison.Ordinal)
        && configuredId != 0 && configuredId == actualId && configuredFarm == farm;

    public static bool Healthy(ProtocolDiagnosticSnapshot snapshot, bool allowWelcomeReplay = false) => snapshot is not null && snapshot.RunId != Guid.Empty
        && !snapshot.Faulted && !snapshot.WritesEnabled && snapshot.ProbeConfigured
        && snapshot.QueueCount == 0 && snapshot.QueueRejected == 0 && snapshot.Evicted == 0
        && snapshot.WorldGeneration > 0 && snapshot.DisconnectGeneration >= 0
        && snapshot.Peers is not null && snapshot.Trace is not null
        && snapshot.Trace.Count is > 0 and <= 256 && snapshot.LastSequence == snapshot.Trace.Count
        && snapshot.Trace.Select((t, i) => t is not null && Enum.IsDefined(typeof(ProtocolTraceKind), t.Kind) && t.Sequence == i + 1 && t.Milliseconds >= 0
            && (i == 0 || t.Milliseconds >= snapshot.Trace[i - 1].Milliseconds)
            && t.ThreadId > 0 && t.ThreadId == snapshot.Trace[0].ThreadId
            && t.Kind != ProtocolTraceKind.ProbeAttempt
            && (allowWelcomeReplay || t.Kind != ProtocolTraceKind.WelcomeReplayAttempt)).All(value => value);

    public static bool Ready(ProtocolDiagnosticSnapshot snapshot, bool host) => Healthy(snapshot)
        && snapshot.Active && snapshot.ProbeAllowed && snapshot.IsHost == host && snapshot.LocalPlayerId != 0
        && snapshot.SessionId is Guid session && session != Guid.Empty
        && snapshot.Peers.Count == 1 && snapshot.Peers[0] is { Ready: true } peer
        && peer.IsHost != host && peer.PlayerId != 0 && peer.PlayerId != snapshot.LocalPlayerId
        && peer.ConnectionId != Guid.Empty && peer.Challenge != Guid.Empty
        && Handshake(snapshot, 0) is not null;

    /// <summary>获取该连接确实读入的Hello/Welcome；不能从Ready或旧连接的trace推断新握手。</summary>
    public static ProtocolTrace? Handshake(ProtocolDiagnosticSnapshot snapshot, long afterSequence)
    {
        if (!Healthy(snapshot) || snapshot.Peers.Count != 1 || snapshot.Peers[0] is null) return null;
        var peer = snapshot.Peers[0];
        var accepted = snapshot.Trace.FirstOrDefault(t => t.Sequence > afterSequence
            && t.Kind == ProtocolTraceKind.Processed && t.PeerId == peer.PlayerId && t.ConnectionId == peer.ConnectionId
            && t.WorldGeneration == snapshot.WorldGeneration && t.DisconnectGeneration == snapshot.DisconnectGeneration
            && t.MessageType == (snapshot.IsHost ? ReadOnlyProtocolCodec.HelloType : ReadOnlyProtocolCodec.WelcomeType)
            && t.Observation is { BodyReadAttempted: true, RequestId: null, Reason: null } observation
            && observation.Kind == (snapshot.IsHost ? ProtocolReceiveKind.HelloAccepted : ProtocolReceiveKind.WelcomeAccepted)
            && observation.Challenge is Guid challenge && challenge != Guid.Empty
            && (snapshot.IsHost || challenge == peer.Challenge));
        if (accepted is null) return null;
        // 主机Welcome在实际Hello读取之后发送；客户端实际Hello尝试在Welcome接受之前。
        bool sent = snapshot.Trace.Any(t => t.Sequence > afterSequence && t.Kind == ProtocolTraceKind.SendAttempt
            && t.PeerId == peer.PlayerId && t.ConnectionId == peer.ConnectionId
            && t.WorldGeneration == snapshot.WorldGeneration && t.DisconnectGeneration == snapshot.DisconnectGeneration
            && t.MessageType == (snapshot.IsHost ? ReadOnlyProtocolCodec.WelcomeType : ReadOnlyProtocolCodec.HelloType)
            && (snapshot.IsHost ? t.Sequence == accepted.Sequence + 1 : t.Sequence < accepted.Sequence));
        return sent ? accepted : null;
    }

    public static bool HostGap(ProtocolDiagnosticSnapshot initial, ProtocolDiagnosticSnapshot gap) => Ready(initial, true)
        && Healthy(gap) && gap.Active && gap.ProbeAllowed && gap.IsHost && gap.Peers.Count == 0
        && gap.RunId == initial.RunId && gap.LocalPlayerId == initial.LocalPlayerId
        && gap.SessionId == initial.SessionId && gap.WorldGeneration == initial.WorldGeneration
        && gap.DisconnectGeneration > initial.DisconnectGeneration && gap.LastSequence >= initial.LastSequence;

    public static bool ClientTitle(ProtocolDiagnosticSnapshot initial, ProtocolDiagnosticSnapshot title) => Ready(initial, false)
        && Healthy(title) && !title.Active && !title.ProbeAllowed && !title.IsHost
        && title.SessionId is null && title.LocalPlayerId == 0 && title.Peers.Count == 0
        && title.RunId == initial.RunId && title.WorldGeneration > initial.WorldGeneration
        && title.DisconnectGeneration >= initial.DisconnectGeneration && title.LastSequence > initial.LastSequence;

    public static bool Reconnected(ProtocolDiagnosticSnapshot initial, ProtocolDiagnosticSnapshot gap,
        ProtocolDiagnosticSnapshot current, bool host)
    {
        if (!(host ? HostGap(initial, gap) : ClientTitle(initial, gap)) || !Ready(current, host)
            || current.RunId != initial.RunId || current.LocalPlayerId != initial.LocalPlayerId
            || current.SessionId != initial.SessionId || current.LastSequence <= gap.LastSequence
            || current.Peers[0].PlayerId != initial.Peers[0].PlayerId
            || current.Peers[0].ConnectionId == initial.Peers[0].ConnectionId
            || current.Peers[0].Challenge == initial.Peers[0].Challenge
            || (host ? current.WorldGeneration != initial.WorldGeneration || current.DisconnectGeneration != gap.DisconnectGeneration
                     : current.WorldGeneration <= gap.WorldGeneration || current.DisconnectGeneration < gap.DisconnectGeneration)) return false;
        var fresh = Handshake(current, gap.LastSequence);
        return fresh is not null && fresh.Observation!.Challenge != Handshake(initial, 0)!.Observation!.Challenge;
    }

    public static bool PairMatches(NetworkLifecycleReport host, NetworkLifecycleReport client) => Endpoint(host, true)
        && Endpoint(client, false) && host.HarnessRunId != client.HarnessRunId && host.ProcessId != client.ProcessId
        && host.ExpectedFixtureId == client.ExpectedFixtureId && host.ConfiguredFarmName == client.ConfiguredFarmName
        && host.ProductionDllSha256 == client.ProductionDllSha256 && host.HarnessDllSha256 == client.HarnessDllSha256
        && Match(host.InitialReady!.Protocol, client.InitialReady!.Protocol, 0, 0)
        && Match(host.Reconnected!.Protocol, client.Reconnected!.Protocol,
            host.DisconnectOrTitle!.Protocol.LastSequence, client.DisconnectOrTitle!.Protocol.LastSequence);

    private static bool Match(ProtocolDiagnosticSnapshot host, ProtocolDiagnosticSnapshot client, long hostAfter, long clientAfter) =>
        host.SessionId == client.SessionId && host.LocalPlayerId == client.Peers[0].PlayerId
        && client.LocalPlayerId == host.Peers[0].PlayerId
        && Handshake(host, hostAfter)!.Observation!.Challenge == Handshake(client, clientAfter)!.Observation!.Challenge;

    private static bool Endpoint(NetworkLifecycleReport report, bool host)
    {
        if (report is null || report.SchemaVersion != 1 || report.HarnessRunId == Guid.Empty || report.ProcessId <= 0
            || report.NetworkRole != (host ? "Host" : "Client") || report.Status != "Completed" || report.Failure is not null
            || report.Saved || report.ProbeSent || report.FullNet02Accepted || report.OldPacketReplayVerified || report.LoadedQueueLifecycleVerified
            || report.ExpectedFixtureId == 0 || !Sha(report.ProductionDllSha256) || !Sha(report.HarnessDllSha256)
            || report.InitialReady is not { } first || report.DisconnectOrTitle is not { } gap || report.Reconnected is not { } current
            || !ScopeMatches(true, host, false, 0, false, report.NetworkRole, report.ConfiguredFarmName,
                report.ExpectedFixtureId, first.FarmName, first.FixtureId)
            || current.FarmName != first.FarmName || current.FixtureId != first.FixtureId
            || first.LoadCount != 1 || gap.LoadCount != 1 || current.LoadCount != (host ? 1 : 2)
            || !Fingerprint(first) || !Fingerprint(current)
            || first.InventorySha256 != current.InventorySha256 || first.VisibleMachineRawSha256 != current.VisibleMachineRawSha256
            || first.VisibleMachineCount != current.VisibleMachineCount
            || !Reconnected(first.Protocol, gap.Protocol, current.Protocol, host)) return false;
        return host ? Fingerprint(gap) && gap.FixtureId == first.FixtureId && gap.FarmName == first.FarmName
            && gap.InventorySha256 == first.InventorySha256 && gap.VisibleMachineRawSha256 == first.VisibleMachineRawSha256
            && gap.VisibleMachineCount == first.VisibleMachineCount
            : gap.FixtureId == 0 && gap.FarmName is null && gap.InventorySha256 is null
              && gap.VisibleMachineRawSha256 is null && gap.VisibleMachineCount is null && gap.InventoryReferencesUnchanged is null;
    }

    private static bool Fingerprint(LifecycleCheckpoint point) => Sha(point.InventorySha256) && Sha(point.VisibleMachineRawSha256)
        && point.VisibleMachineCount is >= 0 && point.InventoryReferencesUnchanged is true;
    private static bool Sha(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
