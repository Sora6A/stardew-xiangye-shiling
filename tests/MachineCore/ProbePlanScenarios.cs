using System.Text.Json;
using XiangyeShiling.Networking;

/// <summary>执行真实探针计划和纯协议源码；没有 SMAPI、网络连接或真实设备/库存。</summary>
internal static class ProbePlanScenarios
{
    public static void Run(Action<string, Action> test)
    {
        test("probe start and collect roundtrip fixed synthetic intentions", () =>
        {
            Guid session = Guid.NewGuid(), requestId = Guid.NewGuid();
            foreach (ProtocolProbeKind kind in new[] { ProtocolProbeKind.Start, ProtocolProbeKind.Collect })
            {
                string json = ProtocolProbePlan.Create(kind, session, requestId, 20);
                Assert(MachineRequestCodec.TryRead(json, out var request), "A normal probe was invalid wire text.");
                Assert(ProtocolProbePlan.TargetIsSynthetic && request!.ProtocolVersion == MachineRequestEnvelope.CurrentProtocol
                    && request.SessionId == session && request.RequestId == requestId && request.ActorId == 20
                    && request.LocationName == "FarmHouse" && request.TileX == 0 && request.TileY == 0
                    && request.InstanceId == requestId && request.ExpectedRevision == 0, "Probe changed its fixed synthetic target or identity.");
                Assert(request.Action == (kind == ProtocolProbeKind.Start ? MachineRequestAction.Start : MachineRequestAction.Collect)
                    && request.RecipeId == (kind == ProtocolProbeKind.Start ? "tofu" : null), "Probe changed action/recipe semantics.");
                Assert(ProtocolProbePlan.ExpectedReason(kind) == ProtocolRejection.WritesDisabled, "A normal probe expected success.");
            }
        });

        test("probe actor mismatch remains nonzero and distinct across long boundaries", () =>
        {
            Guid session = Guid.NewGuid(), requestId = Guid.NewGuid();
            foreach (long actor in new[] { long.MinValue, long.MinValue + 1, -1L, 1L, 2L, long.MaxValue - 1, long.MaxValue })
            {
                string json = ProtocolProbePlan.Create(ProtocolProbeKind.ActorMismatch, session, requestId, actor);
                Assert(MachineRequestCodec.TryRead(json, out var request) && request!.ActorId != 0 && request.ActorId != actor,
                    "Actor mismatch overflowed, became zero, or matched the real actor.");
                Assert(request!.SessionId == session && request.RequestId == requestId && request.InstanceId == requestId
                    && request.Action == MachineRequestAction.Start && request.RecipeId == "tofu",
                    "Actor mismatch unexpectedly changed another command field.");
            }
            Assert(ProtocolProbePlan.ExpectedReason(ProtocolProbeKind.ActorMismatch) == ProtocolRejection.ActorMismatch,
                "Actor mismatch expected the wrong rejection.");
        });

        test("probe wrong session is fresh and malformed text is fixed", () =>
        {
            Guid session = Guid.NewGuid(), requestId = Guid.NewGuid();
            for (int i = 0; i < 8; i++)
            {
                string json = ProtocolProbePlan.Create(ProtocolProbeKind.WrongSession, session, requestId, 20);
                Assert(MachineRequestCodec.TryRead(json, out var request) && request!.SessionId != Guid.Empty && request.SessionId != session,
                    "Wrong-session probe reused the current or empty session.");
                Assert(request!.ActorId == 20 && request.RequestId == requestId && request.InstanceId == requestId,
                    "Wrong-session probe changed other identity fields.");
            }
            string malformed = ProtocolProbePlan.Create(ProtocolProbeKind.Malformed, session, requestId, 20);
            Assert(malformed == "{invalid" && !MachineRequestCodec.TryRead(malformed, out var invalid) && invalid is null,
                "Malformed probe became valid or caller-controlled wire text.");
            Assert(ProtocolProbePlan.ExpectedReason(ProtocolProbeKind.WrongSession) == ProtocolRejection.WrongSession
                && ProtocolProbePlan.ExpectedReason(ProtocolProbeKind.Malformed) == ProtocolRejection.InvalidRequest,
                "Negative probe expected the wrong rejection.");
        });

        test("probe payloads contain only the complete intention schema", () =>
        {
            string[] fields = { "ProtocolVersion", "SessionId", "RequestId", "ActorId", "LocationName", "TileX", "TileY",
                "InstanceId", "ExpectedRevision", "Action", "RecipeId" };
            foreach (ProtocolProbeKind kind in new[] { ProtocolProbeKind.Start, ProtocolProbeKind.Collect,
                         ProtocolProbeKind.ActorMismatch, ProtocolProbeKind.WrongSession })
            {
                string json = ProtocolProbePlan.Create(kind, Guid.NewGuid(), Guid.NewGuid(), 20);
                using JsonDocument document = JsonDocument.Parse(json);
                string[] actual = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
                Assert(actual.Length == fields.Length && actual.ToHashSet(StringComparer.Ordinal).SetEquals(fields),
                    "Probe introduced client inventory, clock, product, or other extra fields.");
                Assert(json.Length <= MachineRequestCodec.MaxUtf8Bytes, "A fixed probe exceeded its text bound.");
            }
        });

        test("probe invalid identities and unknown kinds throw before producing text", () =>
        {
            Guid session = Guid.NewGuid(), requestId = Guid.NewGuid();
            foreach (ProtocolProbeKind kind in Enum.GetValues<ProtocolProbeKind>())
            {
                ExpectArgument(() => ProtocolProbePlan.Create(kind, Guid.Empty, requestId, 20));
                ExpectArgument(() => ProtocolProbePlan.Create(kind, session, Guid.Empty, 20));
                ExpectArgument(() => ProtocolProbePlan.Create(kind, session, requestId, 0));
            }
            foreach (ProtocolProbeKind unknown in new[] { (ProtocolProbeKind)(-1), (ProtocolProbeKind)5, (ProtocolProbeKind)int.MaxValue })
            {
                ExpectArgument(() => ProtocolProbePlan.Create(unknown, session, requestId, 20));
                ExpectArgument(() => ProtocolProbePlan.ExpectedReason(unknown));
            }
        });

        test("pure read-only session rejects every generated probe with writes disabled", () =>
        {
            const string modId = "Sora6A.XiangyeShiling";
            const long hostId = 10, actorId = 20;
            Guid session = Guid.NewGuid();
            var host = new ReadOnlyProtocolSession(hostId, true, modId, session);
            var client = new ReadOnlyProtocolSession(actorId, false, modId, Guid.NewGuid());
            host.SynchronizePeers(new[] { new ProtocolPeer(actorId, false, true, false, Guid.NewGuid()) }, 0);
            OutboundProtocolMessage hello = client.SynchronizePeers(new[] { new ProtocolPeer(hostId, true, true, false, Guid.NewGuid()) }, 0).Single();
            var welcome = host.Receive(actorId, modId, hello.MessageType, () => hello.Json, 0);
            Assert(welcome is not null && ReadOnlyProtocolCodec.TryReadWelcome(welcome.Json, out var decoded)
                && !decoded!.WritesEnabled, "The genuine pure handshake advertised write access.");
            client.Receive(hostId, modId, welcome!.MessageType, () => welcome.Json, 0);
            Assert(client.RemoteSessionId == session, "Probe session did not complete the normal pure handshake.");

            foreach (ProtocolProbeKind kind in Enum.GetValues<ProtocolProbeKind>())
            {
                Guid requestId = Guid.NewGuid();
                string json = ProtocolProbePlan.Create(kind, session, requestId, actorId);
                var response = host.Receive(actorId, modId, ReadOnlyProtocolCodec.RequestType, () => json, 0);
                Assert(response is not null && response.PlayerId == actorId
                    && response.MessageType == ReadOnlyProtocolCodec.RejectedType
                    && ReadOnlyProtocolCodec.TryReadRejected(response.Json, out var rejected)
                    && rejected!.Reason == ProtocolProbePlan.ExpectedReason(kind) && rejected.SessionId == session
                    && rejected.ActorId == actorId && rejected.RequestId == (kind == ProtocolProbeKind.Malformed ? null : requestId),
                    "A generated probe was accepted or received an unbound/wrong rejection.");
                client.Receive(hostId, modId, response!.MessageType, () => response.Json, 0);
                Assert(client.RemoteSessionId == session && client.LastRejection?.Reason == ProtocolProbePlan.ExpectedReason(kind),
                    "Probe rejection altered the established session or failed diagnostic routing.");
            }
        });
    }

    private static void ExpectArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid probe arguments did not throw.");
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
