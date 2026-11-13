using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace XiangyeShiling.Networking;

/// <summary>The v3 direct-IP wrapper retains the exact v1 machine request used in native save records.</summary>
internal sealed record DirectIpMachineIntent
{
    internal const string MessageType = "XYS/MachineIntent/3";
    private const string Prefix = "3|";
    private const int GuidLength = 36;
    private const int MaxSequenceDigits = 20;
    private const int MaxPrefixLength = 2 + 2 * (GuidLength + 1) + MaxSequenceDigits + 1;

    internal Guid Challenge { get; }
    internal ulong Sequence { get; }
    internal Guid Nonce { get; }
    internal string RequestRaw { get; }
    internal MachineRequestEnvelope Request { get; }

    private DirectIpMachineIntent(Guid challenge, ulong sequence, Guid nonce, string requestRaw, MachineRequestEnvelope request)
    {
        this.Challenge = challenge;
        this.Sequence = sequence;
        this.Nonce = nonce;
        this.RequestRaw = requestRaw;
        this.Request = request;
    }

    internal static string Write(Guid challenge, ulong sequence, Guid nonce, string requestRaw)
    {
        if (challenge == Guid.Empty || sequence == 0 || nonce == Guid.Empty || !MachineRequestCodec.TryRead(requestRaw, out _))
            throw new ArgumentException("A direct-IP intent needs a challenge, positive sequence, nonce and valid request.");
        return FormattableString.Invariant($"{Prefix}{challenge:D}|{sequence}|{nonce:D}|{requestRaw}");
    }

    internal static bool TryRead(string? wire, [NotNullWhen(true)] out DirectIpMachineIntent? intent)
    {
        intent = null;
        const int sequenceStart = 2 + GuidLength + 1;
        const int minimumPrefixLength = MaxPrefixLength - MaxSequenceDigits + 1;
        if (wire is null || wire.Length <= minimumPrefixLength || wire.Length > MaxPrefixLength + MachineRequestCodec.MaxUtf8Bytes
            || !wire.StartsWith(Prefix, StringComparison.Ordinal) || wire[sequenceStart - 1] != '|'
            || !TryReadGuid(wire.AsSpan(Prefix.Length, GuidLength), out Guid challenge)) return false;

        int sequenceLength = wire.AsSpan(sequenceStart).IndexOf('|');
        if (sequenceLength is < 1 or > MaxSequenceDigits
            || !TryReadSequence(wire.AsSpan(sequenceStart, sequenceLength), out ulong sequence))
            return false;

        int nonceStart = sequenceStart + sequenceLength + 1;
        int requestStart = nonceStart + GuidLength + 1;
        if (requestStart >= wire.Length || wire[requestStart - 1] != '|'
            || !TryReadGuid(wire.AsSpan(nonceStart, GuidLength), out Guid nonce)) return false;

        string requestRaw = wire[requestStart..];
        if (!MachineRequestCodec.TryRead(requestRaw, out var request) || request is null) return false;
        intent = new DirectIpMachineIntent(challenge, sequence, nonce, requestRaw, request);
        return true;
    }

    private static bool TryReadGuid(ReadOnlySpan<char> value, out Guid guid) => Guid.TryParseExact(value, "D", out guid)
        && guid != Guid.Empty && value.SequenceEqual(guid.ToString("D"));

    private static bool TryReadSequence(ReadOnlySpan<char> value, out ulong sequence)
    {
        // Numeric parsing accepts some noncanonical spellings, including trailing NUL.
        return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence) && sequence > 0
            && value.SequenceEqual(sequence.ToString(CultureInfo.InvariantCulture));
    }
}

internal enum DirectIpIntentDisposition { Next, CurrentRefused, CurrentReserveAttempt, Stale, Gap, ForeignChallenge, Conflict }

/// <summary>
/// One tracker belongs to a currently verified native and SDK peer pair. The caller checks world, actor and
/// exact transport before consulting it and retires it when the verified challenge or connection changes.
/// </summary>
internal sealed class DirectIpIntentSequence
{
    private readonly Guid Challenge;
    private DirectIpMachineIntent? Current;
    private DirectIpIntentDisposition CurrentOutcome;

    internal DirectIpIntentSequence(Guid challenge)
    {
        if (challenge == Guid.Empty) throw new ArgumentException("A verified challenge is required.", nameof(challenge));
        this.Challenge = challenge;
    }

    internal ulong HighWater => this.Current?.Sequence ?? 0;

    internal DirectIpIntentDisposition Classify(DirectIpMachineIntent intent)
    {
        if (intent.Challenge != this.Challenge) return DirectIpIntentDisposition.ForeignChallenge;
        ulong highWater = this.HighWater;
        if (intent.Sequence < highWater) return DirectIpIntentDisposition.Stale;
        if (intent.Sequence == highWater)
            return intent == this.Current ? this.CurrentOutcome : DirectIpIntentDisposition.Conflict;

        // Subtract after establishing order. The final valid UInt64 sequence never wraps to zero.
        return intent.Sequence - highWater == 1 ? DirectIpIntentDisposition.Next : DirectIpIntentDisposition.Gap;
    }

    /// <summary>Fence a determinate no-grant outcome before publishing the exact native refusal.</summary>
    internal bool TryRecordRefusal(DirectIpMachineIntent intent) => this.TryAdvance(intent, DirectIpIntentDisposition.CurrentRefused);

    /// <summary>Fence a plan immediately before attempting the native reserve setter, even if it may fail ambiguously.</summary>
    internal bool TryBeginReserve(DirectIpMachineIntent intent) => this.TryAdvance(intent, DirectIpIntentDisposition.CurrentReserveAttempt);

    private bool TryAdvance(DirectIpMachineIntent intent, DirectIpIntentDisposition outcome)
    {
        if (this.Classify(intent) != DirectIpIntentDisposition.Next) return false;
        this.Current = intent;
        this.CurrentOutcome = outcome;
        return true;
    }
}
