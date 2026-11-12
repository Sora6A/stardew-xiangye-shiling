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
    private string? AttemptedPlan;

    internal FarmhandReservationClient(IMonitor monitor) => this.Commit = new(monitor);

    internal void OnSaving() => this.Commit.OnSaving();
    internal void OnSaved() => this.Commit.OnSaved();
    internal void ResetWorld()
    {
        this.FenceIntent();
        this.Commit.Reset();
    }

    internal void FenceIntent()
    {
        this.Pending = null;
        this.PendingConnection = Guid.Empty;
        this.AttemptedPlan = null;
    }

    internal bool TryRemember(MachineRequestEnvelope intent, Guid connectionId)
    {
        if (connectionId == Guid.Empty) return false;
        if (this.Pending is not null) return this.Pending == intent && this.PendingConnection == connectionId;
        if (Game1.player.modData.ContainsKey(OwnerInventoryCommit.JournalKey)) return false;
        this.Pending = intent;
        this.PendingConnection = connectionId;
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

        if (plan?.Request.ActorId == owner.UniqueMultiplayerID)
        {
            if (plan.Status != OwnerReservationStatus.Reserved)
            {
                if (owner.modData.GetValueOrDefault(OwnerInventoryCommit.JournalKey) != OwnerInventoryCommit.SeenJournal(plan))
                    this.Commit.TryMarkSeen(owner, plan);
            }
            else if (plan.Request == this.Pending && this.AttemptedPlan != plan.PlanKey && !Game1.eventUp && !owner.UsingTool
                && owner.currentLocation is { } location && location.NameOrUniqueName == plan.Request.LocationName
                && location.objects.TryGetValue(new Vector2(plan.Request.TileX, plan.Request.TileY), out SObject machine)
                && machine.TileLocation == new Vector2(plan.Request.TileX, plan.Request.TileY)
                && machine.modData.GetValueOrDefault(MachineService.StateKey) == plan.BeforeRaw && MachineService.TryRead(machine, out _)
                && Math.Abs((long)owner.Tile.X - plan.Request.TileX) <= 1 && Math.Abs((long)owner.Tile.Y - plan.Request.TileY) <= 1)
            {
                this.AttemptedPlan = plan.PlanKey;
                this.Commit.TryApply(owner, plan);
            }
            return;
        }

        if (!HostReservationStore.TryReadReceipt(host, owner.UniqueMultiplayerID, out var receipt) || receipt is null) return;
        if (this.Commit.TryClearSeen(owner, receipt.Seen) && this.Pending?.RequestId == receipt.RequestId)
            this.FenceIntent();
    }
}
