using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameHarness;
using NetworkWelcomeReplayCheck;
using XiangyeShiling.Networking;

internal static class WelcomeReplayScenarios
{
    private const string Mod = "Sora6A.XiangyeShiling";
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static OutboundProtocolMessage Welcome(Guid session, Guid challenge, long client = 20, long host = 10) =>
        new(client, ReadOnlyProtocolCodec.WelcomeType, ReadOnlyProtocolCodec.Write(new ProtocolWelcome(1, 1, challenge, session, host, false)));

    public static void Run(Action<string, Action> test)
    {
        test("welcome replay buffer returns exact previous sent bytes once on a new ready connection", () =>
        {
            Guid session = Guid.NewGuid(), oldConnection = Guid.NewGuid(), current = Guid.NewGuid();
            var old = Welcome(session, Guid.NewGuid()); var fresh = Welcome(session, Guid.NewGuid());
            var buffer = new WelcomeReplayBuffer(10, session);
            Assert(buffer.ObserveSent(old, oldConnection) && buffer.ObserveSent(fresh, current), "Sent provenance not captured.");
            var peer = new ProtocolPeerStatus(20, current, false, true, Guid.NewGuid());
            Assert(buffer.TryTake(peer, current, out var replay) && replay!.Message == old
                && replay.PreviousConnectionId == oldConnection && replay.CurrentConnectionId == current, "Previous wire bytes/connection changed.");
            Assert(!buffer.TryTake(peer, current, out _), "Replay issued twice.");
            buffer.ObserveSent(fresh, current);
            Assert(!buffer.TryTake(peer, current, out _), "Hello retry renewed replay allowance.");
        });
        test("welcome replay buffer refuses first connection wrong target stale snapshot and nonready roles", () =>
        {
            Guid session = Guid.NewGuid(), current = Guid.NewGuid();
            var buffer = new WelcomeReplayBuffer(10, session);
            var peer = new ProtocolPeerStatus(20, current, false, true, Guid.NewGuid());
            buffer.ObserveSent(Welcome(session, Guid.NewGuid()), current);
            Assert(!buffer.TryTake(peer, current, out _), "First connection has no old source.");
            Guid next = Guid.NewGuid(); buffer.ObserveSent(Welcome(session, Guid.NewGuid()), next); peer = peer with { ConnectionId = next };
            foreach (var bad in new[] { peer with { Ready = false }, peer with { IsHost = true }, peer with { PlayerId = 21 }, peer with { ConnectionId = current } })
                Assert(!buffer.TryTake(bad, next, out _), "Unsafe source/role/state accepted.");
            Assert(!buffer.TryTake(peer, Guid.Empty, out _) && !buffer.TryTake(peer, current, out _), "Stale expected connection accepted.");
            Assert(buffer.TryTake(peer, next, out _), "Refusals consumed the valid one-shot.");
        });
        test("welcome replay buffer rejects foreign wire context and same-connection challenge substitution", () =>
        {
            Guid session = Guid.NewGuid(), connection = Guid.NewGuid();
            var buffer = new WelcomeReplayBuffer(10, session);
            var original = Welcome(session, Guid.NewGuid());
            foreach (var bad in new[] { Welcome(Guid.NewGuid(), Guid.NewGuid()), Welcome(session, Guid.NewGuid(), host: 99),
                Welcome(session, Guid.NewGuid(), client: 10), original with { MessageType = ReadOnlyProtocolCodec.HelloType }, original with { Json = "{" } })
                Assert(!buffer.ObserveSent(bad, connection), "Foreign/invalid sent bytes retained.");
            Assert(buffer.Count == 0 && !buffer.ObserveSent(original, Guid.Empty), "Invalid provenance allocated a source.");
            buffer.ObserveSent(original, connection);
            buffer.ObserveSent(Welcome(session, Guid.NewGuid()), connection);
            Assert(!buffer.TryTake(new(20, connection, false, true, Guid.NewGuid()), connection, out _), "Same connection manufactured an old connection.");
            var emptyWorld = new WelcomeReplayBuffer(10, session);
            Assert(!emptyWorld.TryTake(new(20, connection, false, true, Guid.NewGuid()), connection, out _), "New world inherited old replay memory.");
        });
        test("welcome replay buffer is bounded and owner-thread only", () =>
        {
            Guid session = Guid.NewGuid(); var buffer = new WelcomeReplayBuffer(10, session);
            for (int i = 0; i < 32; i++) Assert(buffer.ObserveSent(Welcome(session, Guid.NewGuid(), 100 + i), Guid.NewGuid()), "Bounded item refused.");
            Assert(!buffer.ObserveSent(Welcome(session, Guid.NewGuid(), 999), Guid.NewGuid()) && buffer.Count == 32, "Replay cache unbounded.");
            Task.Run(() =>
            {
                bool rejected = false;
                try { _ = buffer.Count; } catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "Foreign thread accessed replay memory.");
            }).GetAwaiter().GetResult();
        });
        test("welcome rejection observation identifies old challenge before and after fresh ready without mutating state", () =>
        {
            Guid session = Guid.NewGuid(); var client = new ReadOnlyProtocolSession(20, false, Mod, Guid.NewGuid());
            var peer = new ProtocolPeer(10, true, true, false, Guid.NewGuid());
            var hello = client.SynchronizePeers(new[] { peer }, 0).Single();
            ReadOnlyProtocolCodec.TryReadHello(hello.Json, out var oldHello);
            string old = Welcome(session, oldHello!.Challenge).Json;
            client.Receive(10, Mod, ReadOnlyProtocolCodec.WelcomeType, () => old, 0);
            client.SynchronizePeers(Array.Empty<ProtocolPeer>(), 1);
            hello = client.SynchronizePeers(new[] { peer with { ConnectionId = Guid.NewGuid() } }, 2).Single();
            ReadOnlyProtocolCodec.TryReadHello(hello.Json, out var fresh);
            int reads = 0;
            client.Receive(10, Mod, ReadOnlyProtocolCodec.WelcomeType, () => { reads++; return old; }, 2);
            Assert(client.RemoteSessionId is null && !client.ReadPeerStatus()[0].Ready
                && client.LastObservation is { Kind: ProtocolReceiveKind.WelcomeRejected, BodyReadAttempted: true }
                && client.LastObservation.Challenge == oldHello.Challenge, "Old pre-ready welcome lacks a bound read rejection.");
            client.Receive(10, Mod, ReadOnlyProtocolCodec.WelcomeType, () => Welcome(session, fresh!.Challenge).Json, 2);
            var rejection = new ProtocolRejected(1, session, Guid.NewGuid(), 20, ProtocolRejection.WritesDisabled);
            client.Receive(10, Mod, ReadOnlyProtocolCodec.RejectedType, () => ReadOnlyProtocolCodec.Write(rejection), 2);
            var before = client.ReadPeerStatus().ToArray();
            client.Receive(10, Mod, ReadOnlyProtocolCodec.WelcomeType, () => { reads++; return old; }, 2);
            Assert(reads == 2 && client.RemoteSessionId == session && client.LastRejection == rejection
                && client.ReadPeerStatus().SequenceEqual(before) && client.LastObservation.Kind == ProtocolReceiveKind.WelcomeRejected
                && client.LastObservation.Challenge == oldHello.Challenge, "Post-ready old welcome changed state or was not actually read.");
        });
        test("welcome rejection diagnostics do not invent reads for invalid bodies unknown routes or exhausted quota", () =>
        {
            var client = new ReadOnlyProtocolSession(20, false, Mod, Guid.NewGuid());
            client.SynchronizePeers(new[] { new ProtocolPeer(10, true, true, false, Guid.NewGuid()) }, 0);
            int reads = 0;
            client.Receive(10, "other.mod", ReadOnlyProtocolCodec.WelcomeType, () => { reads++; return null; }, 0);
            Assert(reads == 0 && client.LastObservation.Kind == ProtocolReceiveKind.Ignored, "Unknown route read.");
            for (int i = 0; i < 8; i++)
            {
                client.Receive(10, Mod, ReadOnlyProtocolCodec.WelcomeType, () => { reads++; return "{"; }, 0);
                Assert(client.LastObservation.Kind == ProtocolReceiveKind.InvalidMessage && client.LastObservation.Challenge is null, "Malformed body acquired challenge.");
            }
            client.Receive(10, Mod, ReadOnlyProtocolCodec.WelcomeType, () => { reads++; return null; }, 0);
            Assert(reads == 8 && client.LastObservation is { Kind: ProtocolReceiveKind.RateLimited, BodyReadAttempted: false }, "Quota rejection pretended a body read.");
        });

        var (host, clientReport) = Fixture();
        test("welcome replay exact shared gate pairs synthetic send and read rejection on the same fresh handshake", () =>
            Assert(NetworkWelcomeReplayEvidence.PairMatches(host, clientReport), "Complete synthetic pair failed; this is not SDK evidence."));
        test("welcome replay shared gate rejects omitted send suppression wrong challenge connection and no actual read", () =>
        {
            var point = clientReport.AfterReplay!; var trace = point.Protocol.Trace.Last();
            foreach (var bad in new[] { trace with { Kind = ProtocolTraceKind.SendSuppressed }, trace with { ConnectionId = Guid.NewGuid() },
                trace with { Observation = trace.Observation! with { Challenge = Guid.NewGuid() } },
                trace with { Observation = trace.Observation! with { BodyReadAttempted = false } },
                trace with { Observation = trace.Observation! with { Kind = ProtocolReceiveKind.WelcomeAccepted } },
                trace with { Quota = null } })
                Assert(!NetworkWelcomeReplayEvidence.PairMatches(host, clientReport with { AfterReplay = ReplaceLast(point, bad) }), "Unbound/fake rejection passed.");
            var send = host.AfterReplay!;
            Assert(!NetworkWelcomeReplayEvidence.PairMatches(host with { AfterReplay = ReplaceLast(send, send.Protocol.Trace.Last() with { Kind = ProtocolTraceKind.SendSuppressed }) }, clientReport), "Suppressed send paired.");
            Assert(!NetworkLifecycleEvidence.Healthy(send.Protocol), "Passive lifecycle accepted an active replay trace.");
        });
        test("welcome replay shared gate rejects state changes rewritten prefix missing phases and claimed capabilities", () =>
        {
            var point = clientReport.AfterReplay!;
            foreach (var bad in new[] { clientReport with { Saved = true }, clientReport with { FullNet02Accepted = true },
                clientReport with { LoadedQueueLifecycleVerified = true }, clientReport with { ReplayAttempted = true },
                clientReport with { Failure = "failed" }, clientReport with { BeforeReplay = null },
                clientReport with { AfterReplay = point with { InventorySha256 = new string('c', 64) } },
                clientReport with { AfterReplay = point with { InventoryReferencesUnchanged = false } },
                clientReport with { AfterReplay = point with { Protocol = point.Protocol with { SessionId = Guid.NewGuid() } } },
                clientReport with { AfterReplay = point with { Protocol = point.Protocol with { QueueRejected = 1 } } },
                clientReport with { AfterReplay = point with { Protocol = point.Protocol with { Trace = point.Protocol.Trace.Select(t => t.Sequence == 1 ? t with { ThreadId = 99 } : t).ToArray() } } },
                clientReport with { Lifecycle = clientReport.Lifecycle with { HarnessRunId = host.Lifecycle.HarnessRunId } } })
                Assert(!NetworkWelcomeReplayEvidence.PairMatches(host, bad), "Incomplete/spliced/mutated state passed.");
        });
        var options = new JsonSerializerOptions(); options.Converters.Add(new JsonStringEnumConverter());
        byte[] Serialize(NetworkWelcomeReplayReport report) => JsonSerializer.SerializeToUtf8Bytes(report, options);
        bool Refused(string value)
        {
            try { using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value)); NetworkWelcomeReplayReportReader.Read(stream); return false; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or JsonException or KeyNotFoundException) { return true; }
        }
        test("welcome replay strict reader preserves bounded synthetic original bytes and nested fields", () =>
        {
            byte[] raw = Serialize(host), copy = raw.ToArray(); using var stream = new MemoryStream(raw);
            var read = NetworkWelcomeReplayReportReader.Read(stream);
            Assert(raw.SequenceEqual(copy) && read.Hash.Length == 64 && NetworkWelcomeReplayEvidence.PairMatches(read.Report, clientReport), "Strict pair roundtrip failed.");
        });
        test("welcome replay strict reader refuses missing duplicate alias nested flags types and oversize inputs", () =>
        {
            string raw = Encoding.UTF8.GetString(Serialize(host));
            foreach (string bad in new[] { "{}", "null", "[]", "{", raw.Replace("\"Saved\":false,", ""),
                raw.Replace("\"ReplayAttempted\":true", "\"ReplayAttempted\":true,\"replayAttempted\":false"),
                raw.Replace("\"ReplayAttempted\":true", "\"ReplayAttempted\":true,\"ReplayAttempted\":false"),
                raw.Replace("\"ReplayAttempted\":true", "\"ReplayAttempted\":1"), raw.Replace("\"BodyReadAttempted\":false,", ""),
                new string(' ', NetworkWelcomeReplayReportReader.MaximumBytes + 1) })
                Assert(Refused(bad), "Ambiguous/oversize/missing nested declaration accepted.");
        });
    }

    private static LifecycleCheckpoint ReplaceLast(LifecycleCheckpoint point, ProtocolTrace trace) => point with
    { Protocol = point.Protocol with { Trace = point.Protocol.Trace.Take(point.Protocol.Trace.Count - 1).Append(trace).ToArray() } };

    internal static (NetworkWelcomeReplayReport Host, NetworkWelcomeReplayReport Client) Fixture()
    {
        var (host, client) = NetworkLifecycleScenarios.Fixture();
        NetworkWelcomeReplayReport Report(NetworkLifecycleReport life, bool isHost)
        {
            var before = life.Reconnected!; var snapshot = before.Protocol; var peer = snapshot.Peers[0];
            Guid old = NetworkLifecycleEvidence.Handshake(life.InitialReady!.Protocol, 0)!.Observation!.Challenge!.Value;
            ProtocolTrace Trace(long offset, ProtocolTraceKind kind, ProtocolObservation? observation, ProtocolQuotaWindow? quota = null) =>
                new(snapshot.LastSequence + offset, snapshot.Trace.Last().Milliseconds + offset, snapshot.WorldGeneration,
                    snapshot.DisconnectGeneration, snapshot.Trace[0].ThreadId, kind, peer.PlayerId,
                    ReadOnlyProtocolCodec.WelcomeType, observation, quota, peer.ConnectionId);
            var delta = isHost ? new[] { Trace(1, ProtocolTraceKind.WelcomeReplayAttempt, new(ProtocolReceiveKind.Ignored, false, Challenge: old)),
                Trace(2, ProtocolTraceKind.SendAttempt, null) }
                : new[] { Trace(1, ProtocolTraceKind.Processed, new(ProtocolReceiveKind.WelcomeRejected, true, Challenge: old), new(0, 2, true)) };
            var after = before with { Protocol = snapshot with { LastSequence = snapshot.LastSequence + delta.Length, Trace = snapshot.Trace.Concat(delta).ToArray() } };
            return new(1, life, "Completed", null, false, isHost, false, false, before, after);
        }
        return (Report(host, true), Report(client, false));
    }
}
