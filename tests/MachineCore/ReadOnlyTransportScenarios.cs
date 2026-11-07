using XiangyeShiling.Networking;

internal static class ReadOnlyTransportScenarios
{
    private const long HostId = 10;
    private const long ClientId = 20;
    private const string ModId = "Sora6A.XiangyeShiling";
    private static readonly ProtocolPeer HostPeer = new(HostId, true, true, false);
    private static readonly ProtocolPeer ClientPeer = new(ClientId, false, true, false);
    private sealed record Pair(ReadOnlyProtocolSession Host, ReadOnlyProtocolSession Client,
        Guid SessionId, ProtocolHello Hello, OutboundProtocolMessage Welcome);

    public static void Run(Action<string, Action> test)
    {
        test("read-only paired handshake rejects valid start and collect with writes disabled", () =>
        {
            Pair pair = Connect();
            foreach (MachineRequestAction action in new[] { MachineRequestAction.Start, MachineRequestAction.Collect })
            {
                var request = Request(pair.SessionId, action);
                var response = pair.Host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                    () => MachineRequestCodec.Write(request), 0);
                ProtocolRejected rejected = Rejected(response, ProtocolRejection.WritesDisabled, request.RequestId,
                    pair.SessionId);
                Assert(pair.Client.Receive(HostId, ModId, response!.MessageType, () => response.Json, 0) is null
                    && pair.Client.LastRejection == rejected, "Paired rejection was not accepted as a diagnostic.");
            }
        });
        test("read-only host rejects requests before handshake and rejects actor session or malformed data", () =>
        {
            Guid session = Guid.NewGuid();
            var host = Host(session);
            host.SynchronizePeers(new[] { ClientPeer }, 0);
            var request = Request(session);
            OutboundProtocolMessage? Send(string? json) => host.Receive(ClientId, ModId,
                ReadOnlyProtocolCodec.RequestType, () => json, 0);
            Rejected(Send(MachineRequestCodec.Write(request)), ProtocolRejection.HandshakeRequired, request.RequestId, session);
            Rejected(Send(MachineRequestCodec.Write(request with { ActorId = 30 })), ProtocolRejection.ActorMismatch,
                request.RequestId, session);
            Rejected(Send(MachineRequestCodec.Write(request with { SessionId = Guid.NewGuid() })),
                ProtocolRejection.WrongSession, request.RequestId, session);
            foreach (string? json in new string?[] { null, "{" })
                Rejected(Send(json), ProtocolRejection.InvalidRequest, null, session);
            foreach (string? json in new string?[] { null, ReadOnlyProtocolCodec.Write(new ProtocolHello(1, Guid.NewGuid(), 30)) })
                Assert(host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.HelloType, () => json, 0) is null,
                    "Malformed or mismatched hello produced a welcome.");
            Rejected(Send(MachineRequestCodec.Write(request)), ProtocolRejection.HandshakeRequired, request.RequestId, session);
        });
        test("read-only routing ignores unknown mod source type self and ineligible peers before body read", () =>
        {
            var host = Host(Guid.NewGuid());
            host.SynchronizePeers(new[] { ClientPeer, new ProtocolPeer(21, false, false, false),
                new ProtocolPeer(22, false, true, true), new ProtocolPeer(23, true, true, false),
                new ProtocolPeer(HostId, false, true, false) }, 0);
            int reads = 0;
            foreach (var route in new[] {
                (30L, ModId, ReadOnlyProtocolCodec.RequestType),
                (HostId, ModId, ReadOnlyProtocolCodec.RequestType),
                (ClientId, "another.mod", ReadOnlyProtocolCodec.RequestType),
                (ClientId, ModId, "unknown/type"),
                (ClientId, ModId, ReadOnlyProtocolCodec.WelcomeType),
                (21L, ModId, ReadOnlyProtocolCodec.RequestType),
                (22L, ModId, ReadOnlyProtocolCodec.RequestType),
                (23L, ModId, ReadOnlyProtocolCodec.RequestType) })
                Assert(host.Receive(route.Item1, route.Item2, route.Item3, () => { reads++; return null; }, 0) is null,
                    "Ignored route emitted a response.");
            Pair pair = Connect();
            Assert(pair.Client.Receive(HostId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => { reads++; return null; }, 0) is null && reads == 0,
                "Ignored or wrong-role route invoked readBody.");
        });
        test("read-only per-peer quota precedes body read and does not reset on clock rollback", () =>
        {
            Guid session = Guid.NewGuid();
            var host = Host(session);
            host.SynchronizePeers(new[] { ClientPeer, new ProtocolPeer(21, false, true, false) }, 0);
            int reads = 0;
            OutboundProtocolMessage? Send(long peer, long now) => host.Receive(peer, ModId,
                ReadOnlyProtocolCodec.RequestType, () => { reads++; return null; }, now);
            for (int i = 0; i < ReadOnlyProtocolSession.MessagesPerWindow; i++)
                Rejected(Send(ClientId, 0), ProtocolRejection.InvalidRequest, null, session);
            Assert(reads == 8 && Send(21, 0) is not null && reads == 9, "Peer quotas were combined.");
            Assert(Send(ClientId, 900) is null && Send(ClientId, 800) is null
                && Send(ClientId, 900) is null && Send(ClientId, -1) is null && reads == 9,
                "Overflow or backwards clock read a body, emitted a response, or renewed quota.");
            Rejected(Send(ClientId, 1000), ProtocolRejection.InvalidRequest, null, session);
            for (int i = 1; i < ReadOnlyProtocolSession.MessagesPerWindow; i++)
                Assert(Send(ClientId, 1000) is not null, "Advancing window did not restore its bounded allowance.");
            Assert(reads == 17 && Send(ClientId, 1000) is null && reads == 17,
                "The renewed window exceeded eight reads or responded to overflow.");
        });
        test("read-only client welcome binds challenge host version and disabled writes", () =>
        {
            var client = Client();
            ProtocolHello hello = Hello(client.SynchronizePeers(new[] { HostPeer }, 0).Single());
            Guid session = Guid.NewGuid();
            var welcome = new ProtocolWelcome(1, 1, hello.Challenge, session, HostId, false);
            string json = ReadOnlyProtocolCodec.Write(welcome);
            foreach (string invalid in new[] { ReadOnlyProtocolCodec.Write(welcome with { Challenge = Guid.NewGuid() }),
                ReadOnlyProtocolCodec.Write(welcome with { HostPlayerId = 11 }),
                json.Replace("\"WritesEnabled\":false", "\"WritesEnabled\":true"),
                json.Replace("\"RequestProtocolVersion\":1", "\"RequestProtocolVersion\":2"),
                json.Replace("\"Challenge\"", "\"\\uD800\"") })
            {
                Assert(invalid != json, "Invalid welcome fixture did not change the original.");
                Assert(client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType, () => invalid, 0) is null
                    && client.RemoteSessionId is null, "Invalid welcome established a session.");
            }
            client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType, () => json, 0);
            Assert(client.RemoteSessionId == session, "Matching welcome did not establish a read-only session.");
        });
        test("read-only client accepts rejections only after handshake for its bound actor and session", () =>
        {
            var client = Client();
            ProtocolHello hello = Hello(client.SynchronizePeers(new[] { HostPeer }, 0).Single());
            Guid session = Guid.NewGuid();
            var rejected = new ProtocolRejected(1, session, Guid.NewGuid(), ClientId, ProtocolRejection.WritesDisabled);
            void Receive(string? json) => client.Receive(HostId, ModId, ReadOnlyProtocolCodec.RejectedType, () => json, 0);
            Receive(ReadOnlyProtocolCodec.Write(rejected));
            Assert(client.LastRejection is null, "Unhandshaken rejection changed client diagnostic state.");
            client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType,
                () => ReadOnlyProtocolCodec.Write(new ProtocolWelcome(1, 1, hello.Challenge, session, HostId, false)), 0);
            foreach (string? invalid in new string?[] { null,
                ReadOnlyProtocolCodec.Write(rejected with { ActorId = 30 }),
                ReadOnlyProtocolCodec.Write(rejected with { SessionId = Guid.NewGuid() }) })
            {
                Receive(invalid);
                Assert(client.LastRejection is null, "Malformed or unbound rejection was accepted.");
            }
            Receive(ReadOnlyProtocolCodec.Write(rejected));
            Assert(client.LastRejection == rejected, "Bound rejection was ignored.");
        });
        test("read-only duplicate welcome cannot replace established session or clear rejection", () =>
        {
            Pair pair = Connect();
            var rejected = new ProtocolRejected(1, pair.SessionId, Guid.NewGuid(), ClientId, ProtocolRejection.WritesDisabled);
            pair.Client.Receive(HostId, ModId, ReadOnlyProtocolCodec.RejectedType,
                () => ReadOnlyProtocolCodec.Write(rejected), 0);
            string duplicate = ReadOnlyProtocolCodec.Write(new ProtocolWelcome(1, 1, pair.Hello.Challenge,
                Guid.NewGuid(), HostId, false));
            pair.Client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType, () => duplicate, 0);
            Assert(pair.Client.RemoteSessionId == pair.SessionId && pair.Client.LastRejection == rejected,
                "Duplicate welcome changed established session/diagnostics.");
        });
        test("read-only reconnect uses fresh challenge and rejects old connection welcome", () =>
        {
            Pair pair = Connect();
            pair.Client.Receive(HostId, ModId, ReadOnlyProtocolCodec.RejectedType,
                () => ReadOnlyProtocolCodec.Write(new ProtocolRejected(1, pair.SessionId, null,
                    ClientId, ProtocolRejection.InvalidRequest)), 0);
            pair.Client.SynchronizePeers(Array.Empty<ProtocolPeer>(), 10);
            pair.Host.SynchronizePeers(Array.Empty<ProtocolPeer>(), 10);
            Assert(pair.Client.RemoteSessionId is null && pair.Client.LastRejection is null,
                "Disconnect retained the old session or diagnostics.");
            pair.Host.SynchronizePeers(new[] { ClientPeer }, 11);
            OutboundProtocolMessage hello = pair.Client.SynchronizePeers(new[] { HostPeer }, 11).Single();
            Assert(Hello(hello).Challenge != pair.Hello.Challenge, "Reconnect reused an old challenge.");
            pair.Client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType, () => pair.Welcome.Json, 11);
            Assert(pair.Client.RemoteSessionId is null, "Old connection welcome established the reconnected session.");
            var request = Request(pair.SessionId);
            Rejected(pair.Host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => MachineRequestCodec.Write(request), 11), ProtocolRejection.HandshakeRequired, request.RequestId, pair.SessionId);
            OutboundProtocolMessage? welcome = pair.Host.Receive(ClientId, ModId, hello.MessageType, () => hello.Json, 11);
            Assert(welcome is not null, "Reconnect hello was rejected.");
            pair.Client.Receive(HostId, ModId, welcome!.MessageType, () => welcome.Json, 11);
            Assert(pair.Client.RemoteSessionId == pair.SessionId, "Fresh reconnect handshake failed.");
        });
        test("read-only pending hello retries preserve challenge and stop after matching welcome", () =>
        {
            var client = Client();
            OutboundProtocolMessage first = client.SynchronizePeers(new[] { HostPeer }, 0).Single();
            Assert(client.SynchronizePeers(new[] { HostPeer }, 4999).Count == 0, "Hello retried before five seconds.");
            OutboundProtocolMessage retry = client.SynchronizePeers(new[] { HostPeer }, 5000).Single();
            Assert(Hello(first) == Hello(retry), "Retry replaced the pending challenge.");
            Assert(client.SynchronizePeers(new[] { HostPeer }, 4000).Count == 0
                && client.SynchronizePeers(new[] { HostPeer }, 5000).Count == 0, "Clock rollback renewed a hello retry.");
            Guid session = Guid.NewGuid();
            client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType,
                () => ReadOnlyProtocolCodec.Write(new ProtocolWelcome(1, 1, Hello(first).Challenge, session, HostId, false)), 5000);
            Assert(client.RemoteSessionId == session && client.SynchronizePeers(new[] { HostPeer }, 10000).Count == 0,
                "A delayed matching reply failed or ready client kept retrying.");
        });
        test("read-only changed connection invalidates handshake while other peers retain host world session", () =>
        {
            Pair pair = Connect();
            var other = new ProtocolPeer(21, false, true, false);
            pair.Host.SynchronizePeers(new[] { ClientPeer, other }, 0);
            Assert(pair.Host.Receive(21, ModId, ReadOnlyProtocolCodec.HelloType,
                () => ReadOnlyProtocolCodec.Write(new ProtocolHello(1, Guid.NewGuid(), 21)), 0) is not null,
                "Other peer failed to handshake.");
            // Same IDs/roles/version, with no intervening empty connection snapshot.
            var restarted = pair.Client.SynchronizePeers(new[] {
                HostPeer with { ConnectionId = Guid.NewGuid() } }, 1);
            Assert(restarted.Count == 1, "BUG-2026-014: changed connection retained Ready instead of issuing a fresh challenge.");
            OutboundProtocolMessage hello = restarted.Single();
            pair.Host.SynchronizePeers(new[] { ClientPeer with { ConnectionId = Guid.NewGuid() }, other }, 1);
            Assert(pair.Client.RemoteSessionId is null && Hello(hello).Challenge != pair.Hello.Challenge,
                "Changed connection reused old client handshake/challenge.");
            var request = Request(pair.SessionId);
            Rejected(pair.Host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => MachineRequestCodec.Write(request), 1), ProtocolRejection.HandshakeRequired, request.RequestId, pair.SessionId);
            var otherRequest = Request(pair.SessionId) with { ActorId = 21 };
            Rejected(pair.Host.Receive(21, ModId, ReadOnlyProtocolCodec.RequestType,
                () => MachineRequestCodec.Write(otherRequest), 1), ProtocolRejection.WritesDisabled,
                otherRequest.RequestId, pair.SessionId, 21);
            pair.Client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType, () => pair.Welcome.Json, 1);
            Assert(pair.Client.RemoteSessionId is null, "Old welcome crossed an unobserved reconnect.");
            OutboundProtocolMessage? welcome = pair.Host.Receive(ClientId, ModId, hello.MessageType, () => hello.Json, 1);
            Assert(welcome is not null, "New connection hello failed.");
            pair.Client.Receive(HostId, ModId, welcome!.MessageType, () => welcome.Json, 1);
            Assert(pair.Client.RemoteSessionId == pair.SessionId, "Reconnect changed the shared host world session.");
        });
        test("read-only session rejects foreign-thread peer synchronization reception and count", () =>
        {
            var host = Host(Guid.NewGuid());
            host.SynchronizePeers(new[] { ClientPeer }, 0);
            int reads = 0;
            Task.Run(() =>
            {
                Expect<InvalidOperationException>(() => { _ = host.PeerCount; }, "Foreign count was allowed.");
                Expect<InvalidOperationException>(() => host.SynchronizePeers(Array.Empty<ProtocolPeer>(), 0),
                    "Foreign synchronization was allowed.");
                Expect<InvalidOperationException>(() => host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                    () => { reads++; return null; }, 0), "Foreign reception was allowed.");
            }).GetAwaiter().GetResult();
            Assert(host.PeerCount == 1 && reads == 0, "Rejected foreign access changed peers or read a body.");
        });
        test("read-only peer capacity duplicates ambiguous hosts and changed metadata fail closed", () =>
        {
            var host = Host(Guid.NewGuid());
            var peers = Enumerable.Range(20, 40).Select(id => new ProtocolPeer(id, false, true, false))
                .Append(new ProtocolPeer(31, false, true, false)).ToArray();
            host.SynchronizePeers(peers, 0);
            Assert(host.PeerCount == ReadOnlyProtocolSession.MaxPeers, "Host peer capacity was not bounded.");
            int reads = 0;
            foreach (long id in new long[] { 31, 59 })
                Assert(host.Receive(id, ModId, ReadOnlyProtocolCodec.RequestType, () => { reads++; return null; }, 0) is null,
                    "Duplicate or beyond-capacity peer produced a response.");
            Assert(reads == 0 && host.Receive(ClientId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => { reads++; return null; }, 0) is not null && reads == 1, "Capacity routing read ignored sources.");
            var client = Client();
            Assert(client.SynchronizePeers(new[] { HostPeer, new ProtocolPeer(11, true, true, false) }, 0).Count == 0
                && client.PeerCount == 0, "Ambiguous hosts established a pending peer.");
            Assert(client.SynchronizePeers(new[] { HostPeer, HostPeer }, 0).Count == 0 && client.PeerCount == 0,
                "Duplicate host metadata was accepted.");
            ProtocolHello hello = Hello(client.SynchronizePeers(new[] { HostPeer }, 0).Single());
            client.Receive(HostId, ModId, ReadOnlyProtocolCodec.WelcomeType,
                () => ReadOnlyProtocolCodec.Write(new ProtocolWelcome(1, 1, hello.Challenge, Guid.NewGuid(), HostId, false)), 0);
            Assert(client.RemoteSessionId is not null, "Single eligible host failed to connect.");
            client.SynchronizePeers(new[] { HostPeer with { HasMatchingMod = false } }, 0);
            Assert(client.PeerCount == 0 && client.RemoteSessionId is null, "Changed mod metadata retained an old session.");
            Assert(client.SynchronizePeers(new[] { HostPeer with { IsSplitScreen = true } }, 0).Count == 0,
                "Split-screen host emitted hello.");
        });
    }

    private static ReadOnlyProtocolSession Host(Guid session) => new(HostId, true, ModId, session);
    private static ReadOnlyProtocolSession Client() => new(ClientId, false, ModId, Guid.NewGuid());
    private static MachineRequestEnvelope Request(Guid session, MachineRequestAction action = MachineRequestAction.Start) =>
        new(1, session, Guid.NewGuid(), ClientId, "FarmHouse", 8, 7, Guid.NewGuid(), 0,
            action, action == MachineRequestAction.Start ? "tofu" : null);

    // Pair the real hello/welcome messages; never set Ready/RemoteSessionId through a test-only shortcut.
    private static Pair Connect()
    {
        Guid session = Guid.NewGuid();
        var host = Host(session);
        var client = Client();
        Assert(host.SynchronizePeers(new[] { ClientPeer }, 0).Count == 0, "Host initiated a client hello.");
        OutboundProtocolMessage hello = client.SynchronizePeers(new[] { HostPeer }, 0).Single();
        Assert(hello.PlayerId == HostId && hello.MessageType == ReadOnlyProtocolCodec.HelloType, "Hello route changed.");
        OutboundProtocolMessage? welcome = host.Receive(ClientId, ModId, hello.MessageType, () => hello.Json, 0);
        Assert(welcome is not null && welcome.PlayerId == ClientId && welcome.MessageType == ReadOnlyProtocolCodec.WelcomeType
            && ReadOnlyProtocolCodec.TryReadWelcome(welcome.Json, out var decoded) && !decoded!.WritesEnabled,
            "Host failed to return a bound read-only welcome.");
        client.Receive(HostId, ModId, welcome!.MessageType, () => welcome.Json, 0);
        Assert(client.RemoteSessionId == session, "Genuine paired handshake did not bind the host session.");
        return new(host, client, session, Hello(hello), welcome);
    }

    private static ProtocolHello Hello(OutboundProtocolMessage message)
    {
        Assert(ReadOnlyProtocolCodec.TryReadHello(message.Json, out var hello), "Hello was not valid strict protocol JSON.");
        return hello!;
    }

    private static ProtocolRejected Rejected(OutboundProtocolMessage? message, ProtocolRejection reason,
        Guid? requestId, Guid session, long actorId = ClientId)
    {
        Assert(message is not null && message.PlayerId == actorId && message.MessageType == ReadOnlyProtocolCodec.RejectedType,
            "Host omitted or misrouted its rejection.");
        Assert(ReadOnlyProtocolCodec.TryReadRejected(message!.Json, out var rejected)
            && rejected!.Reason == reason && rejected.RequestId == requestId && rejected.ActorId == actorId
            && rejected.SessionId == session, "Host rejection changed reason, correlation or session binding.");
        return rejected!;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Expect<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }
}
