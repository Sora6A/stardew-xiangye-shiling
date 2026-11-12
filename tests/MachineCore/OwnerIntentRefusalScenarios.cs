using XiangyeShiling.Networking;

internal static class OwnerIntentRefusalScenarios
{
    internal static void Run(Action<string, Action> test)
    {
        const ulong world = 7;
        const long host = -12;
        var nonce = Guid.Parse("a6438e78-abab-401d-b00a-1a60d82a83aa");
        var request = new MachineRequestEnvelope(1, Guid.NewGuid(), Guid.NewGuid(), 10, "Farm|Shed", 4, 7,
            Guid.NewGuid(), 2, MachineRequestAction.Start, "bean-workshop");
        string raw = MachineRequestCodec.Write(request);

        test("owner intent wrapper retains the exact original request bytes", () =>
        {
            string exact = " " + raw + "\n";
            string wire = OwnerIntentRefusal.WriteIntent(nonce, exact);
            Check(OwnerIntentRefusal.TryReadIntent(wire, out Guid actualNonce, out string? actualRaw)
                && actualNonce == nonce && actualRaw == exact, "The nonce wrapper altered the request body.");
            Check(!OwnerIntentRefusal.TryReadIntent(raw, out _, out _)
                && !OwnerIntentRefusal.TryReadIntent(wire.Replace(nonce.ToString("D"), nonce.ToString("D").ToUpperInvariant()), out _, out _)
                && !OwnerIntentRefusal.TryReadIntent($"1|{Guid.Empty:D}|{raw}", out _, out _)
                && !OwnerIntentRefusal.TryReadIntent($"1|{nonce:D}|{new string('禾', 1400)}", out _, out _),
                "An unwrapped, noncanonical, empty-nonce or oversized wire request was accepted.");
        });

        test("native no-grant marker binds host world actor session nonce and exact intent", () =>
        {
            var refusal = Create();
            Check(OwnerIntentRefusal.TryRead(refusal.Encode(), out var decoded) && decoded == refusal
                && decoded.Matches(world, host, nonce, request, raw), "The exact refusal did not survive encoding.");
            Check(!refusal.Matches(world + 1, host, nonce, request, raw)
                && !refusal.Matches(world, host - 1, nonce, request, raw)
                && !refusal.Matches(world, host, Guid.NewGuid(), request, raw)
                && !refusal.Matches(world, host, nonce, request with { ActorId = 11 }, raw)
                && !refusal.Matches(world, host, nonce, request with { SessionId = Guid.NewGuid() }, raw)
                && !refusal.Matches(world, host, nonce, request with { RequestId = Guid.NewGuid() }, raw)
                && !refusal.Matches(world, host, nonce, request with { TileX = 5 }, raw)
                && !refusal.Matches(world, host, nonce, request, raw + " "), "A stale or nonexact refusal matched the pending intent.");
            Check(OwnerIntentRefusal.MarkerKey("Test.Mod", host) == "Test.Mod/OwnerRefusal/-12", "Actor key is unstable.");
        });

        test("unknown and noncanonical native refusal markers are unreadable", () =>
        {
            string[] fields = Create().Encode().Split('|');
            foreach (var (index, value) in new[] { (0, "2"), (1, "07"), (2, "-0"), (3, "-12"), (3, "+10"),
                (4, Guid.Empty.ToString("D")), (5, nonce.ToString("D").ToUpperInvariant()), (7, new string('A', 64)) })
            {
                var malformed = fields.ToArray();
                malformed[index] = value;
                Check(!OwnerIntentRefusal.TryRead(string.Join('|', malformed), out _), "Malformed marker was accepted.");
            }
            Check(!OwnerIntentRefusal.TryRead(new string('1', 257), out _)
                && !OwnerIntentRefusal.TryRead(string.Join('|', fields) + "|extra", out _)
                && !OwnerIntentRefusal.TryCreate(world, request.ActorId, nonce, raw, out _),
                "An oversized, extended or host-owned marker was accepted.");
        });

        OwnerIntentRefusal Create() => OwnerIntentRefusal.TryCreate(world, host, nonce, raw, out var refusal)
            ? refusal : throw new InvalidOperationException("The fixture refusal is invalid.");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
