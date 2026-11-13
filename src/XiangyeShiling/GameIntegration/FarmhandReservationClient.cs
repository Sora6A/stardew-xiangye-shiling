using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.Networking;
using Microsoft.Xna.Framework;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

internal enum FarmhandRequestStatus
{
    None, AwaitingHostReservation, AwaitingHostResult, Cancelling, Applied, Cancelled, NoGrant, Unconfirmed
}

/// <summary>
/// The grant and receipt come from the replicated native host Farmer, not a mod-message sender claim.
/// A new debit also needs this live client's exact pending intent, which is fenced on session reset.
/// </summary>
internal sealed class FarmhandReservationClient
{
    private readonly OwnerInventoryCommit Commit;
    private MachineRequestEnvelope? Pending;
    private Guid PendingConnection;
    private Guid PendingNonce;
    private Func<bool>? PendingInteraction;
    private bool ObservedReservation;
    private (Guid Request, FarmhandRequestStatus Status) LastResult;
    private TerminalObservation? KnownTerminal;
    private (Guid Session, Guid Request) LastRefused;
    // Keep the last actual attempt through connection fences. A new grant has a different plan key.
    private string? AttemptedPlan;
    private MachineRequestEnvelope? AttemptedRequest;
    private bool CancellationRequested;
    private bool Saving;

    private sealed record TerminalObservation(ulong WorldId, long HostId, long ActorId, Guid RequestId, string Seen,
        FarmhandRequestStatus Status);

    internal FarmhandReservationClient(IMonitor monitor) => this.Commit = new(monitor);

    internal void OnSaving() { this.Saving = true; this.Commit.OnSaving(); }
    internal void OnSaved() { this.Saving = false; this.Commit.OnSaved(); }
    internal void ResetWorld()
    {
        this.FenceIntent();
        this.LastRefused = default;
        this.Saving = false;
        this.Commit.Reset();
    }

    internal void FenceIntent()
    {
        if (this.Pending is { } pending) this.LastResult = (pending.RequestId, FarmhandRequestStatus.Unconfirmed);
        this.ClearPending();
    }

    private void ClearPending()
    {
        this.Pending = null;
        this.PendingConnection = Guid.Empty;
        this.PendingNonce = Guid.Empty;
        this.PendingInteraction = null;
        this.ObservedReservation = false;
        this.CancellationRequested = false;
    }

    /// <summary>Read only the exact in-flight request or the latest locally verified result.</summary>
    internal FarmhandRequestStatus ReadStatus(Guid exactRequestId)
    {
        if (this.Pending?.RequestId == exactRequestId)
            return this.CancellationRequested ? FarmhandRequestStatus.Cancelling
                : this.ObservedReservation ? FarmhandRequestStatus.AwaitingHostResult : FarmhandRequestStatus.AwaitingHostReservation;
        return this.LastResult.Request == exactRequestId ? this.LastResult.Status : FarmhandRequestStatus.None;
    }

    /// <summary>Queue a decline for the exact live intent. The native host still owns the final cancellation.</summary>
    internal bool RequestCancellation(Guid exactPendingId)
    {
        if (this.Pending is not { } pending || pending.RequestId != exactPendingId || pending == this.AttemptedRequest
            || this.KnownTerminal?.RequestId == exactPendingId)
            return false;
        this.CancellationRequested = true;
        return true;
    }

    internal bool TryRemember(MachineRequestEnvelope intent, Guid connectionId, out Guid nonce)
        => this.TryRemember(intent, connectionId, null, out nonce);

    /// <summary>The production predicate is a read-only check of the initiating menu and local target.</summary>
    internal bool TryRemember(MachineRequestEnvelope intent, Guid connectionId, Func<bool>? isInteractionCurrent,
        out Guid nonce, bool allowNew = true)
    {
        nonce = default;
        if (connectionId == Guid.Empty || this.LastRefused == (intent.SessionId, intent.RequestId)
            || (isInteractionCurrent is not null && this.LastResult.Request == intent.RequestId)) return false;
        if (this.Pending is not null)
        {
            if (this.Pending != intent || this.PendingConnection != connectionId
                || (this.PendingInteraction is null) != (isInteractionCurrent is null)) return false;
            // Exact retries retain the original permission and nonce, even if the original menu has since closed.
        }
        else
        {
            if (!allowNew || this.Saving || Game1.player.modData.ContainsKey(OwnerInventoryCommit.JournalKey)
                || (isInteractionCurrent is not null && !CheckInteraction(isInteractionCurrent))) return false;
            this.Pending = intent;
            this.PendingConnection = connectionId;
            this.PendingNonce = Guid.NewGuid();
            this.PendingInteraction = isInteractionCurrent;
        }
        nonce = this.PendingNonce;
        return true;
    }

    internal void Advance(long hostId, Guid sessionId, Guid connectionId)
    {
        if (this.Pending is { } request && (request.SessionId != sessionId || this.PendingConnection != connectionId))
            this.FenceIntent();
        Farmer owner = Game1.player;
        Farmer? host = Game1.MasterPlayer;
        if (host is null || ReferenceEquals(host, owner) || host.UniqueMultiplayerID != hostId) return;
        this.RefreshCancellation();

        OwnerCommitReservation? plan = null;
        if (host.modData.TryGetValue(HostReservationStore.Key, out string raw)
            && (!OwnerCommitReservationCodec.TryRead(raw, out plan) || plan.WorldId != Game1.uniqueIDForThisGame || plan.HostId != hostId))
            return;

        // An older grant can still occupy this owner's native slot after reconnect. Its existence only
        // blocks refusal of its own request ID. Clear a different, positively refused intent so the owner
        // can explicitly retry the older grant without altering the host's reservation.
        if (this.Pending is { } pending && (plan?.Request.ActorId != pending.ActorId || plan.Request.RequestId != pending.RequestId)
            && !owner.modData.ContainsKey(OwnerInventoryCommit.JournalKey)
            && host.modData.TryGetValue(OwnerIntentRefusal.MarkerKey(MachineService.Prefix, owner.UniqueMultiplayerID), out string refused)
            && OwnerIntentRefusal.TryRead(refused, out var refusal)
            && refusal.Matches(Game1.uniqueIDForThisGame, hostId, this.PendingNonce, pending, MachineRequestCodec.Write(pending)))
        {
            this.LastRefused = (pending.SessionId, pending.RequestId);
            this.LastResult = (pending.RequestId, FarmhandRequestStatus.NoGrant);
            this.ClearPending();
        }

        if (plan?.Request.ActorId == owner.UniqueMultiplayerID)
        {
            bool current = plan.Request == this.Pending;
            if (current) this.ObservedReservation = true;
            if (plan.Status != OwnerReservationStatus.Reserved)
            {
                string seen = OwnerInventoryCommit.SeenJournal(plan);
                if (current)
                    this.KnownTerminal = new(plan.WorldId, plan.HostId, plan.Request.ActorId, plan.Request.RequestId, seen,
                        plan.Status == OwnerReservationStatus.Applied ? FarmhandRequestStatus.Applied : FarmhandRequestStatus.Cancelled);
                if (owner.modData.GetValueOrDefault(OwnerInventoryCommit.JournalKey) != seen)
                    this.Commit.TryMarkSeen(owner, plan);
            }
            else if (!this.Saving && current && this.AttemptedPlan != plan.PlanKey
                && OwnerInventoryCommit.ObserveJournal(owner, plan) == OwnerJournalObservation.Absent)
            {
                if (this.CancellationRequested)
                    this.Commit.TryDecline(owner, plan);
                else if (!Game1.eventUp && !owner.UsingTool
                    && owner.currentLocation is { } location && location.NameOrUniqueName == plan.Request.LocationName
                    && location.objects.TryGetValue(new Vector2(plan.Request.TileX, plan.Request.TileY), out SObject machine)
                    && machine.TileLocation == new Vector2(plan.Request.TileX, plan.Request.TileY)
                    && machine.modData.GetValueOrDefault(MachineService.StateKey) == plan.BeforeRaw && MachineService.TryRead(machine, out _)
                    && Math.Abs((long)owner.Tile.X - plan.Request.TileX) <= 1 && Math.Abs((long)owner.Tile.Y - plan.Request.TileY) <= 1
                    && OwnerInventoryCommit.Observe(owner, plan.WriteSet) == OwnerInventoryObservation.Before)
                {
                    this.RefreshCancellation();
                    if (this.CancellationRequested)
                    {
                        this.Commit.TryDecline(owner, plan);
                        return;
                    }
                    // Delay while save, map or native slot sync can still settle. Once execution is entered,
                    // even a false result can represent an uncertain rollback and consumes the attempt.
                    this.AttemptedPlan = plan.PlanKey;
                    this.AttemptedRequest = plan.Request;
                    this.Commit.TryApply(owner, plan);
                }
            }
            return;
        }

        if (!HostReservationStore.TryReadReceipt(host, owner.UniqueMultiplayerID, out var receipt) || receipt is null) return;
        if (!this.Commit.TryClearSeen(owner, receipt.Seen)) return;
        if (this.KnownTerminal is { } terminal && terminal.WorldId == Game1.uniqueIDForThisGame && terminal.HostId == hostId
            && terminal.ActorId == owner.UniqueMultiplayerID && terminal.RequestId == receipt.RequestId && terminal.Seen == receipt.Seen)
        {
            // The existing receipt format has no outcome field. Report only the status previously read
            // from the exact native terminal plan, after the matching receipt and marker have retired.
            this.LastResult = (terminal.RequestId, terminal.Status);
            this.KnownTerminal = null;
            if (this.Pending?.RequestId == receipt.RequestId) this.ClearPending();
        }
        else if (this.PendingInteraction is null && this.Pending?.RequestId == receipt.RequestId)
            this.ClearPending();
    }

    private void RefreshCancellation()
    {
        if (!this.CancellationRequested && this.Pending is { } pending && pending != this.AttemptedRequest
            && this.PendingInteraction is { } check && !CheckInteraction(check))
            this.CancellationRequested = true;
    }

    private static bool CheckInteraction(Func<bool> check)
    {
        try { return check(); }
        catch (Exception error) when (error is not OutOfMemoryException) { return false; }
    }
}
