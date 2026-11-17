using System.Globalization;

namespace XiangyeShiling.Networking;

/// <summary>The native host Farmer advertises one capability for an already verified direct-IP peer.</summary>
internal readonly record struct LanMachineCapability(ulong WorldId, long HostId, long ActorId, Guid SessionId, Guid Challenge)
{
    internal static string Key(string modId, long actorId) => modId + "/LanMachineCapability/"
        + actorId.ToString(CultureInfo.InvariantCulture);

    internal string Encode() => FormattableString.Invariant(
        $"1|{this.WorldId}|{this.HostId}|{this.ActorId}|{this.SessionId:D}|{this.Challenge:D}");

    internal bool MatchesWorld(ulong world, long host, long actor) => this.WorldId == world
        && this.HostId == host && this.ActorId == actor;

    internal static bool TryRead(string? raw, out LanMachineCapability capability)
    {
        capability = default;
        if (raw is not { Length: <= 144 }) return false;
        string[] fields = raw.Split('|');
        if (fields.Length != 6 || fields[0] != "1"
            || !ulong.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong world) || world == 0
            || !long.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long host) || host == 0
            || !long.TryParse(fields[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long actor) || actor == 0 || actor == host
            || !Guid.TryParseExact(fields[4], "D", out Guid session) || session == Guid.Empty
            || !Guid.TryParseExact(fields[5], "D", out Guid challenge) || challenge == Guid.Empty) return false;

        var parsed = new LanMachineCapability(world, host, actor, session, challenge);
        if (parsed.Encode() != raw) return false;
        capability = parsed;
        return true;
    }
}
