using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.Machines;
using XiangyeShiling.Networking;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

/// <summary>
/// Prepares one immutable reservation from a verified ingress actor and the live host world.
/// Native reads are restricted to the creating game thread. The caller owns admission, persistence,
/// and rechecking live state before any later commit.
/// </summary>
internal sealed class RemoteHostPlanPreparation
{
    private readonly int GameThread = Environment.CurrentManagedThreadId;
    private readonly IReadOnlyList<MachineRecipe> Recipes;

    internal RemoteHostPlanPreparation(IReadOnlyList<MachineRecipe> recipes) => this.Recipes = recipes;

    internal bool TryPrepare(ulong expectedWorldId, long verifiedActorId, Guid activeSessionId, string? requestRaw,
        [NotNullWhen(true)] out OwnerCommitReservation? reservation)
    {
        reservation = null;
        if (Environment.CurrentManagedThreadId != this.GameThread || expectedWorldId == 0 || verifiedActorId == 0
            || activeSessionId == Guid.Empty || !MachineRequestCodec.TryRead(requestRaw, out var intent)
            || intent is null || intent.ActorId != verifiedActorId || intent.SessionId != activeSessionId
            || !IsBoundHost(expectedWorldId))
            return false;

        try
        {
            Farmer host = Game1.player;
            long hostId = host.UniqueMultiplayerID;
            if (hostId == 0 || hostId == verifiedActorId || !ReferenceEquals(host, Game1.MasterPlayer)) return false;
            Farmer? owner = Game1.GetPlayer(verifiedActorId, onlyOnline: true);
            if (owner is null || ReferenceEquals(owner, host) || owner.UniqueMultiplayerID != verifiedActorId
                || !Game1.getOnlineFarmers().Contains(owner)) return false;

            // The native Farmer's current location supplies the actual map. A request's name is only an exact address.
            GameLocation? location = owner.currentLocation;
            var tile = new Vector2(intent.TileX, intent.TileY);
            if (location is null || location.NameOrUniqueName != intent.LocationName || !location.isTileOnMap(tile)
                || !location.objects.TryGetValue(tile, out SObject machine) || machine.TileLocation != tile
                || !MachineService.TryGetKind(machine, out MachineKind kind)
                || !machine.modData.TryGetValue(MachineService.StateKey, out string beforeRaw)
                || !OwnerCommitReservation.TryReadMachine(beforeRaw, out var before) || before.Kind != kind
                || before.InstanceId != intent.InstanceId || before.Revision != intent.ExpectedRevision
                || before.LastRequestId == intent.RequestId)
                return false;

            Vector2 ownerTile = owner.Tile;
            var actor = new HostActorSnapshot(activeSessionId, IsOnGameThread: true, IsMainPlayer: true, IsWorldReady: true,
                ActorConnected: true, verifiedActorId, owner.UniqueMultiplayerID, location.NameOrUniqueName,
                (int)ownerTile.X, (int)ownerTile.Y, Game1.eventUp || location.currentEvent is not null || owner.UsingTool);
            var target = new HostTargetSnapshot(location.NameOrUniqueName, intent.TileX, intent.TileY, before);
            HostAuthorization authorization = HostRequestGate.Authorize(intent, actor, target);
            if (!authorization.Authorized) return false;

            bool start = intent.Action == MachineRequestAction.Start;
            MachineRecipe? recipe = start ? this.Recipes.SingleOrDefault(candidate => candidate.Id == intent.RecipeId) : null;
            int capacity = owner.MaxItems;
            if ((start && recipe is null) || capacity is <= 0 or > OwnerCommitReservation.MaxCapacity
                || owner.Items.Count != capacity) return false;
            long now = MachineService.Now();
            // Collection uses the frozen product of this batch. Only collection needs an output for native stack checks.
            Item? output = null;
            if (!start && (before.PhaseAt(now) != MachinePhase.Ready
                || !OwnerInventoryPlanning.TryCreateOutput(before.Batch?.Output, out output))) return false;
            Item[] items = owner.Items.ToArray();
            InventorySlot?[] slots = OwnerInventoryPlanning.Snapshot(items, output);
            // P1 exposes the registered recipes through their matching machine without a separate unlock.
            Decision decision = start
                ? MachineRules.Start(before, authorization.RuleRequest!, recipe!, recipeUnlocked: true, slots, now)
                : MachineRules.Collect(before, authorization.RuleRequest!, slots, now);
            if (!decision.Accepted || (start && !OwnerInventoryPlanning.TryCreateOutput(recipe!.Output, out output))) return false;
            MachineTransaction transaction = decision.Transaction!;
            if (!OwnerInventoryPlanning.TryDescribeChanges(items, transaction.Slots, output, out var writeSet, out _))
                return false;

            if (!IsBoundHost(expectedWorldId) || !ReferenceEquals(Game1.player, host) || !ReferenceEquals(Game1.MasterPlayer, host)
                || !ReferenceEquals(Game1.GetPlayer(verifiedActorId, onlyOnline: true), owner) || !Game1.getOnlineFarmers().Contains(owner)
                || !ReferenceEquals(owner.currentLocation, location) || owner.Tile != ownerTile || owner.UsingTool
                || Game1.eventUp || location.currentEvent is not null
                || !location.objects.TryGetValue(tile, out var placed) || !ReferenceEquals(placed, machine)
                || machine.TileLocation != tile || machine.modData.GetValueOrDefault(MachineService.StateKey) != beforeRaw
                || owner.MaxItems != capacity || owner.Items.Count != capacity
                || writeSet.Changes.Any(change => !ReferenceEquals(owner.Items[change.Index], items[change.Index])
                    || !OwnerInventoryFingerprint.TryReadObserved(owner.Items[change.Index], out string? observed)
                    || observed != change.Before))
                return false;

            return OwnerCommitReservation.TryCreate(expectedWorldId, hostId, Guid.NewGuid(), requestRaw,
                beforeRaw, MachineStateCodec.Write(transaction.After), writeSet, out reservation);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Failed native item/location reads cannot provide the exact evidence required by a reservation.
            return false;
        }
    }

    private static bool IsBoundHost(ulong expectedWorldId) => Context.IsWorldReady && Context.IsMainPlayer
        && Context.IsMultiplayer && !Context.IsSplitScreen && Context.ScreenId == 0 && !Game1.newDay
        && Game1.server is not null && Game1.uniqueIDForThisGame == expectedWorldId;
}
