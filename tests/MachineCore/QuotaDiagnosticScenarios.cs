using XiangyeShiling.Networking;

/// <summary>
/// 直接验证实际只读会话的配额观测与正文读取，供离线双端配对使用。
/// 本地窗口、线程和连接标记不是跨进程时间或认证；这些纯场景不证明 SMAPI 实际传输。
/// </summary>
internal static class QuotaDiagnosticScenarios
{
    private const string ModId = "Sora6A.XiangyeShiling";
    private const long HostId = 10, ClientId = 20;

    public static void Run(Action<string, Action> test)
    {
        test("quota diagnostics bind eight actual reads and the unread ninth to one host window", () =>
        {
            Guid sessionId = Guid.NewGuid();
            var host = new ReadOnlyProtocolSession(HostId, true, ModId, sessionId);
            host.SynchronizePeers(new[] { ClientPeer() }, 1000);
            int reads = 0;
            string hello = ReadOnlyProtocolCodec.Write(new ProtocolHello(1, Guid.NewGuid(), ClientId));
            Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.HelloType,
                () => { reads++; return hello; }, 1000) is not null, "The host did not complete the prerequisite hello.");
            Quota(host, 1000, 1, true);

            // Start a real new fixed window: the earlier hello must not secretly consume a burst slot.
            for (int count = 1; count <= ReadOnlyProtocolSession.MessagesPerWindow; count++)
            {
                Guid id = Guid.NewGuid();
                string body = ProtocolProbePlan.Create(ProtocolProbeKind.Start, sessionId, id, ClientId);
                var response = host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                    () => { reads++; return body; }, 2000);
                Quota(host, 2000, count, true);
                Assert(response is not null && host.LastObservation == new ProtocolObservation(
                    ProtocolReceiveKind.RejectionProduced, true, id, ProtocolRejection.WritesDisabled),
                    "An admitted burst request lost its actual read, identity, or rejection evidence.");
            }
            Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => throw new InvalidOperationException("The ninth body must remain unread."), 2000) is null,
                "An over-quota burst request produced a response.");
            Quota(host, 2000, 8, false);
            Assert(reads == 9 && host.LastObservation == new ProtocolObservation(ProtocolReceiveKind.RateLimited, false),
                "Rate limiting read a ninth burst body or retained the previous request/rejection metadata.");
        });

        test("quota diagnostics clear filtered evidence and reset only a genuinely new peer connection", () =>
        {
            Guid sessionId = Guid.NewGuid();
            var peer = ClientPeer();
            var host = new ReadOnlyProtocolSession(HostId, true, ModId, sessionId);
            host.SynchronizePeers(new[] { peer }, 500);
            int reads = 0;
            void Read(long now)
            {
                Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                    () => { reads++; return "{invalid"; }, now) is not null, "An admitted malformed body was not rejected.");
            }
            Read(500);
            Quota(host, 500, 1, true);
            foreach (var route in new[]
            {
                (30L, ModId, ReadOnlyProtocolCodec.RequestType),
                (ClientId, "another.mod", ReadOnlyProtocolCodec.RequestType),
                (ClientId, ModId, "unknown/type"),
                (ClientId, ModId, ReadOnlyProtocolCodec.WelcomeType)
            })
            {
                Assert(host.Receive(route.Item1, route.Item2, route.Item3,
                    () => throw new InvalidOperationException("A filtered route must not read its body."), 500) is null,
                    "Filtered traffic produced a response.");
                NoQuota(host);
            }
            foreach (long backwards in new long[] { 499, -1 })
            {
                Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                    () => throw new InvalidOperationException("A backwards clock must not read its body."), backwards) is null,
                    "A backwards clock produced a response.");
                NoQuota(host);
            }
            Read(500);
            Quota(host, 500, 2, true);
            Assert(reads == 2, "Filtering or clock rollback read a body or renewed an existing allowance.");

            host.SynchronizePeers(Array.Empty<ProtocolPeer>(), 600);
            Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => throw new InvalidOperationException("A disconnected peer must not read its body."), 600) is null,
                "A disconnected peer produced a response.");
            NoQuota(host);
            host.SynchronizePeers(new[] { peer with { ConnectionId = Guid.NewGuid() } }, 700);
            Read(700);
            Quota(host, 700, 1, true);
            Assert(host.ActiveSessionId == sessionId && reads == 3,
                "A peer reconnect changed the host world session or inherited the old connection's quota.");
        });

        test("quota diagnostics include hello and malformed reads and distinguish a fixed window boundary", () =>
        {
            Guid sessionId = Guid.NewGuid();
            var host = new ReadOnlyProtocolSession(HostId, true, ModId, sessionId);
            host.SynchronizePeers(new[] { ClientPeer() }, 0);
            int reads = 0;
            string request = ProtocolProbePlan.Create(ProtocolProbeKind.Start, sessionId, Guid.NewGuid(), ClientId);
            Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => { reads++; return request; }, 0) is not null
                && host.LastObservation.Reason == ProtocolRejection.HandshakeRequired,
                "The pre-handshake request did not consume a real body read.");
            Quota(host, 0, 1, true);
            string hello = ReadOnlyProtocolCodec.Write(new ProtocolHello(1, Guid.NewGuid(), ClientId));
            Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.HelloType,
                () => { reads++; return hello; }, 1) is not null, "The hello was not admitted after an earlier request.");
            Quota(host, 0, 2, true);
            for (int count = 3; count <= 8; count++)
            {
                Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                    () => { reads++; return "{invalid"; }, 2) is not null,
                    "Malformed known-direction traffic failed to consume its allowance.");
                Quota(host, 0, count, true);
                Assert(host.LastObservation == new ProtocolObservation(ProtocolReceiveKind.RejectionProduced, true,
                    Reason: ProtocolRejection.InvalidRequest), "A malformed read lost its null-id rejection evidence.");
            }
            Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => throw new InvalidOperationException("The old window remains full."), 999) is null,
                "The old window regained allowance before its boundary.");
            Quota(host, 0, 8, false);
            ProtocolQuotaWindow oldWindow = host.LastQuota!;
            Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => { reads++; return request; }, 1000) is not null, "The next fixed window did not admit its first body.");
            Quota(host, 1000, 1, true);
            Assert(reads == 9 && oldWindow == new ProtocolQuotaWindow(0, 8, false),
                "Window rollover changed an earlier immutable observation or read an over-quota body.");
            // These receives are 1ms apart but belong to different windows; elapsed span cannot pair quota evidence.
            Assert(host.LastQuota!.StartMilliseconds != oldWindow.StartMilliseconds,
                "A short timestamp span incorrectly collapsed two actual quota windows.");
        });

        test("quota diagnostics refuse foreign thread reads without replacing owner evidence", () =>
        {
            var host = new ReadOnlyProtocolSession(HostId, true, ModId, Guid.NewGuid());
            host.SynchronizePeers(new[] { ClientPeer() }, 0);
            host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType, () => "{invalid", 0);
            var originalQuota = host.LastQuota;
            var originalObservation = host.LastObservation;
            var failures = new List<Exception>();
            var thread = new Thread(() =>
            {
                foreach (Action operation in new Action[]
                {
                    () => { _ = host.LastQuota; },
                    () => { host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                        () => throw new Exception("A foreign thread must not read a body."), 0); }
                })
                {
                    try { operation(); failures.Add(new Exception("Foreign quota evidence access was accepted.")); }
                    catch (InvalidOperationException) { }
                    catch (Exception ex) { failures.Add(ex); }
                }
            });
            thread.Start(); thread.Join();
            Assert(failures.Count == 0 && ReferenceEquals(host.LastQuota, originalQuota)
                && ReferenceEquals(host.LastObservation, originalObservation),
                "Foreign access exposed quota state or cleared an owner-thread observation.");
        });
    }

    private static ProtocolPeer ClientPeer() => new(ClientId, false, true, false, Guid.NewGuid());
    private static void Quota(ReadOnlyProtocolSession host, long start, int count, bool allowed) =>
        Assert(host.LastQuota == new ProtocolQuotaWindow(start, count, allowed),
            "Actual quota window/count/allowance did not match its body-reading boundary.");
    private static void NoQuota(ReadOnlyProtocolSession host) => Assert(host.LastQuota is null
        && host.LastObservation == new ProtocolObservation(ProtocolReceiveKind.Ignored, false),
        "Filtering retained the previous receive's quota or read metadata.");
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
