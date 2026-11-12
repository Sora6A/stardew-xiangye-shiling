using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace XiangyeShiling.Networking;

/// <summary>
/// A host's final decision to issue no grant for an authenticated machine intent. The caller stores this
/// in the native host Farmer and fences the request ID before publishing it. The nonce identifies one
/// pending intent on one client connection; it grants no authority on its own.
/// </summary>
internal sealed record OwnerIntentRefusal
{
    private const string Version = "1";
    private const int IntentPrefixLength = 39;
    private const int MaxMarkerLength = 256;
    internal ulong WorldId { get; }
    internal long HostId { get; }
    internal long ActorId { get; }
    internal Guid SessionId { get; }
    internal Guid Nonce { get; }
    internal Guid RequestId { get; }
    internal string RequestDigest { get; }

    private OwnerIntentRefusal(ulong worldId, long hostId, long actorId, Guid sessionId, Guid nonce, Guid requestId,
        string requestDigest)
    {
        this.WorldId = worldId;
        this.HostId = hostId;
        this.ActorId = actorId;
        this.SessionId = sessionId;
        this.Nonce = nonce;
        this.RequestId = requestId;
        this.RequestDigest = requestDigest;
    }

    internal static string MarkerKey(string modId, long actorId) => modId + "/OwnerRefusal/"
        + actorId.ToString(CultureInfo.InvariantCulture);

    internal static string WriteIntent(Guid nonce, string requestRaw)
    {
        if (nonce == Guid.Empty || !MachineRequestCodec.TryRead(requestRaw, out _))
            throw new ArgumentException("A machine intent needs a nonce and a valid request.");
        return $"{Version}|{nonce:D}|{requestRaw}";
    }

    // Split only the fixed prefix. The exact embedded JSON, including whitespace and any '|' character,
    // continues through the existing strict request reader after the native transport gate and quota.
    internal static bool TryReadIntent(string? wire, out Guid nonce, [NotNullWhen(true)] out string? requestRaw)
    {
        nonce = default;
        requestRaw = null;
        if (wire is null || wire.Length <= IntentPrefixLength || wire.Length > IntentPrefixLength + MachineRequestCodec.MaxUtf8Bytes
            || !wire.StartsWith("1|", StringComparison.Ordinal) || wire[IntentPrefixLength - 1] != '|'
            || !TryReadGuid(wire.AsSpan(2, 36), out Guid parsed)) return false;

        string candidate = wire[IntentPrefixLength..];
        if (!MachineRequestCodec.IsBoundedWireText(candidate)) return false;
        nonce = parsed;
        requestRaw = candidate;
        return true;
    }

    internal static bool TryCreate(ulong worldId, long hostId, Guid nonce, string? requestRaw,
        [NotNullWhen(true)] out OwnerIntentRefusal? refusal)
    {
        refusal = null;
        if (worldId == 0 || hostId == 0 || nonce == Guid.Empty || !MachineRequestCodec.TryRead(requestRaw, out var request)
            || request!.ActorId == hostId) return false;
        refusal = new(worldId, hostId, request.ActorId, request.SessionId, nonce, request.RequestId, Digest(requestRaw!));
        return true;
    }

    internal string Encode() => FormattableString.Invariant(
        $"{Version}|{this.WorldId}|{this.HostId}|{this.ActorId}|{this.SessionId:D}|{this.Nonce:D}|{this.RequestId:D}|{this.RequestDigest}");

    internal static bool TryRead(string? raw, [NotNullWhen(true)] out OwnerIntentRefusal? refusal)
    {
        refusal = null;
        if (raw is null || raw.Length > MaxMarkerLength) return false;
        string[] fields = raw.Split('|');
        if (fields.Length != 8 || fields[0] != Version
            || !ulong.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong world) || world == 0
            || !long.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long host) || host == 0
            || !long.TryParse(fields[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long actor) || actor == 0 || actor == host
            || !TryReadGuid(fields[4], out Guid session) || !TryReadGuid(fields[5], out Guid nonce) || !TryReadGuid(fields[6], out Guid request)
            || fields[7].Length != 64 || fields[7].Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            return false;
        var parsed = new OwnerIntentRefusal(world, host, actor, session, nonce, request, fields[7]);
        if (parsed.Encode() != raw) return false;
        refusal = parsed;
        return true;
    }

    internal bool Matches(ulong worldId, long hostId, Guid nonce, MachineRequestEnvelope request, string requestRaw)
        => this.WorldId == worldId && this.HostId == hostId && this.ActorId == request.ActorId
            && this.SessionId == request.SessionId && this.Nonce == nonce && this.RequestId == request.RequestId
            && MachineRequestCodec.IsBoundedWireText(requestRaw) && this.RequestDigest == Digest(requestRaw)
            && MachineRequestCodec.TryRead(requestRaw, out var decoded) && decoded == request;

    private static bool TryReadGuid(ReadOnlySpan<char> value, out Guid guid) => Guid.TryParseExact(value, "D", out guid)
        && guid != Guid.Empty && value.SequenceEqual(guid.ToString("D"));

    private static string Digest(string requestRaw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestRaw))).ToLowerInvariant();
}
