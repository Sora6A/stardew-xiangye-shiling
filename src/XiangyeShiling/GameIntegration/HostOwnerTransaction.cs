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
    private const int MaxReceiptsPerSession = 256;
    private readonly HostReservationStore Store;
    private readonly RemoteHostPlanPreparation Planner;
    private readonly HashSet<(long Actor, Guid Request)> Completed = new();
    private string? ColdPlanKey;

    internal HostOwnerTransaction(MachineService machines)
    {
        this.Store = machines.Reservations;
        this.Planner = new(machines.Recipes);
    }

    internal void Reset()
    {
        this.ColdPlanKey = null;
        this.Completed.Clear();
    }

    internal void CaptureLoadedRecord()
    {
        this.Reset();
        if (this.Store.TryRead(out var plan) && plan is not null
            && !Game1.getOnlineFarmers().Any(player => player.UniqueMultiplayerID == plan.Request.ActorId))
            this.ColdPlanKey = plan.PlanKey;
    }

    internal bool TryReserve(Guid sessionId, long verifiedActor, string raw, MachineRequestEnvelope intent)
    {
        if (!this.Store.TryRead(out var existing)) return false;
        if (existing is not null)
            return existing.Request.ActorId == verifiedActor && existing.RequestRaw == raw;

        Farmer? owner = Game1.GetPlayer(verifiedActor, onlyOnline: true);
        if (owner is null || owner.modData.ContainsKey(OwnerInventoryCommit.JournalKey) || this.Completed.Count >= MaxReceiptsPerSession
            || this.Completed.Contains((verifiedActor, intent.RequestId))
            || !HostReservationStore.TryReadReceipt(Game1.player, verifiedActor, out var previous) || previous?.RequestId == intent.RequestId
            || !this.Planner.TryPrepare(Game1.uniqueIDForThisGame, verifiedActor, sessionId, raw, out var plan)
            || plan is null || plan.Request != intent)
            return false;

        return this.Store.TryReserve(plan);
    }

    internal void Advance(Guid sessionId)
    {
        if (!this.Store.TryRead(out var plan) || plan is null) return;
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
                else if (this.Store.TryRetire(plan, seen))
                    this.Completed.Add((plan.Request.ActorId, plan.Request.RequestId));
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
