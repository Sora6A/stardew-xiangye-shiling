using System.Diagnostics.CodeAnalysis;
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
        return this.TryWrite(host, null, reservation);
    }

    internal bool TryRecordTerminal(OwnerCommitReservation reservation, OwnerReservationStatus status)
    {
        if (status is not (OwnerReservationStatus.Applied or OwnerReservationStatus.Cancelled)
            || !this.TryGetHost(out Farmer? host) || !TryRead(host, out string? currentRaw, out var current)
            || current is null || current.PlanKey != reservation.PlanKey || current.Status != OwnerReservationStatus.Reserved)
            return false;

        // The caller reconciles the exact inventory, journal and machine observations before recording a result.
        return this.TryWrite(host, currentRaw, current.WithTerminalStatus(status));
    }

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

    private bool TryWrite(Farmer host, string? expectedRaw, OwnerCommitReservation next)
    {
        string raw = OwnerCommitReservationCodec.Write(next);
        if (host.modData.GetValueOrDefault(Key) != expectedRaw) return false;

        this.Writing = true;
        try
        {
            host.modData[Key] = raw;
            if (host.modData.GetValueOrDefault(Key) == raw) return true;
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
