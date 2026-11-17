using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Tools;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace XiangyeShiling.GameIntegration;

internal static partial class MachinePatches
{
    private static HostPlacement? ActiveHostPlacement;

    private static void InstallHostPlacement(Harmony harmony)
    {
        harmony.Patch(RequireMethod(typeof(Utility), "tryToPlaceItem", typeof(GameLocation), typeof(SObject), typeof(int), typeof(int)),
            prefix: PatchMethod(nameof(BeginHostPlacement)), finalizer: PatchMethod(nameof(EndHostPlacement)));
        harmony.Patch(RequireMethod(typeof(SObject), "GetOneCopyFrom", typeof(Item)),
            postfix: PatchMethod(nameof(CaptureHostPlacementClone)));
        harmony.Patch(RequireMethod(typeof(Farmer), "reduceActiveItemByOne"), prefix: PatchMethod(nameof(AuthorizeHostPlacementDebit)));

        static System.Reflection.MethodInfo RequireMethod(Type type, string method, params Type[] arguments)
            => AccessTools.Method(type, method, arguments) ?? throw new MissingMethodException(type.FullName, method);
        static HarmonyMethod PatchMethod(string name) => new(typeof(MachinePatches), name);
    }

    private static bool AllowsCoopHost(Farmer? who, GameLocation? location)
        => who is not null && location is not null && Service.AllowsLocalHostOperations()
            && ReferenceEquals(who, Game1.player) && ReferenceEquals(who, Game1.MasterPlayer)
            && ReferenceEquals(location, who.currentLocation) && ReferenceEquals(location, Game1.currentLocation);

    private static bool BlocksUnattributedRemoval(SObject? item) => BlocksNativeRemoval(item)
        || (MachineService.TryGetKind(item, out _) && Context.IsWorldReady && Context.IsMultiplayer);

    private static bool ProtectTool(SObject __instance, Tool? t, ref bool __result)
    {
        if (!MachineService.TryGetKind(__instance, out _)) return true;
        // Tool.DoFunction binds the actual farmer, including the pickaxe used by native right-click.
        if (!BlocksNativeRemoval(__instance) && (!Context.IsMultiplayer
            || (t is Axe or Pickaxe && __instance.Location is { } location && AllowsCoopHost(t.getLastFarmerToUse(), location)
                && location.objects.TryGetValue(__instance.TileLocation, out SObject placed) && ReferenceEquals(placed, __instance))))
            return true;
        __result = false;
        return false;
    }

    private static bool BeginHostPlacement(GameLocation location, SObject item, int x, int y, ref bool __result,
        out HostPlacement? __state)
    {
        __state = null;
        if (ActiveHostPlacement is not null)
        {
            __result = false;
            return false;
        }
        if (!MachineService.TryGetKind(item, out _) || !Context.IsMultiplayer) return true;

        Farmer owner = Game1.player;
        var tile = new Vector2(x / 64, y / 64);
        if (!AllowsCoopHost(owner, location) || x < 0 || y < 0 || !location.isTileOnMap(tile)
            || item.IsRecipe || item.Stack <= 0 || owner.CurrentToolIndex < 0 || owner.CurrentToolIndex >= owner.Items.Count
            || !ReferenceEquals(owner.CurrentItem, item) || !ReferenceEquals(owner.Items[owner.CurrentToolIndex], item))
        {
            __result = false;
            return false;
        }

        __state = ActiveHostPlacement = new HostPlacement(owner, location, item, x, y, tile);
        return true;
    }

    private static void CaptureHostPlacementClone(SObject __instance, Item source)
    {
        // Object.placementAction creates the map object via getOne. Capture the actual native clone
        // before it is inserted so compensation cannot remove a different object from the same tile.
        if (ActiveHostPlacement is { Entered: true, Clone: null } scope && ReferenceEquals(scope.Source, source)
            && !ReferenceEquals(__instance, source)) scope.Clone = __instance;
    }

    private static bool AuthorizeHostPlacementDebit(Farmer __instance)
    {
        if (ActiveHostPlacement is not { } scope || !ReferenceEquals(__instance, scope.Owner)) return true;
        if (scope.DebitEntered || !scope.IsAuthorized() || !scope.HasInitializedClone() || !scope.HeldUnchanged()) return false;
        scope.DebitEntered = true;
        return true;
    }

    private static Exception? EndHostPlacement(Exception? __exception, HostPlacement? __state, ref bool __result)
    {
        if (__state is not { } scope) return __exception;
        try
        {
            // Utility can report true after an Object placement returned false. For this scoped custom
            // operation success requires a verified native clone and the original selected-slot debit.
            __result = scope.DebitEntered && scope.HasDebited() && scope.HasInitializedClone();
            if (!__result && scope.Entered && !scope.TryRestore())
                Service.Reservations.Isolate("could not reconcile native host placement");
            return __exception;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Service.Reservations.Isolate("native host placement verification failed");
            return __exception ?? error;
        }
        finally { ActiveHostPlacement = null; }
    }

    private sealed class HostPlacement
    {
        internal readonly Farmer Owner;
        internal readonly SObject Source;
        private readonly IInventory Items;
        private readonly GameLocation Location;
        private readonly ulong World;
        private readonly int X, Y, Slot, Stack, Health;
        private readonly long OriginalOwner;
        private readonly string? OriginalState;
        private readonly GameLocation? OriginalLocation;
        private readonly Vector2 Tile, OriginalTile;
        internal SObject? Clone;
        internal string? InitializedRaw;
        internal bool Entered, DebitEntered;

        internal HostPlacement(Farmer owner, GameLocation location, SObject source, int x, int y, Vector2 tile)
        {
            this.Owner = owner;
            this.Location = location;
            this.Source = source;
            this.Items = owner.Items;
            this.World = Game1.uniqueIDForThisGame;
            this.X = x;
            this.Y = y;
            this.Tile = tile;
            this.Slot = owner.CurrentToolIndex;
            this.Stack = source.Stack;
            this.Health = source.getHealth();
            this.OriginalOwner = source.owner.Value;
            this.OriginalState = source.modData.GetValueOrDefault(MachineService.StateKey);
            this.OriginalLocation = source.Location;
            this.OriginalTile = source.TileLocation;
        }

        internal bool Matches(SObject source, Farmer? owner, GameLocation location, int x, int y)
            => !this.Entered && ReferenceEquals(this.Source, source) && ReferenceEquals(this.Owner, owner)
                && ReferenceEquals(this.Location, location) && this.X == x && this.Y == y
                && this.IsAuthorized() && this.HeldUnchanged();

        internal bool IsAuthorized() => AllowsCoopHost(this.Owner, this.Location) && !Service.Reservations.BlocksMachine(this.Source);

        internal bool HeldUnchanged() => this.OwnsSlot() && this.Owner.CurrentToolIndex == this.Slot
            && ReferenceEquals(this.Owner.CurrentItem, this.Source) && this.SlotUnchanged();

        internal bool HasDebited() => this.OwnsSlot() && (this.Stack == 1 ? this.Items[this.Slot] is null && this.Source.Stack == 0
            : ReferenceEquals(this.Items[this.Slot], this.Source) && this.Source.Stack == this.Stack - 1);

        internal bool TryInitializeClone()
        {
            if (!this.IsAuthorized() || !this.HeldUnchanged() || !this.HasExpectedClone()
                || !MachineService.TryGetKind(this.Clone, out var kind)) return false;
            this.InitializedRaw = MachineStateCodec.Write(MachineState.Empty(Guid.NewGuid(), kind));
            this.Clone.modData[MachineService.StateKey] = this.InitializedRaw;
            return this.HasInitializedClone() && this.HeldUnchanged();
        }

        internal bool HasInitializedClone() => this.InitializedRaw is not null && this.HasExpectedClone()
            && this.Clone!.modData.GetValueOrDefault(MachineService.StateKey) == this.InitializedRaw;

        internal bool TryRestore()
        {
            if (!this.OwnsSlot() || !this.SlotUnchanged()) return false;
            if (this.HasClone()) this.Location.objects.Remove(this.Tile);
            this.Source.Location = this.OriginalLocation;
            this.Source.TileLocation = this.OriginalTile;
            this.Source.owner.Value = this.OriginalOwner;
            this.Source.setHealth(this.Health);
            return !this.Location.objects.ContainsKey(this.Tile) && this.SlotUnchanged()
                && ReferenceEquals(this.Source.Location, this.OriginalLocation) && this.Source.TileLocation == this.OriginalTile
                && this.Source.owner.Value == this.OriginalOwner && this.Source.getHealth() == this.Health;
        }

        private bool OwnsSlot() => this.World == Game1.uniqueIDForThisGame && ReferenceEquals(this.Owner, Game1.player)
            && ReferenceEquals(this.Owner, Game1.MasterPlayer) && ReferenceEquals(this.Owner.Items, this.Items) && this.Slot < this.Items.Count;

        private bool SlotUnchanged() => ReferenceEquals(this.Items[this.Slot], this.Source) && this.Source.Stack == this.Stack
            && this.Source.modData.GetValueOrDefault(MachineService.StateKey) == this.OriginalState;

        private bool HasClone() => this.Clone is not null && this.Location.objects.TryGetValue(this.Tile, out SObject current)
            && ReferenceEquals(current, this.Clone);

        private bool HasExpectedClone() => this.HasClone() && this.Clone!.QualifiedItemId == this.Source.QualifiedItemId
            && this.Clone.TileLocation == this.Tile && ReferenceEquals(this.Clone.Location, this.Location)
            && this.Clone.owner.Value == this.Owner.UniqueMultiplayerID;
    }
}
