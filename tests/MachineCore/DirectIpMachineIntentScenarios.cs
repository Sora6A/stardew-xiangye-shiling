using System.Globalization;
using System.Reflection;
using System.Text;
using XiangyeShiling.Networking;
using static XiangyeShiling.Networking.DirectIpIntentDisposition;

internal static class DirectIpMachineIntentScenarios
{
    internal static void Run(Action<string, Action> test)
    {
        var challenge = Guid.Parse("fe4abe19-e642-4721-bf17-d1c64be2fead");
        var nonce = Guid.Parse("dbef907a-9a88-462e-a461-e44ba3d94e15");
        var request = new MachineRequestEnvelope(1, Guid.NewGuid(), Guid.NewGuid(), 10, "Farm|Shed", 2, 3, Guid.NewGuid(), 0,
            MachineRequestAction.Start, "bean-workshop");
        string raw = " \n" + MachineRequestCodec.Write(request) + "\r\n";

        test("direct IP intent v3 preserves the original request bytes and canonical outer fields", () =>
        {
            string wire = DirectIpMachineIntent.Write(challenge, 17, nonce, raw);
            Check(wire == $"3|{challenge:D}|17|{nonce:D}|{raw}", "V3 outer encoding changed.");
            Check(DirectIpMachineIntent.TryRead(wire, out var read) && read.Challenge == challenge && read.Sequence == 17
                && read.Nonce == nonce && read.Request == request && Encoding.UTF8.GetBytes(read.RequestRaw).SequenceEqual(Encoding.UTF8.GetBytes(raw)),
                "V3 changed the challenge, sequence, nonce or inner request bytes.");
            int padding = MachineRequestCodec.MaxUtf8Bytes - Encoding.UTF8.GetByteCount(raw);
            string exactLimit = raw + new string(' ', padding);
            string atLimit = DirectIpMachineIntent.Write(challenge, ulong.MaxValue, nonce, exactLimit);
            Check(DirectIpMachineIntent.TryRead(atLimit, out var maximum) && maximum.RequestRaw == exactLimit,
                "A canonical max-sequence request at the byte limit should survive.");
            Check(!DirectIpMachineIntent.TryRead(atLimit + " ", out _), "An oversized body was accepted.");
        });

        test("direct IP intent rejects malformed versions challenges sequences nonces and inner bodies", () =>
        {
            foreach (string invalidSequence in new[] { "", "0", "01", "+1", "-1", " 1", "1 ", "1\0", "１", "18446744073709551616" })
                Reject($"3|{challenge:D}|{invalidSequence}|{nonce:D}|{raw}");
            Reject(OwnerIntentRefusal.WriteIntent(nonce, raw));
            Reject($"3|{challenge:D}|1|{nonce:D}|{{}}");
            Reject($"3|{challenge:D}|1|{nonce:D}|{raw}\ud800");
            Reject($"3|{challenge:D}|1|{nonce:D}|{new string('禾', 1400)}");
            Reject($"3|{challenge.ToString("D").ToUpperInvariant()}|1|{nonce:D}|{raw}");
            Reject($"3|{Guid.Empty:D}|1|{nonce:D}|{raw}");
            Reject($"3|{challenge:D}|1|{Guid.Empty:D}|{raw}");
            Reject($"3|{challenge:D}|1|{nonce.ToString("D").ToUpperInvariant()}|{raw}");
            Reject($"3|{challenge:D}|1|{nonce:D}|");
            Reject($"3|{challenge:D}|1|{nonce:D}");
            Reject(null);
        });

        test("direct IP sequence advances only on a final refusal or reserve attempt and classifies exact retry", () =>
        {
            var tracker = new DirectIpIntentSequence(challenge);
            var first = Read(1);
            var second = Read(2);
            Check(tracker.HighWater == 0 && tracker.Classify(first) == Next && tracker.Classify(first) == Next
                && tracker.Classify(second) == Gap && !tracker.TryRecordRefusal(second) && !tracker.TryBeginReserve(second),
                "Inspecting a transient request or a gap must leave the next number available.");
            Check(tracker.TryRecordRefusal(first) && tracker.HighWater == 1 && tracker.Classify(Read(1)) == CurrentRefused
                && !tracker.TryBeginReserve(first) && !tracker.TryRecordRefusal(first), "An exact refusal cannot turn into a later grant.");
            Check(tracker.Classify(Read(1, body: raw.Trim())) == Conflict && tracker.Classify(Read(1, nonceOverride: Guid.NewGuid())) == Conflict
                && tracker.Classify(Read(1, body: MachineRequestCodec.Write(request with { TileX = 4 }))) == Conflict,
                "Equal sequence must bind exact request bytes and nonce.");
            Check(tracker.TryBeginReserve(second) && tracker.Classify(Read(2)) == CurrentReserveAttempt
                && !tracker.TryBeginReserve(second) && !tracker.TryRecordRefusal(second) && tracker.Classify(first) == Stale
                && tracker.HighWater == 2, "An ambiguous reserve cannot be retried as a new grant or labelled refused.");
        });

        test("direct IP sequence fences a new connection and does not wrap after UInt64 max", () =>
        {
            var old = new DirectIpIntentSequence(challenge);
            var replacementChallenge = Guid.NewGuid();
            var replacement = new DirectIpIntentSequence(replacementChallenge);
            var oldFirst = Read(1);
            Check(old.TryBeginReserve(oldFirst) && replacement.Classify(oldFirst) == ForeignChallenge && !replacement.TryBeginReserve(oldFirst)
                && replacement.TryRecordRefusal(Read(1, challengeOverride: replacementChallenge)),
                "A new connection must start at one without reviving the old challenge.");

            // Jump to the boundary using the same current-intent representation as production. Ordinary
            // progression to this point would require UInt64.MaxValue - 1 independent decisions.
            typeof(DirectIpIntentSequence).GetField("Current", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(old, Read(ulong.MaxValue - 1));
            var maximum = Read(ulong.MaxValue);
            Check(old.TryBeginReserve(maximum) && old.HighWater == ulong.MaxValue && old.Classify(Read(ulong.MaxValue)) == CurrentReserveAttempt
                && old.Classify(oldFirst) == Stale && !old.TryRecordRefusal(oldFirst), "The final number must stay replayable without wrapping.");
            Reject($"3|{challenge:D}|{0.ToString(CultureInfo.InvariantCulture)}|{nonce:D}|{raw}");
        });

        DirectIpMachineIntent Read(ulong sequence, string? body = null, Guid? nonceOverride = null, Guid? challengeOverride = null)
            => DirectIpMachineIntent.TryRead(DirectIpMachineIntent.Write(challengeOverride ?? challenge, sequence, nonceOverride ?? nonce, body ?? raw),
                out var result) ? result : throw new InvalidOperationException("The fixture intent is invalid.");
    }

    private static void Reject(string? wire) => Check(!DirectIpMachineIntent.TryRead(wire, out var intent) && intent is null,
        "Malformed direct-IP input was accepted.");

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
