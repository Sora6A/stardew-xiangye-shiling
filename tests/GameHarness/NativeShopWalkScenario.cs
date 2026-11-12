using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Pathfinding;
using StardewValley.Util;
using XiangyeShiling.GameIntegration;

namespace GameHarness;

/// <summary>
/// Drives only the original movement and action controls in a disposable farm. Pathfinding picks waypoints,
/// while the native update loop decides actual movement, map changes, opening hours and counter access.
/// </summary>
internal sealed class NativeShopWalkScenario : IInputSimulator
{
    internal const string FarmName = "XYSP1WalkShop";
    private const string Soybean = "(O)" + MachineService.Prefix + "_Soybean";
    private static readonly string[] Maps = { "FarmHouse", "Farm", "BusStop", "Town", "SeedShop" };
    private static readonly Point Below = new(0, 1);
    private static readonly FieldInfo Simulator = AccessTools.Field(typeof(Game1), "inputSimulator");
    private static NativeShopWalkScenario? Observer;

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Config;
    private readonly Harmony Patch = new(MachineService.Prefix + ".GameHarness.NativeShopWalk");
    private readonly Stopwatch Watch = new();
    private readonly List<object> Legs = new(), Actions = new();
    private readonly List<int> Clock = new();
    private Options? Options;
    private bool OriginalPause, Loaded, Started, Finished, SavingObserved, ActionQueued, ActionSent, AwaitingArrival;
    private int LoadDelay = -1, Leg, Direction = -1, LastDirection = -1, InputFrames, StartingMoney, StartingSoybeans;
    private Vector2 LastPosition;
    private float Walked;
    private Stack<Point>? Waypoints;
    private Point Target, Standing;
    private int DoorOpens, DoorCloses;
    private string? Failure, SkippedEvent;
    private TimeSpan ActionSentAt;
    private static Farmer Player => Game1.player;
    private string SaveName => $"{FarmName}_{this.Config.SaveFixtureId}";

    internal NativeShopWalkScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && config.Scenario == "NativeShopWalk" && config.FarmName == FarmName
            && config.SaveFixtureId != 0 && !config.OpenPreview, "Bind NativeShopWalk to a dedicated XYSP1WalkShop save.");
        this.Helper = helper;
        this.Monitor = monitor;
        this.Config = config;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.Loaded = true;
    internal void OnTimeChanged(int time) { if (this.Started && !this.Finished) this.Clock.Add(time); }
    internal void OnSaving()
    {
        this.SavingObserved = true;
        this.Failure = "A native save occurred during the walk.";
    }
    internal void OnReturnedToTitle() { if (this.Started) this.Failure = "The walk returned to title."; }

    internal void Tick()
    {
        if (this.Finished) { Game1.game1.Exit(); return; }
        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, this.SaveName, this.SaveName)),
                    "Dedicated walking fixture is absent or a game is already active.");
                Game1.activeClickableMenu = null;
                this.Watch.Start();
                SaveGame.Load(this.SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "Walk failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(8), "Timed out waiting for the native walking route.");
            if (this.Loaded && !this.Started && Context.IsPlayerFree) this.Start();
            if (!this.Started) return;
            this.CheckWorld();
            Require(ReferenceEquals(Simulator.GetValue(null), this), "The native simulator was replaced during the walk.");
            string map = Player.currentLocation.Name;
            if (this.Leg + 1 < Maps.Length && map == Maps[this.Leg + 1])
            {
                // The native warp can update currentLocation before SMAPI publishes its Warped event.
                this.Direction = -1;
                return;
            }
            Require(map == Maps[this.Leg], "Unexpected current map during the walk: " + map);

            // A warp's new map can be published before the native landing position. Never count that delta as walking.
            if (!this.AwaitingArrival && !Game1.eventUp) this.Walked += Vector2.Distance(this.LastPosition, Player.Position);
            this.LastPosition = Player.Position;
            this.Direction = -1;
            if (Game1.fadeToBlack || Game1.isWarping) return;
            this.AwaitingArrival = Game1.eventUp;
            if (Game1.activeClickableMenu is ShopMenu shop)
            {
                this.Buy(shop);
                this.Finish(null);
                return;
            }
            if (map == "SeedShop" && this.SkippedEvent is null && Game1.eventUp && !Game1.isQuestion
                && Game1.activeClickableMenu is DialogueBox && Game1.CurrentEvent is { id: "17", skippable: true } arrivalEvent)
            {
                // This legacy fixture triggers the original Caroline/Abigail scene on entering the shop.
                // Use the game's skip path and restart walking evidence after the scene releases control.
                this.SkippedEvent = arrivalEvent.id;
                arrivalEvent.skipEvent();
                this.Waypoints = null;
                return;
            }
            if (Game1.activeClickableMenu is { } menu)
                throw new InvalidOperationException($"Unexpected {menu.GetType().Name} on {map} at {Game1.timeOfDay}"
                    + $" (event {Game1.CurrentEvent?.id ?? "none"}, command {Game1.CurrentEvent?.CurrentCommand.ToString() ?? "none"},"
                    + $" dialogue {Game1.dialogueUp}, question {Game1.isQuestion}, tile {Player.TilePoint}).");
            if (Game1.eventUp || !Player.CanMove) return;
            Require(!this.ActionSent || this.Watch.Elapsed - this.ActionSentAt < TimeSpan.FromSeconds(10),
                $"The native action on {map} at {Player.TilePoint} did not open the door or the counter.");
            if (this.Waypoints is null) this.Plan(Player.currentLocation);
            while (this.Waypoints!.TryPeek(out Point tile))
            {
                if (this.Steer(tile)) return;
                this.Waypoints.Pop();
            }

            if (this.Leg < 3)
            {
                // Native maps can place an exit just outside the playable bounds.
                if (this.Target != this.Standing) this.Steer(this.Target);
                return;
            }
            if (this.Leg == 3 && Game1.timeOfDay < this.DoorOpens) return;
            Require(Game1.timeOfDay < this.DoorCloses, "The native shop's opening window elapsed.");
            if (this.Leg == 4 && !PierreAtCounter()) return;
            if (Player.FacingDirection != 0) { this.Direction = 0; return; }
            if (!this.ActionSent) this.ActionQueued = true;
        }
        catch (Exception error) { this.Finish(error.Message); }
    }

    private void Start()
    {
        this.CheckWorld();
        Require(Player.currentLocation.Name == Maps[0] && Game1.dayOfMonth == 4 && Game1.season == Season.Spring
            && Game1.timeOfDay < 900 && !Game1.eventUp && Player.Money >= 60,
            "Use the spring 4 morning fixture with funds, inside the farmhouse.");
        Require(Observer is null && Simulator.GetValue(null) is null, "Another native input simulator is active.");
        this.Patch.Patch(AccessTools.Method(typeof(GameLocation), "performAction",
            new[] { typeof(string), typeof(Farmer), typeof(xTile.Dimensions.Location) }),
            postfix: new HarmonyMethod(typeof(NativeShopWalkScenario), nameof(ObserveAction)));
        Observer = this;
        Simulator.SetValue(null, this);
        this.Options = Game1.options;
        this.OriginalPause = this.Options.pauseWhenOutOfFocus;
        this.Options.pauseWhenOutOfFocus = false;
        Player.Items.OverwriteWith(new Item[Player.MaxItems]);
        this.StartingMoney = Player.Money;
        this.StartingSoybeans = Player.Items.CountId(Soybean);
        this.LastPosition = Player.Position;
        this.Clock.Add(Game1.timeOfDay);
        this.Started = true;
    }

    internal void OnWarped(GameLocation oldLocation, GameLocation newLocation)
    {
        if (!this.Started || this.Finished) return;
        if (this.Leg + 1 >= Maps.Length || oldLocation.Name != Maps[this.Leg] || newLocation.Name != Maps[this.Leg + 1]
            || this.InputFrames == 0 || this.Walked < 16 || (this.Leg == 3 && !this.ActionSent))
        {
            this.Failure = $"Unexpected native map change from {oldLocation.Name} to {newLocation.Name}.";
            return;
        }
        this.Legs.Add(new { From = oldLocation.Name, To = newLocation.Name, Time = Game1.timeOfDay, this.Walked, this.InputFrames });
        this.Leg++;
        this.Waypoints = null;
        this.Direction = -1;
        this.LastDirection = -1;
        this.ActionQueued = this.ActionSent = false;
        this.AwaitingArrival = true;
        this.LastPosition = Player.Position;
        this.Walked = 0;
        this.InputFrames = 0;
    }

    private void Plan(GameLocation location)
    {
        var bounds = location.Map.Layers[0];
        if (this.Leg < 3)
        {
            string next = Maps[this.Leg + 1];
            foreach (Warp exit in location.warps.Where(exit => exit.TargetName == next && !exit.npcOnly.Value))
            {
                this.Target = new Point(exit.X, exit.Y);
                this.Standing = new Point(Math.Clamp(exit.X, 0, bounds.LayerWidth - 1), Math.Clamp(exit.Y, 0, bounds.LayerHeight - 1));
                this.Waypoints = PathTo(this.Standing);
                if (this.Waypoints is not null) return;
            }
        }
        else
        {
            for (int y = 0; y + 1 < bounds.LayerHeight; y++)
            for (int x = 0; x < bounds.LayerWidth; x++)
            {
                string[]? action = location.doesTileHaveProperty(x, y, "Action", "Buildings")?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool wanted = this.Leg == 3
                    ? action is { Length: >= 6 } && action[0] == "LockedDoorWarp" && action[3] == "SeedShop"
                        && int.TryParse(action[4], out this.DoorOpens) && int.TryParse(action[5], out this.DoorCloses)
                    : action is { Length: > 0 } && action[0] == "Buy";
                if (!wanted) continue;
                this.Target = new Point(x, y);
                this.Standing = this.Target + Below;
                this.Waypoints = PathTo(this.Standing);
                if (this.Waypoints is not null) return;
            }
        }
        throw new InvalidOperationException($"No reachable native route waypoint on {location.Name}.");

        Stack<Point> PathTo(Point tile) => PathFindController.findPath(Player.TilePoint, tile,
            PathFindController.isAtEndPoint, location, Player, 4096);
    }

    private bool Steer(Point tile)
    {
        int x = tile.X * 64 + 32 - Player.StandingPixel.X;
        int y = tile.Y * 64 + 32 - Player.StandingPixel.Y;
        if (Math.Abs(x) <= 6 && Math.Abs(y) <= 6) return false;
        this.Direction = Math.Abs(x) > Math.Abs(y) ? (x > 0 ? 1 : 3) : (y > 0 ? 2 : 0);
        return true;
    }

    public void SimulateInput(ref bool actionButtonPressed, ref bool switchToolButtonPressed, ref bool useToolButtonPressed,
        ref bool useToolButtonReleased, ref bool addItemToInventoryButtonPressed, ref bool cancelButtonPressed,
        ref bool moveUpPressed, ref bool moveRightPressed, ref bool moveLeftPressed, ref bool moveDownPressed,
        ref bool moveUpReleased, ref bool moveRightReleased, ref bool moveLeftReleased, ref bool moveDownReleased,
        ref bool moveUpHeld, ref bool moveRightHeld, ref bool moveLeftHeld, ref bool moveDownHeld)
    {
        actionButtonPressed = this.ActionQueued;
        if (this.ActionQueued)
        {
            this.ActionSent = true;
            this.ActionSentAt = this.Watch.Elapsed;
        }
        this.ActionQueued = false;
        moveUpHeld = this.Direction == 0; moveUpPressed = moveUpHeld && this.LastDirection != 0; moveUpReleased = !moveUpHeld && this.LastDirection == 0;
        moveRightHeld = this.Direction == 1; moveRightPressed = moveRightHeld && this.LastDirection != 1; moveRightReleased = !moveRightHeld && this.LastDirection == 1;
        moveDownHeld = this.Direction == 2; moveDownPressed = moveDownHeld && this.LastDirection != 2; moveDownReleased = !moveDownHeld && this.LastDirection == 2;
        moveLeftHeld = this.Direction == 3; moveLeftPressed = moveLeftHeld && this.LastDirection != 3; moveLeftReleased = !moveLeftHeld && this.LastDirection == 3;
        if (this.Direction >= 0) this.InputFrames++;
        this.LastDirection = this.Direction;
    }

    private static void ObserveAction(GameLocation __instance, string fullActionString, Farmer who,
        xTile.Dimensions.Location tileLocation, bool __result)
    {
        if (Observer is not { } current || !current.Started || current.Finished || !__result || !ReferenceEquals(who, Player)) return;
        string token = fullActionString.Split(' ')[0];
        if ((__instance.Name, token) is not (("Town", "LockedDoorWarp") or ("SeedShop", "Buy"))
            || tileLocation.X != current.Target.X || tileLocation.Y != current.Target.Y) return;
        current.Actions.Add(new { Map = __instance.Name, Action = fullActionString, tileLocation.X, tileLocation.Y, Time = Game1.timeOfDay });
    }

    private void Buy(ShopMenu shop)
    {
        Require(this.Leg == 4 && this.ActionSent && this.Walked >= 16 && this.InputFrames > 0 && this.Clock.Count > 1
            && this.Actions.Count == 2 && shop.ShopId == "SeedShop" && Game1.timeOfDay >= this.DoorOpens && Game1.timeOfDay < this.DoorCloses
            && PierreAtCounter(), "Missing the native door, walking, shop hours or Pierre counter evidence.");
        var item = shop.forSale.Single(item => item.QualifiedItemId == Soybean && !item.IsRecipe);
        Require(shop.itemPriceAndStock[item].Price == 60 && shop.heldItem is null, "Unexpected native soybean stock.");
        var purchase = AccessTools.Method(typeof(ShopMenu), "tryToPurchaseItem");
        purchase.Invoke(shop, new object?[] { item, null, 1, 0, 0 });
        Require(shop.heldItem is Item held && held.QualifiedItemId == Soybean && held.Stack == 1
            && Player.addItemToInventory(held) is null, "The native shop did not deliver the purchased soybean.");
        shop.heldItem = null;
        Require(Player.Money == this.StartingMoney - 60 && Player.Items.CountId(Soybean) == this.StartingSoybeans + 1,
            "Wrong final inventory or funds after the native shop purchase.");
    }

    private static bool PierreAtCounter() => Game1.getCharacterFromName("Pierre") is { } owner
        && ReferenceEquals(owner.currentLocation, Player.currentLocation) && owner.TilePoint == new Point(4, 17);

    private void CheckWorld() => Require(Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && Constants.SaveFolderName == this.SaveName && Game1.uniqueIDForThisGame == this.Config.SaveFixtureId
        && Player.farmName.Value == FarmName, "The active world does not match the dedicated walking fixture.");

    private void Finish(string? failure)
    {
        this.Finished = true;
        this.Failure = failure;
        if (ReferenceEquals(Simulator.GetValue(null), this)) Simulator.SetValue(null, null);
        if (ReferenceEquals(Observer, this)) Observer = null;
        this.Patch.UnpatchAll(this.Patch.Id);
        if (this.Options is not null) this.Options.pauseWhenOutOfFocus = this.OriginalPause;
        if (failure is not null) this.Monitor.Log(failure, LogLevel.Error);
        this.Helper.Data.WriteJsonFile("native-shop-walk-result.json", new
        {
            SchemaVersion = 1, Completed = failure is null && !this.SavingObserved, this.Failure, this.SavingObserved,
            ProcessId = Environment.ProcessId, FixtureId = this.Config.SaveFixtureId, this.Leg, this.Legs, this.Clock,
            this.Actions, this.Walked, this.InputFrames, this.Standing, this.Target, this.SkippedEvent,
            MoneySpent = this.Started ? this.StartingMoney - Player.Money : 0,
            Soybeans = this.Started ? Player.Items.CountId(Soybean) : 0,
            Preparation = "The harness cleared the disposable backpack, disabled pause while unfocused, and supplied movement and action through the native input simulator."
        });
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
