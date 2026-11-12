using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// Owns one reservation in the main farmer's native save data, alongside the machine and farmhand inventory.
/// A successful write is visible in this world. Disk persistence requires the native save checkpoint.
/// </summary>
internal sealed class HostReservationStore
{
    internal const string Key = MachineService.Prefix + "/HostReservation";

    internal sealed record Receipt(Guid RequestId, string Seen);

    private readonly IMonitor Monitor;
    private readonly int GameThread = Environment.CurrentManagedThreadId;
    private bool Saving;
    private bool Writing;
    private bool Quarantined;

    internal HostReservationStore(IMonitor monitor) => this.Monitor = monitor;

    internal void OnSaving() => this.Saving = true;
    internal void OnSaved() => this.Saving = false;

    internal void Reset()
    {
        this.Saving = false;
        this.Quarantined = false;
    }

    internal void Isolate(string reason)
    {
        if (this.Quarantined) return;
        this.Quarantined = true;
        this.Monitor.Log("Host reservation isolated: " + reason, LogLevel.Warn);
    }

    internal bool TryRead(out OwnerCommitReservation? reservation)
    {
        reservation = null;
        if (!this.TryGetHost(out Farmer? host)) return false;
        return TryRead(host, out _, out reservation);
    }

    internal bool BlocksMachine(SObject? machine)
    {
        if (!Context.IsWorldReady || !MachineService.TryGetKind(machine, out _)) return false;

        // A farmhand's replica can lag the host reservation. Until removal is itself host-authorized,
        // the farmhand cannot remove a custom machine through a local native entry point.
        if (Context.IsMultiplayer && !Context.IsMainPlayer) return true;
        if (!this.TryRead(out OwnerCommitReservation? reservation)) return true;
        return reservation is not null && MachineService.TryRead(machine, out var state)
            && state!.InstanceId == reservation.Request.InstanceId;
    }

    internal bool TryReserve(OwnerCommitReservation reservation)
    {
        if (reservation.Status != OwnerReservationStatus.Reserved || !this.TryGetHost(out Farmer? host)
            || !IsSameWorld(host, reservation) || host.modData.ContainsKey(Key)) return false;

        // An existing value, even an unknown or terminal one, owns this slot until reconciled.
        return this.TryWrite(host, Key, null, OwnerCommitReservationCodec.Write(reservation));
    }

    internal bool TryRecordTerminal(OwnerCommitReservation reservation, OwnerReservationStatus status)
    {
        if (status is not (OwnerReservationStatus.Applied or OwnerReservationStatus.Cancelled)
            || !this.TryGetHost(out Farmer? host) || !TryRead(host, out string? currentRaw, out var current)
            || current is null || current.PlanKey != reservation.PlanKey || current.Status != OwnerReservationStatus.Reserved)
            return false;

        // The caller reconciles the exact inventory, journal and machine observations before recording a result.
        return this.TryWrite(host, Key, currentRaw, OwnerCommitReservationCodec.Write(current.WithTerminalStatus(status)));
    }

    internal bool TryPublishRefusal(OwnerIntentRefusal refusal)
    {
        if (!this.TryGetHost(out Farmer? host) || refusal.WorldId != Game1.uniqueIDForThisGame || refusal.HostId != host.UniqueMultiplayerID
            || !TryRead(host, out _, out var current)
            || (current?.Request.ActorId == refusal.ActorId && current.Request.RequestId == refusal.RequestId))
            return false;

        if (!TryReadRefusal(host, refusal.ActorId, out string? old)) return false;
        string key = OwnerIntentRefusal.MarkerKey(MachineService.Prefix, refusal.ActorId);

        string next = refusal.Encode();
        return old == next || this.TryWrite(host, key, old, next);
    }

    internal static bool TryReadRefusal(Farmer host, long actorId, out string? raw)
    {
        string key = OwnerIntentRefusal.MarkerKey(MachineService.Prefix, actorId);
        if (!host.modData.TryGetValue(key, out raw)) return true;
        return OwnerIntentRefusal.TryRead(raw, out var refusal) && refusal.WorldId == Game1.uniqueIDForThisGame
            && refusal.HostId == host.UniqueMultiplayerID && refusal.ActorId == actorId;
    }

    /// <summary>Publish the result before releasing the reservation. The owner can clear its marker after reload.</summary>
    internal bool TryRetire(OwnerCommitReservation plan, string seen)
    {
        if (plan.Status == OwnerReservationStatus.Reserved || seen != OwnerInventoryCommit.SeenJournal(plan)
            || !this.TryGetHost(out Farmer? host) || !TryRead(host, out string? currentRaw, out var current)
            || current is null || current.PlanKey != plan.PlanKey || current.Status != plan.Status
            || !TryReadReceipt(host, plan.Request.ActorId, out _))
            return false;

        string key = ReceiptKey(plan.Request.ActorId);
        string next = FormattableString.Invariant($"1|{plan.WorldId}|{plan.HostId}|{plan.Request.RequestId:D}|{seen}");
        string? old = host.modData.GetValueOrDefault(key);
        return (old == next || this.TryWrite(host, key, old, next)) && this.TryWrite(host, Key, currentRaw, null);
    }

    internal static bool TryReadReceipt(Farmer host, long actorId, out Receipt? receipt)
    {
        receipt = null;
        if (!host.modData.TryGetValue(ReceiptKey(actorId), out string raw)) return true;
        if (raw is not { Length: <= 192 }) return false;
        string[] fields = raw.Split('|');
        if (fields.Length != 5 || fields[0] != "1" || fields[1] != Game1.uniqueIDForThisGame.ToString(CultureInfo.InvariantCulture)
            || fields[2] != host.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture)
            || !Guid.TryParseExact(fields[3], "D", out Guid requestId) || requestId == Guid.Empty)
            return false;

        string seen = fields[4];
        if (seen.Length != 106 || !Guid.TryParseExact(seen.AsSpan(0, 36), "D", out Guid grantId) || grantId == Guid.Empty
            || seen[36] != ':' || !seen.EndsWith(":Seen", StringComparison.Ordinal)) return false;
        foreach (char c in seen.AsSpan(37, 64))
            if (c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) return false;

        receipt = new(requestId, seen);
        return true;
    }

    private static string ReceiptKey(long actorId) => MachineService.Prefix + "/OwnerReceipt/"
        + actorId.ToString(CultureInfo.InvariantCulture);

    private bool TryGetHost([NotNullWhen(true)] out Farmer? host)
    {
        host = null;
        if (Environment.CurrentManagedThreadId != this.GameThread || this.Saving || this.Writing || this.Quarantined
            || !Context.IsWorldReady || !Context.IsMainPlayer || Context.IsSplitScreen || Context.ScreenId != 0)
            return false;

        Farmer? player = Game1.player;
        if (player is null || !ReferenceEquals(player, Game1.MasterPlayer) || player.UniqueMultiplayerID == 0
            || Game1.uniqueIDForThisGame == 0)
            return false;

        host = player;
        return true;
    }

    private static bool TryRead(Farmer host, out string? raw, out OwnerCommitReservation? reservation)
    {
        reservation = null;
        if (!host.modData.TryGetValue(Key, out raw)) return true;
        if (!OwnerCommitReservationCodec.TryRead(raw, out var parsed) || parsed is null || !IsSameWorld(host, parsed))
            return false;

        reservation = parsed;
        return true;
    }

    private bool TryWrite(Farmer host, string key, string? expectedRaw, string? next)
    {
        if (host.modData.GetValueOrDefault(key) != expectedRaw) return false;

        this.Writing = true;
        try
        {
            if (next is null) host.modData.Remove(key);
            else host.modData[key] = next;
            if (next is null ? !host.modData.ContainsKey(key) : host.modData.GetValueOrDefault(key) == next) return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Quarantined = true;
            this.Monitor.Log("Host reservation write failed: " + error.GetType().Name, LogLevel.Error);
            return false;
        }
        finally { this.Writing = false; }

        // A callback may have changed the value after the setter. Keep the observed bytes for recovery.
        this.Quarantined = true;
        return false;
    }

    private static bool IsSameWorld(Farmer host, OwnerCommitReservation reservation)
        => reservation.WorldId == Game1.uniqueIDForThisGame && reservation.HostId == host.UniqueMultiplayerID;
}
