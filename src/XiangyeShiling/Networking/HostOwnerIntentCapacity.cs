namespace XiangyeShiling.Networking;

internal enum HostOwnerIntentDisposition { Prepare, KeepGrant, Refuse }

/// <summary>Fence distinct owner request IDs for the lifetime of this loaded host world.</summary>
internal sealed class HostOwnerIntentCapacity
{
    private const int Limit = 256;
    private readonly HashSet<(long Actor, Guid Request)> Known = new();

    internal int Count => this.Known.Count;

    internal HostOwnerIntentDisposition Decide((long Actor, Guid Request) key, (long Actor, Guid Request)? live)
    {
        // A persisted grant, including one loaded from disk, owns one entry before any refusal is recorded.
        if (live is { } grant) this.Remember(grant);
        if (live == key) return HostOwnerIntentDisposition.KeepGrant;
        return live is not null || this.Known.Contains(key) || this.Count >= Limit
            ? HostOwnerIntentDisposition.Refuse : HostOwnerIntentDisposition.Prepare;
    }

    // Overflow need not be remembered because new grants stay closed until a world reset changes the session.
    internal void Remember((long Actor, Guid Request) key)
    {
        if (this.Count < Limit) this.Known.Add(key);
    }

    internal void Reset() => this.Known.Clear();
}
