using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.Networking;
using Microsoft.Xna.Framework;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

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
    private (Guid Session, Guid Request) LastRefused;
    // Keep the last actual attempt through connection fences. A new grant has a different plan key.
    private string? AttemptedPlan;
    private MachineRequestEnvelope? AttemptedRequest;
    private bool CancellationRequested;
    private bool Saving;

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
        this.Pending = null;
        this.PendingConnection = Guid.Empty;
        this.PendingNonce = Guid.Empty;
        this.CancellationRequested = false;
    }

    /// <summary>Queue a decline for the exact live intent. The native host still owns the final cancellation.</summary>
    internal bool RequestCancellation(Guid exactPendingId)
    {
        if (this.Pending is not { } pending || pending.RequestId != exactPendingId || pending == this.AttemptedRequest)
            return false;
        this.CancellationRequested = true;
        return true;
    }

    internal bool TryRemember(MachineRequestEnvelope intent, Guid connectionId, out Guid nonce)
    {
        nonce = default;
        if (connectionId == Guid.Empty || this.LastRefused == (intent.SessionId, intent.RequestId)) return false;
        if (this.Pending is not null)
        {
            if (this.Pending != intent || this.PendingConnection != connectionId) return false;
        }
        else
        {
            if (this.Saving || Game1.player.modData.ContainsKey(OwnerInventoryCommit.JournalKey)) return false;
            this.Pending = intent;
            this.PendingConnection = connectionId;
            this.PendingNonce = Guid.NewGuid();
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
            this.FenceIntent();
        }

        if (plan?.Request.ActorId == owner.UniqueMultiplayerID)
        {
            if (plan.Status != OwnerReservationStatus.Reserved)
            {
                if (owner.modData.GetValueOrDefault(OwnerInventoryCommit.JournalKey) != OwnerInventoryCommit.SeenJournal(plan))
                    this.Commit.TryMarkSeen(owner, plan);
            }
            else if (!this.Saving && plan.Request == this.Pending && this.AttemptedPlan != plan.PlanKey
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
        if (this.Commit.TryClearSeen(owner, receipt.Seen) && this.Pending?.RequestId == receipt.RequestId)
            this.FenceIntent();
    }
}
