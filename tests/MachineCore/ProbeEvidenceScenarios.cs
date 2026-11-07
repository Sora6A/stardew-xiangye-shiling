using GameHarness;
using XiangyeShiling.Networking;

internal static class ProbeEvidenceScenarios
{
    public static void Run(Action<string, Action> test)
    {
        var context = new ProbeEvidenceContext(Guid.NewGuid(), Guid.NewGuid(), 2, 3, 10, Guid.NewGuid(), 0, 0, 1);
        var trace = new ProtocolTrace(11, 100, 2, 3, 1, ProtocolTraceKind.Processed, 10,
            ReadOnlyProtocolCodec.RejectedType, new(ProtocolReceiveKind.RejectionAccepted, true, null, ProtocolRejection.InvalidRequest));
        ProtocolDiagnosticSnapshot Snapshot(ProtocolTrace value) => new(context.RunId, true, true, true, false, false,
            false, 20, context.SessionId, 2, 3, 0, 0, 11, 0,
            new[] { new ProtocolPeerStatus(10, context.HostConnectionId, true, true, Guid.NewGuid()) }, new[] { value });
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        test("network probe evidence rejects late null-id response from another generation or connection", () =>
        {
            Assert(!NetworkProbeEvidence.HasRejection(Snapshot(trace with { WorldGeneration = 1 }), context, 10, null, ProtocolRejection.InvalidRequest),
                "BUG-2026-016: late null-id response from old world was accepted as this malformed probe.");
            var reconnected = Snapshot(trace) with { Peers = new[] { new ProtocolPeerStatus(10, Guid.NewGuid(), true, true, Guid.NewGuid()) } };
            Assert(!NetworkProbeEvidence.HasRejection(reconnected, context, 10, null, ProtocolRejection.InvalidRequest),
                "BUG-2026-016: unchanged host session reused a different connection's evidence.");
        });
        test("network probe evidence requires actual current host read and subsequent correlated rejection", () =>
        {
            Assert(NetworkProbeEvidence.HasRejection(Snapshot(trace), context, 10, null, ProtocolRejection.InvalidRequest), "Current malformed rejection failed.");
            foreach (var invalid in new[] { trace with { Sequence = 10 }, trace with { DisconnectGeneration = 2 },
                trace with { PeerId = 30 }, trace with { MessageType = ReadOnlyProtocolCodec.WelcomeType },
                trace with { ThreadId = 2 }, trace with { Observation = trace.Observation! with { BodyReadAttempted = false } },
                trace with { Observation = trace.Observation! with { Kind = ProtocolReceiveKind.RejectionProduced } },
                trace with { Observation = trace.Observation! with { RequestId = Guid.NewGuid() } } })
                Assert(!NetworkProbeEvidence.HasRejection(Snapshot(invalid), context, 10, null, ProtocolRejection.InvalidRequest), "Unrelated trace was accepted.");
        });
        test("network probe evidence rejects dropped trace queue faults and another protocol run", () =>
        {
            foreach (var invalid in new[] { Snapshot(trace) with { Evicted = 1 }, Snapshot(trace) with { QueueRejected = 1 },
                Snapshot(trace) with { Faulted = true }, Snapshot(trace) with { WritesEnabled = true },
                Snapshot(trace) with { RunId = Guid.NewGuid() }, Snapshot(trace) with { SessionId = Guid.NewGuid() },
                Snapshot(trace) with { Active = false }, Snapshot(trace) with { DisconnectGeneration = 4 } })
                Assert(!NetworkProbeEvidence.HasRejection(invalid, context, 10, null, ProtocolRejection.InvalidRequest), "Incomplete or different-run trace passed.");
        });
    }
}
