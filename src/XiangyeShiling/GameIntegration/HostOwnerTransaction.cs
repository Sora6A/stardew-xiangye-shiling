using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewValley;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// Host-side preparation and recovery. Ingress supplies an actor verified for this exact native connection.
/// After reserving, only synchronized inventory and the bound owner's journal can certify an owner commit.
/// </summary>
internal sealed class HostOwnerTransaction
{
    private readonly HostReservationStore Store;
    private readonly RemoteHostPlanPreparation Planner;
    private readonly HostOwnerIntentCapacity IntentCapacity = new();
    private string? ColdPlanKey;

    internal HostOwnerTransaction(MachineService machines)
    {
        this.Store = machines.Reservations;
        this.Planner = new(machines.Recipes);
    }

    internal void Reset()
    {
        this.ColdPlanKey = null;
        this.IntentCapacity.Reset();
    }

    internal void CaptureLoadedRecord()
    {
        this.Reset();
        if (!this.Store.TryRead(out var plan) || plan is null) return;
        this.IntentCapacity.Remember((plan.Request.ActorId, plan.Request.RequestId));
        if (!Game1.getOnlineFarmers().Any(player => player.UniqueMultiplayerID == plan.Request.ActorId)) this.ColdPlanKey = plan.PlanKey;
    }

    internal void ReceiveIntent(Guid sessionId, long verifiedActor, Guid nonce, string raw, MachineRequestEnvelope intent)
    {
        if (!this.TryReadIntent(sessionId, verifiedActor, nonce, raw, intent, out var existing, out var refusal))
            return;

        var key = (verifiedActor, intent.RequestId);
        (long Actor, Guid Request)? live = existing is null ? null : (existing.Request.ActorId, existing.Request.RequestId);
        // A live grant always wins. A changed retry cannot replace it or publish a no-grant result.
        switch (this.IntentCapacity.Decide(key, live))
        {
            case HostOwnerIntentDisposition.KeepGrant: return;
            case HostOwnerIntentDisposition.Refuse: Refuse(); return;
        }

        if (!this.TryDecidePlan(sessionId, verifiedActor, raw, intent, out var plan)) return;
        if (plan is null)
        {
            Refuse();
            return;
        }

        // A failed native setter can leave a real record. Do not issue a refusal at that call site.
        // Account for the grant (including any ambiguous native write) before another request can enter.
        this.IntentCapacity.Remember(key);
        this.Store.TryReserve(plan);

        void Refuse()
        {
            this.IntentCapacity.Remember(key);
            this.Store.TryPublishRefusal(refusal);
        }
    }

    /// <summary>Only the current verified SDK/native peer may supply the challenge-bound sequence.</summary>
    internal void ReceiveDirectIpIntent(Guid sessionId, long verifiedActor, DirectIpMachineIntent message,
        DirectIpIntentSequence sequence)
    {
        var disposition = sequence.Classify(message);
        if (disposition is not (DirectIpIntentDisposition.Next or DirectIpIntentDisposition.CurrentRefused)) return;

        MachineRequestEnvelope intent = message.Request;
        string raw = message.RequestRaw;
        if (!this.TryReadIntent(sessionId, verifiedActor, message.Nonce, raw, intent, out var existing, out var refusal)) return;

        // Preserve the authoritative record even if a retry arrives over another connection or sequence.
        if (existing?.Request.ActorId == verifiedActor && existing.Request.RequestId == intent.RequestId) return;
        if (disposition == DirectIpIntentDisposition.CurrentRefused)
        {
            this.Store.TryPublishRefusal(refusal);
            return;
        }

        OwnerCommitReservation? plan = null;
        if (existing is null && !this.TryDecidePlan(sessionId, verifiedActor, raw, intent, out plan)) return;
        if (plan is null)
        {
            if (sequence.TryRecordRefusal(message)) this.Store.TryPublishRefusal(refusal);
            return;
        }

        // An ambiguous reserve consumes its sequence without a refusal. Subsequent exact retransmission
        // sees CurrentReserveAttempt and leaves the native record for Advance and cold recovery.
        if (sequence.TryBeginReserve(message)) this.Store.TryReserve(plan);
    }

    private bool TryReadIntent(Guid sessionId, long actor, Guid nonce, string raw, MachineRequestEnvelope intent,
        out OwnerCommitReservation? existing, [NotNullWhen(true)] out OwnerIntentRefusal? refusal)
    {
        existing = null;
        refusal = null;
        return intent.ActorId == actor && intent.SessionId == sessionId && this.Store.TryRead(out existing)
            && HostReservationStore.TryReadRefusal(Game1.player, actor, out _)
            && OwnerIntentRefusal.TryCreate(Game1.uniqueIDForThisGame, Game1.player.UniqueMultiplayerID, nonce, raw, out refusal)
            && refusal.Matches(Game1.uniqueIDForThisGame, Game1.player.UniqueMultiplayerID, nonce, intent, raw);
    }

    // False leaves the same intent retryable. A true result with no plan is a determinate no-grant.
    private bool TryDecidePlan(Guid sessionId, long actor, string raw, MachineRequestEnvelope intent, out OwnerCommitReservation? plan)
    {
        plan = null;
        Farmer? owner = Game1.GetPlayer(actor, onlyOnline: true);
        // A prior owner marker can still be leaving the replica. Let the same pending intent retry later.
        if (owner is null || owner.modData.ContainsKey(OwnerInventoryCommit.JournalKey)
            || !HostReservationStore.TryReadReceipt(Game1.player, actor, out var previous)) return false;
        if (previous?.RequestId != intent.RequestId && this.Planner.TryPrepare(Game1.uniqueIDForThisGame, actor,
            sessionId, raw, out var candidate) && candidate?.Request == intent)
            plan = candidate;
        return true;
    }

    internal void Advance(Guid sessionId)
    {
        if (!this.Store.TryRead(out var plan) || plan is null) return;
        this.IntentCapacity.Remember((plan.Request.ActorId, plan.Request.RequestId));
        Farmer? owner = Game1.GetPlayer(plan.Request.ActorId, onlyOnline: false);
        if (owner is null || owner.UniqueMultiplayerID != plan.Request.ActorId) return;

        // Only the initial offline view from this load can prove an untouched grant can no longer run.
        bool settled = this.ColdPlanKey == plan.PlanKey && plan.Request.SessionId != sessionId
            && !Game1.getOnlineFarmers().Any(player => player.UniqueMultiplayerID == plan.Request.ActorId);
        this.ColdPlanKey = null;
        try
        {
            SObject? machine = FindMachine(plan.Request);
            var machineState = ReadMachine(machine, plan);
            string seen = OwnerInventoryCommit.SeenJournal(plan);
            if (plan.Status is OwnerReservationStatus.Applied or OwnerReservationStatus.Cancelled
                && owner.modData.GetValueOrDefault(OwnerInventoryCommit.JournalKey) == seen)
            {
                var expected = plan.Status == OwnerReservationStatus.Applied
                    ? ReservedMachineObservation.After : ReservedMachineObservation.Before;
                if (machineState != expected)
                    this.Store.Isolate("terminal reservation disagrees with the machine");
                else this.Store.TryRetire(plan, seen);
                return;
            }

            OwnerRecoveryAction action = OwnerCommitRecovery.Decide(plan.Status, machineState,
                OwnerInventoryCommit.Observe(owner, plan.WriteSet), OwnerInventoryCommit.ObserveJournal(owner, plan), settled);
            switch (action)
            {
                case OwnerRecoveryAction.CommitMachine:
                    // Recheck the exact object and both owner observations before touching the machine value.
                    if (machine is null || !ReferenceEquals(machine, FindMachine(plan.Request))
                        || ReadMachine(machine, plan) != ReservedMachineObservation.Before || !OwnerApplied(owner, plan))
                        return;
                    machine.modData[MachineService.StateKey] = plan.AfterRaw;
                    if (!ReferenceEquals(machine, FindMachine(plan.Request))
                        || ReadMachine(machine, plan) != ReservedMachineObservation.After || !OwnerApplied(owner, plan))
                    {
                        this.Store.Isolate("machine commit verification failed");
                        return;
                    }
                    this.Store.TryRecordTerminal(plan, OwnerReservationStatus.Applied);
                    break;
                case OwnerRecoveryAction.RecordAppliedReceipt:
                    this.Store.TryRecordTerminal(plan, OwnerReservationStatus.Applied);
                    break;
                case OwnerRecoveryAction.CancelReservation:
                    this.Store.TryRecordTerminal(plan, OwnerReservationStatus.Cancelled);
                    break;
                case OwnerRecoveryAction.Isolate:
                    this.Store.Isolate("inconsistent machine or owner evidence");
                    break;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Store.Isolate(error.GetType().Name);
        }
    }

    private static SObject? FindMachine(MachineRequestEnvelope request)
    {
        GameLocation? location = Game1.getLocationFromName(request.LocationName);
        if (location?.NameOrUniqueName != request.LocationName)
            location = Game1.getLocationFromName(request.LocationName, isStructure: true);
        var tile = new Vector2(request.TileX, request.TileY);
        return location?.NameOrUniqueName == request.LocationName && location.objects.TryGetValue(tile, out SObject machine)
            && machine.TileLocation == tile && MachineService.TryGetKind(machine, out _) ? machine : null;
    }

    private static ReservedMachineObservation ReadMachine(SObject? machine, OwnerCommitReservation plan)
    {
        string? raw = machine?.modData.GetValueOrDefault(MachineService.StateKey);
        if ((raw != plan.BeforeRaw && raw != plan.AfterRaw) || !MachineService.TryRead(machine, out _))
            return ReservedMachineObservation.Conflict;
        return raw == plan.BeforeRaw ? ReservedMachineObservation.Before
            : ReservedMachineObservation.After;
    }

    private static bool OwnerApplied(Farmer owner, OwnerCommitReservation plan)
        => ReferenceEquals(owner, Game1.GetPlayer(plan.Request.ActorId, onlyOnline: false))
            && OwnerInventoryCommit.Observe(owner, plan.WriteSet) == OwnerInventoryObservation.After
            && OwnerInventoryCommit.ObserveJournal(owner, plan) == OwnerJournalObservation.Matching;
}
