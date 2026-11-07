using XiangyeShiling.Networking;

internal static class ReadOnlyCodecScenarios
{
    public static void Run(Action<string, Action> test)
    {
        var hello = new ProtocolHello(1, Guid.NewGuid(), 20);
        var welcome = new ProtocolWelcome(1, 1, hello.Challenge, Guid.NewGuid(), 10, false);
        var rejected = new ProtocolRejected(1, welcome.SessionId, Guid.NewGuid(), 20, ProtocolRejection.WritesDisabled);
        string h = ReadOnlyProtocolCodec.Write(hello), w = ReadOnlyProtocolCodec.Write(welcome), r = ReadOnlyProtocolCodec.Write(rejected);
        static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        test("read-only codecs roundtrip all messages and nullable diagnostic request id", () =>
        {
            Assert(ReadOnlyProtocolCodec.TryReadHello(h, out var a) && a == hello, "Hello changed.");
            Assert(ReadOnlyProtocolCodec.TryReadWelcome(w, out var b) && b == welcome, "Welcome changed.");
            Assert(ReadOnlyProtocolCodec.TryReadRejected(r, out var c) && c == rejected, "Rejection changed.");
            var invalid = rejected with { RequestId = null, Reason = ProtocolRejection.InvalidRequest };
            Assert(ReadOnlyProtocolCodec.TryReadRejected(ReadOnlyProtocolCodec.Write(invalid), out c) && c == invalid,
                "Malformed request diagnostics require an explicit null request id.");
        });
        test("read-only codecs reject unknown duplicate missing fields and wrong primitive types", () =>
        {
            foreach (string bad in new[] { "null", "[]", "{", h.Replace("\"ActorId\":20", "\"ActorId\":\"20\""),
                h.Replace("\"ProtocolVersion\":1", "\"ProtocolVersion\":2"), h.Replace("\"ActorId\":20", "\"ActorId\":20,\"Inventory\":[]"),
                h.Replace("\"ActorId\":20", "\"ActorId\":20,\"ActorId\":20"), h.Replace("\"ProtocolVersion\":1,", "") })
                Assert(!ReadOnlyProtocolCodec.TryReadHello(bad, out var value) && value is null, "Ambiguous hello accepted.");
            Assert(!ReadOnlyProtocolCodec.TryReadWelcome(w.Replace("false", "\"false\""), out _), "String boolean accepted.");
            Assert(!ReadOnlyProtocolCodec.TryReadRejected(r.Replace("\"WritesDisabled\"", "0"), out _)
                && !ReadOnlyProtocolCodec.TryReadRejected(r.Replace("WritesDisabled", "writesdisabled"), out _), "Loose enum accepted.");
        });
        test("read-only welcome cannot enable writes or change protocol versions", () =>
        {
            Assert(!ReadOnlyProtocolCodec.TryReadWelcome(w.Replace("false", "true"), out _), "Remote enabled writes.");
            Assert(!ReadOnlyProtocolCodec.TryReadWelcome(w.Replace("\"RequestProtocolVersion\":1", "\"RequestProtocolVersion\":2"), out _),
                "Unknown machine protocol accepted.");
            bool threw = false;
            try { ReadOnlyProtocolCodec.Write(welcome with { WritesEnabled = true }); } catch (ArgumentException) { threw = true; }
            Assert(threw, "Writer emitted an enabled message.");
        });
        test("read-only codecs preserve strict guid and unicode rejection without throwing", () =>
        {
            foreach (string bad in new[] { h.Replace(hello.Challenge.ToString(), hello.Challenge.ToString("N")),
                h.Replace(hello.Challenge.ToString(), "\\uD800"), h.Replace("\"Challenge\"", "\"\\uDC00\""), h + "\uD800",
                h.Replace(hello.Challenge.ToString(), Guid.Empty.ToString()) })
                Assert(!ReadOnlyProtocolCodec.TryReadHello(bad, out var value) && value is null, "Malformed hello escaped boundary.");
            Assert(!ReadOnlyProtocolCodec.TryReadWelcome(w.Replace(welcome.SessionId.ToString(), "\\uD800"), out _), "Malformed welcome accepted.");
            Assert(!ReadOnlyProtocolCodec.TryReadRejected(r.Replace("WritesDisabled", "\\uDC00"), out _), "Malformed reason accepted.");
        });
        test("read-only codecs enforce wire bound and nonzero identities", () =>
        {
            foreach (string tooLarge in new[] { new string(' ', 4097), "{\"" + new string('豆', 1400) + "\":0}" })
                Assert(!ReadOnlyProtocolCodec.TryReadHello(tooLarge, out _)
                    && !ReadOnlyProtocolCodec.TryReadWelcome(tooLarge, out _)
                    && !ReadOnlyProtocolCodec.TryReadRejected(tooLarge, out _), "Wire limit ignored.");
            Assert(!ReadOnlyProtocolCodec.TryReadHello(h.Replace("\"ActorId\":20", "\"ActorId\":0"), out _), "Zero actor accepted.");
            Assert(!ReadOnlyProtocolCodec.TryReadRejected(r.Replace(rejected.RequestId!.Value.ToString(), Guid.Empty.ToString()), out _),
                "Empty request id accepted instead of null.");
        });
    }
}
