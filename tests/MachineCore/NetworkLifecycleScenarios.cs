using GameHarness;
using XiangyeShiling.Networking;

internal static class NetworkLifecycleScenarios
{
    public static void Run(Action<string, Action> test)
    {
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        var (host, client) = Fixture();
        test("lifecycle actual shared gate accepts a fresh same-ID host/client rehandshake pair", () =>
            Assert(NetworkLifecycleEvidence.PairMatches(host, client), "Artificial complete pair should pass the exact shared gate."));
        test("lifecycle scope refuses preview wrong roles worlds and split screens", () =>
        {
            bool Scope(bool main = true, bool split = false, int screen = 0, bool preview = false,
                string role = "Host", ulong actual = 450604963, string? farm = "XYSNET02") =>
                NetworkLifecycleEvidence.ScopeMatches(true, main, split, screen, preview, role, "XYSNET02", 450604963, farm, actual);
            Assert(Scope() && Scope(main: false, role: "Client"), "Dedicated scopes were rejected.");
            foreach (bool value in new[] { Scope(split: true), Scope(screen: 1), Scope(preview: true), Scope(role: "Observe"),
                Scope(main: false), Scope(actual: 0), Scope(actual: 9), Scope(farm: null), Scope(farm: "XYSP1UI") })
                Assert(!value, "Unsafe lifecycle scope accepted.");
        });
        test("lifecycle host gap must retain session world and identity after a real disconnect", () =>
        {
            var initial = host.InitialReady!.Protocol;
            var gap = host.DisconnectOrTitle!.Protocol;
            foreach (var bad in new[] { gap with { SessionId = Guid.NewGuid() }, gap with { WorldGeneration = 3 },
                gap with { DisconnectGeneration = 0 }, gap with { Peers = initial.Peers }, gap with { LocalPlayerId = 99 } })
                Assert(!NetworkLifecycleEvidence.HostGap(initial, bad), "Unrelated host gap was accepted.");
        });
        test("lifecycle client title requires same run cleared session peers queue and later generation", () =>
        {
            var initial = client.InitialReady!.Protocol;
            var title = client.DisconnectOrTitle!.Protocol;
            foreach (var bad in new[] { title with { RunId = Guid.NewGuid() }, title with { Active = true },
                title with { SessionId = initial.SessionId }, title with { QueueCount = 1 },
                title with { Peers = initial.Peers }, title with { WorldGeneration = initial.WorldGeneration } })
                Assert(!NetworkLifecycleEvidence.ClientTitle(initial, bad), "Uncleared/unrelated title was accepted.");
        });
        test("lifecycle cannot reuse old connection challenge ready or trace as new handshake", () =>
        {
            var first = client.InitialReady!.Protocol;
            var gap = client.DisconnectOrTitle!.Protocol;
            var current = client.Reconnected!.Protocol;
            foreach (var bad in new[] {
                current with { Peers = new[] { current.Peers[0] with { ConnectionId = first.Peers[0].ConnectionId } } },
                current with { Peers = new[] { current.Peers[0] with { Challenge = first.Peers[0].Challenge } } },
                current with { Trace = current.Trace.Select(t => t.Sequence == 5 ? t with { Observation = new(ProtocolReceiveKind.Ignored, false) } : t).ToArray() },
                current with { Trace = current.Trace.Select(t => t.Sequence == 4 ? t with { Kind = ProtocolTraceKind.SendSuppressed } : t).ToArray() },
                current with { WorldGeneration = gap.WorldGeneration }, current with { RunId = Guid.NewGuid() },
                current with { LocalPlayerId = 99 }, current with { SessionId = Guid.NewGuid() } })
                Assert(!NetworkLifecycleEvidence.Reconnected(first, gap, bad, false), "Reused or spliced handshake passed.");
        });
        test("lifecycle rejects lost traces faults probes and foreign-thread snapshots", () =>
        {
            var ready = host.Reconnected!.Protocol;
            foreach (var bad in new[] { ready with { Faulted = true }, ready with { WritesEnabled = true },
                ready with { Evicted = 1 }, ready with { QueueRejected = 1 }, ready with { QueueCount = 1 },
                ready with { Trace = ready.Trace.Skip(1).ToArray() }, ready with { Trace = null! },
                ready with { Trace = ready.Trace.Select(t => t.Sequence == 3 ? t with { ThreadId = 99 } : t).ToArray() },
                ready with { Trace = ready.Trace.Select(t => t.Sequence == 1 ? t with { Kind = ProtocolTraceKind.ProbeAttempt } : t).ToArray() } })
                Assert(!NetworkLifecycleEvidence.Healthy(bad), "Incomplete or unsafe trace passed.");
        });
        test("lifecycle report rejects changed inventory load count process binary and claimed abilities", () =>
        {
            foreach (var bad in new[] { client with { ProcessId = host.ProcessId }, client with { HarnessRunId = host.HarnessRunId },
                client with { ProductionDllSha256 = new string('1', 64) }, client with { HarnessDllSha256 = new string('2', 64) },
                client with { ExpectedFixtureId = 99 }, client with { Failure = "failed" }, client with { Saved = true },
                client with { ProbeSent = true }, client with { FullNet02Accepted = true }, client with { OldPacketReplayVerified = true },
                client with { LoadedQueueLifecycleVerified = true }, client with { Status = "WaitingReconnect" },
                client with { Reconnected = client.Reconnected! with { LoadCount = 3 } },
                client with { Reconnected = client.Reconnected! with { InventoryReferencesUnchanged = false } },
                client with { Reconnected = client.Reconnected! with { InventorySha256 = new string('3', 64) } },
                client with { InitialReady = client.InitialReady! with { Protocol = null! } } })
                Assert(!NetworkLifecycleEvidence.PairMatches(host, bad), "Forged/mismatched lifecycle report passed.");
        });
        test("lifecycle pair compares shared challenges without equating local connection thread or generation", () =>
        {
            var current = client.Reconnected!.Protocol;
            Guid wrong = Guid.NewGuid();
            var bad = current with { Peers = new[] { current.Peers[0] with { Challenge = wrong } },
                Trace = current.Trace.Select(t => t.Sequence == 5 ? t with { Observation = t.Observation! with { Challenge = wrong } } : t).ToArray() };
            Assert(NetworkLifecycleEvidence.Reconnected(client.InitialReady!.Protocol, client.DisconnectOrTitle!.Protocol, bad, false),
                "Artificial client-local evidence should be consistent before cross-endpoint check.");
            Assert(!NetworkLifecycleEvidence.PairMatches(host, client with { Reconnected = client.Reconnected! with { Protocol = bad } }),
                "Different accepted challenges across endpoints were paired.");
        });
    }

    internal static (NetworkLifecycleReport Host, NetworkLifecycleReport Client) Fixture()
    {
        Guid session = Guid.NewGuid(), hostRun = Guid.NewGuid(), clientRun = Guid.NewGuid();
        Guid oldChallenge = Guid.NewGuid(), newChallenge = Guid.NewGuid();
        var hp1 = new ProtocolPeerStatus(20, Guid.NewGuid(), false, true, Guid.NewGuid());
        var hp2 = hp1 with { ConnectionId = Guid.NewGuid(), Challenge = Guid.NewGuid() };
        var cp1 = new ProtocolPeerStatus(10, Guid.NewGuid(), true, true, oldChallenge);
        var cp2 = cp1 with { ConnectionId = Guid.NewGuid(), Challenge = newChallenge };
        ProtocolTrace T(long seq, long world, long disc, ProtocolTraceKind kind, ProtocolPeerStatus? peer = null,
            string? type = null, ProtocolReceiveKind? accepted = null, Guid? challenge = null) => new(seq, seq * 100, world,
                disc, 7, kind, peer?.PlayerId, type, accepted is null ? null : new(accepted.Value, true, Challenge: challenge),
                null, peer?.ConnectionId);
        var hTrace = new[] { T(1, 2, 0, ProtocolTraceKind.Processed, hp1, ReadOnlyProtocolCodec.HelloType, ProtocolReceiveKind.HelloAccepted, oldChallenge),
            T(2, 2, 0, ProtocolTraceKind.SendAttempt, hp1, ReadOnlyProtocolCodec.WelcomeType) };
        var cTrace = new[] { T(1, 2, 0, ProtocolTraceKind.SendAttempt, cp1, ReadOnlyProtocolCodec.HelloType),
            T(2, 2, 0, ProtocolTraceKind.Processed, cp1, ReadOnlyProtocolCodec.WelcomeType, ProtocolReceiveKind.WelcomeAccepted, oldChallenge) };
        ProtocolDiagnosticSnapshot Snap(Guid run, bool host, long world, long disc, ProtocolPeerStatus[] peers, ProtocolTrace[] trace) =>
            new(run, true, true, true, host, false, false, host ? 10 : 20, session, world, disc, 0, 0, trace.Length, 0, peers, trace);
        var hi = Snap(hostRun, true, 2, 0, new[] { hp1 }, hTrace);
        var hg = hi with { Peers = Array.Empty<ProtocolPeerStatus>(), DisconnectGeneration = 1 };
        var hr = Snap(hostRun, true, 2, 1, new[] { hp2 }, hTrace.Concat(new[] {
            T(3, 2, 1, ProtocolTraceKind.Processed, hp2, ReadOnlyProtocolCodec.HelloType, ProtocolReceiveKind.HelloAccepted, newChallenge),
            T(4, 2, 1, ProtocolTraceKind.SendAttempt, hp2, ReadOnlyProtocolCodec.WelcomeType) }).ToArray());
        var ci = Snap(clientRun, false, 2, 0, new[] { cp1 }, cTrace);
        var ct = ci with { Active = false, ProbeAllowed = false, SessionId = null, LocalPlayerId = 0, WorldGeneration = 3,
            Peers = Array.Empty<ProtocolPeerStatus>(), Trace = cTrace.Append(T(3, 3, 0, ProtocolTraceKind.Reset)).ToArray(), LastSequence = 3 };
        var cr = Snap(clientRun, false, 5, 0, new[] { cp2 }, ct.Trace.Concat(new[] {
            T(4, 5, 0, ProtocolTraceKind.SendAttempt, cp2, ReadOnlyProtocolCodec.HelloType),
            T(5, 5, 0, ProtocolTraceKind.Processed, cp2, ReadOnlyProtocolCodec.WelcomeType, ProtocolReceiveKind.WelcomeAccepted, newChallenge) }).ToArray());
        LifecycleCheckpoint Point(ProtocolDiagnosticSnapshot snap, int load) => new(snap, load, 450604963, "XYSNET02",
            new string('a', 64), new string('b', 64), 0, true);
        NetworkLifecycleReport Report(bool host, LifecycleCheckpoint initial, LifecycleCheckpoint gap, LifecycleCheckpoint current) =>
            new(1, Guid.NewGuid(), host ? 100 : 200, host ? "Host" : "Client", 450604963, "XYSNET02", new string('e', 64),
                new string('f', 64), "Completed", null, false, false, false, false, false, initial, gap, current, null);
        return (Report(true, Point(hi, 1), Point(hg, 1), Point(hr, 1)),
            Report(false, Point(ci, 1), new(ct, 1, 0, null, null, null, null, null), Point(cr, 2)));
    }
}
