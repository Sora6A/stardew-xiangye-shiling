using XiangyeShiling.Networking;

/// <summary>验证实际诊断缓冲区和纯会话观察，不把内存消息配对当作 SMAPI 或双客户端实测。</summary>
internal static class DiagnosticScenarios
{
    public static void Run(Action<string, Action> test)
    {
        test("diagnostic snapshots detach containers and retain immutable historical observations", () =>
        {
            var buffer = new ProtocolTraceBuffer();
            var observation = new ProtocolObservation(ProtocolReceiveKind.RejectionAccepted, true,
                Guid.NewGuid(), ProtocolRejection.WritesDisabled);
            buffer.Record(50, 2, 3, ProtocolTraceKind.Processed, 10, ReadOnlyProtocolCodec.RejectedType, observation);
            var first = buffer.Snapshot();
            ProtocolTrace original = first.Single();
            Assert(original.Sequence == 1 && original.Milliseconds == 50 && original.WorldGeneration == 2
                && original.DisconnectGeneration == 3 && original.ThreadId == Environment.CurrentManagedThreadId
                && original.PeerId == 10 && original.MessageType == ReadOnlyProtocolCodec.RejectedType
                && original.Observation == observation, "Trace lost its source, owner, or observation metadata.");
            if (first is IList<ProtocolTrace> mutable)
            {
                try { mutable[0] = original with { Kind = ProtocolTraceKind.Faulted, Observation = observation with { BodyReadAttempted = false } }; }
                catch (NotSupportedException) { /* A read-only container is also a valid detached snapshot. */ }
            }
            var preserved = buffer.Snapshot();
            Assert(preserved.Count == 1 && ReferenceEquals(preserved[0], original) && preserved[0].Observation!.BodyReadAttempted,
                "Mutating a returned snapshot altered the actual diagnostic buffer.");
            buffer.Record(51, 2, 3, ProtocolTraceKind.SendAttempt, 10, ReadOnlyProtocolCodec.RequestType);
            Assert(preserved.Count == 1 && preserved[0] == original && buffer.Snapshot().Count == 2,
                "Later records rewrote an earlier snapshot.");
        });

        test("diagnostic tail bounds memory and reports every evicted sequence", () =>
        {
            Expect<ArgumentOutOfRangeException>(() => new ProtocolTraceBuffer(0));
            Expect<ArgumentOutOfRangeException>(() => new ProtocolTraceBuffer(-1));
            var buffer = new ProtocolTraceBuffer(2);
            Assert(buffer.Snapshot().Count == 0 && buffer.LastSequence == 0 && buffer.Evicted == 0, "Empty diagnostic counters were invented.");
            buffer.Record(0, 1, 0, ProtocolTraceKind.Reset);
            buffer.Record(1, 1, 0, ProtocolTraceKind.SendAttempt);
            var oldTail = buffer.Snapshot();
            Assert(buffer.LastSequence == 2 && buffer.Evicted == 0 && oldTail.Select(t => t.Sequence).SequenceEqual(new long[] { 1, 2 }),
                "A full tail evicted before exceeding capacity.");
            buffer.Record(2, 1, 0, ProtocolTraceKind.Processed);
            buffer.Record(3, 1, 0, ProtocolTraceKind.StaleQueue);
            buffer.Record(4, 2, 1, ProtocolTraceKind.Reset);
            var current = buffer.Snapshot();
            Assert(current.Count == 2 && current.Select(t => t.Sequence).SequenceEqual(new long[] { 4, 5 })
                && buffer.LastSequence == 5 && buffer.Evicted == 3, "Tail overflow was hidden or sequence IDs were reused.");
            Assert(oldTail.Select(t => t.Sequence).SequenceEqual(new long[] { 1, 2 }), "Eviction mutated an existing snapshot.");
        });

        test("diagnostic owner violations cannot alter traces or session observations", () =>
        {
            var buffer = new ProtocolTraceBuffer();
            buffer.Record(0, 1, 0, ProtocolTraceKind.Reset);
            var host = new ReadOnlyProtocolSession(10, true, ModId, Guid.NewGuid());
            host.SynchronizePeers(new[] { new ProtocolPeer(20, false, true, false, Guid.NewGuid()) }, 0);
            host.Receive(20, ModId, ReadOnlyProtocolCodec.HelloType,
                () => ReadOnlyProtocolCodec.Write(new ProtocolHello(1, Guid.NewGuid(), 20)), 0);
            ProtocolObservation original = host.LastObservation;
            var failures = new List<Exception>();
            var thread = new Thread(() =>
            {
                foreach (Action action in new Action[] {
                    () => buffer.Record(1, 1, 0, ProtocolTraceKind.Processed), () => buffer.Snapshot(),
                    () => { _ = buffer.LastSequence; }, () => { _ = buffer.Evicted; },
                    () => host.ReadPeerStatus(), () => { _ = host.ActiveSessionId; },
                    () => host.Receive(20, ModId, ReadOnlyProtocolCodec.RequestType,
                        () => throw new Exception("Foreign-thread body must not be read."), 1)
                })
                {
                    try { action(); failures.Add(new Exception("A foreign diagnostic operation was accepted.")); }
                    catch (InvalidOperationException) { }
                    catch (Exception ex) { failures.Add(ex); }
                }
            });
            thread.Start(); thread.Join();
            Assert(failures.Count == 0 && buffer.LastSequence == 1 && buffer.Evicted == 0 && buffer.Snapshot().Count == 1,
                "Foreign access changed the diagnostic buffer or returned the wrong failure.");
            Assert(ReferenceEquals(host.LastObservation, original) && original.Kind == ProtocolReceiveKind.HelloAccepted,
                "Foreign receive replaced a valid owner-thread observation.");
        });

        test("session diagnostics bind actual body reads rejection metadata and quota skips", () =>
        {
            const long hostId = 10, actorId = 20;
            Guid session = Guid.NewGuid(); int hostReads = 0, clientReads = 0;
            var host = new ReadOnlyProtocolSession(hostId, true, ModId, session);
            var client = new ReadOnlyProtocolSession(actorId, false, ModId, Guid.NewGuid());
            host.SynchronizePeers(new[] { new ProtocolPeer(actorId, false, true, false, Guid.NewGuid()) }, 0);
            var hello = client.SynchronizePeers(new[] { new ProtocolPeer(hostId, true, true, false, Guid.NewGuid()) }, 0).Single();
            Assert(ReadOnlyProtocolCodec.TryReadHello(hello.Json, out var helloValue), "Pure client did not produce a valid hello.");
            foreach ((long actor, string mod, string type) in new[] {
                (30L, ModId, ReadOnlyProtocolCodec.RequestType), (actorId, "another.mod", ReadOnlyProtocolCodec.RequestType),
                (actorId, ModId, "unknown.type"), (actorId, ModId, ReadOnlyProtocolCodec.WelcomeType)
            })
            {
                Assert(host.Receive(actor, mod, type, () => { hostReads++; return "{invalid"; }, 0) is null,
                    "Foreign, unknown, or reversed-role traffic produced a response.");
                Observe(host, ProtocolReceiveKind.Ignored, false);
            }
            Assert(hostReads == 0, "Filtering read the body or invented a read observation.");
            var welcome = host.Receive(actorId, ModId, hello.MessageType, () => { hostReads++; return hello.Json; }, 0);
            Observe(host, ProtocolReceiveKind.HelloAccepted, true, challenge: helloValue!.Challenge);
            Assert(welcome is not null, "Pure host omitted the welcome.");
            client.Receive(hostId, ModId, welcome!.MessageType, () => { clientReads++; return welcome.Json; }, 0);
            Observe(client, ProtocolReceiveKind.WelcomeAccepted, true, challenge: helloValue.Challenge);
            Assert(client.ActiveSessionId == session && hostReads == 1 && clientReads == 1, "Handshake observation did not match actual reads.");

            foreach (ProtocolProbeKind kind in Enum.GetValues<ProtocolProbeKind>())
            {
                Guid id = Guid.NewGuid();
                string json = ProtocolProbePlan.Create(kind, session, id, actorId);
                Guid? correlation = kind == ProtocolProbeKind.Malformed ? null : id;
                var response = host.Receive(actorId, ModId, ReadOnlyProtocolCodec.RequestType,
                    () => { hostReads++; return json; }, 0);
                Observe(host, ProtocolReceiveKind.RejectionProduced, true, correlation, ProtocolProbePlan.ExpectedReason(kind));
                Assert(response is not null && ReadOnlyProtocolCodec.TryReadRejected(response.Json, out var rejected)
                    && rejected!.ActorId == actorId && rejected.SessionId == session && rejected.RequestId == correlation,
                    "Host observation was disconnected from its actual rejection message.");
                client.Receive(hostId, ModId, response!.MessageType, () => { clientReads++; return response.Json; }, 0);
                Observe(client, ProtocolReceiveKind.RejectionAccepted, true, correlation, ProtocolProbePlan.ExpectedReason(kind));
            }
            Assert(hostReads == 6 && clientReads == 6, "Probe observations did not reflect one real body read per pure receive.");
            Assert(host.Receive(actorId, ModId, ReadOnlyProtocolCodec.HelloType,
                () => { hostReads++; return ReadOnlyProtocolCodec.Write(new ProtocolHello(1, Guid.NewGuid(), 30)); }, 0) is null,
                "A mismatched hello was accepted.");
            Observe(host, ProtocolReceiveKind.InvalidMessage, true);

            Guid finalId = Guid.NewGuid(); string final = ProtocolProbePlan.Create(ProtocolProbeKind.Start, session, finalId, actorId);
            Assert(host.Receive(actorId, ModId, ReadOnlyProtocolCodec.RequestType, () => { hostReads++; return final; }, 0) is not null,
                "The eighth allowed body was not consumed.");
            Observe(host, ProtocolReceiveKind.RejectionProduced, true, finalId, ProtocolRejection.WritesDisabled);
            Assert(hostReads == ReadOnlyProtocolSession.MessagesPerWindow, "Quota setup did not use exactly eight reads.");
            Assert(host.Receive(actorId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => { hostReads++; throw new Exception("An over-quota body must not be read."); }, 0) is null,
                "Over-quota receive produced a response.");
            Observe(host, ProtocolReceiveKind.RateLimited, false);
            Assert(hostReads == 8, "Quota skip read the body or retained an old correlation.");
            host.Receive(actorId, "another.mod", ReadOnlyProtocolCodec.RequestType,
                () => { hostReads++; return final; }, 0);
            Observe(host, ProtocolReceiveKind.Ignored, false);
            Assert(hostReads == 8, "Foreign traffic after quota reported a stale read.");

            Assert(host.Receive(actorId, ModId, ReadOnlyProtocolCodec.RequestType,
                () => { hostReads++; return final; }, 1000) is not null, "A new quota window remained closed.");
            Observe(host, ProtocolReceiveKind.RejectionProduced, true, finalId, ProtocolRejection.WritesDisabled);
            host.Receive(actorId, ModId, ReadOnlyProtocolCodec.RequestType, () => { hostReads++; return final; }, 999);
            Observe(host, ProtocolReceiveKind.Ignored, false);
            host.SynchronizePeers(Array.Empty<ProtocolPeer>(), 1001);
            host.Receive(actorId, ModId, ReadOnlyProtocolCodec.RequestType, () => { hostReads++; return final; }, 1001);
            Observe(host, ProtocolReceiveKind.Ignored, false);
            Assert(hostReads == 9, "Regressed clock or disconnected peer consumed a body.");
        });
    }

    private const string ModId = "Sora6A.XiangyeShiling";
    private static void Observe(ReadOnlyProtocolSession session, ProtocolReceiveKind kind, bool read,
        Guid? requestId = null, ProtocolRejection? reason = null, Guid? challenge = null) =>
        Assert(session.LastObservation == new ProtocolObservation(kind, read, requestId, reason, challenge),
            "Session observation has stale or incorrect kind/read/correlation/reason/challenge metadata.");
    private static void Expect<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException("Expected diagnostic failure: " + typeof(TException).Name);
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
