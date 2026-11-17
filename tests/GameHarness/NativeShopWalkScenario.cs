using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
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
    internal const string ThreeShopFarmName = "XYSP1WalkThree";
    private const string Id = MachineService.Prefix;
    private const string Soybean = "(O)" + Id + "_Soybean", Wheat = "(O)262", Toon = "(O)" + Id + "_ChineseToon";
    private static readonly string[] PierreMaps = { "FarmHouse", "Farm", "BusStop", "Town", "SeedShop" };
    private static readonly string[] ThreeShopMaps = { "FarmHouse", "Farm", "BusStop", "Town", "SeedShop", "Town",
        "Mountain", "ScienceHouse", "Mountain", "Town", "Saloon" };
    private static readonly string[] CompleteMaps = ThreeShopMaps.Concat(new[] { "Town", "BusStop", "Farm", "FarmHouse" }).ToArray();
    private static readonly ShopStop[] Shops = {
        new("SeedShop", "SeedShop", "Buy", "Pierre", new[] { new Purchase(Soybean, 6, 60), new Purchase(Wheat, 2, 25) }),
        new("ScienceHouse", "Carpenter", "Carpenter", "Robin", new[] {
            new Purchase("(BC)" + Id + "_BeanWorkshop", 1, 1500, "BeanWorkshop"),
            new Purchase("(BC)" + Id + "_FermentationCrock", 1, 1500, "FermentationCrock") }),
        new("Saloon", "Saloon", "Saloon", "Gus", new[] {
            new Purchase(Toon, 1, 100), new Purchase("(O)" + Id + "_ToonTofu", 1, 1000, "ToonTofu") })
    };
    private static readonly Point Below = new(0, 1);
    private static readonly FieldInfo Simulator = AccessTools.Field(typeof(Game1), "inputSimulator");
    private static NativeShopWalkScenario? Observer;

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Config;
    private readonly NativeShopRoutePersistence? Persistence;
    private readonly Harmony Patch = new(MachineService.Prefix + ".GameHarness.NativeShopWalk");
    private readonly Stopwatch Watch = new();
    private readonly List<object> Legs = new(), Actions = new();
    private readonly Dictionary<string, object> Visits = new();
    private readonly List<int> Clock = new();
    private Options? Options;
    private bool OriginalPause, Loaded, Started, Finished, SavingObserved, ActionQueued, ActionSent, AwaitingArrival, EnteredByDoor;
    private int LoadDelay = -1, Leg, Direction = -1, LastDirection = -1, InputFrames, StartingMoney, CurrentActions;
    private Vector2 LastPosition;
    private float Walked;
    private Stack<Point>? Waypoints;
    private Point Target, Standing;
    private int DoorOpens, DoorCloses;
    private string? Failure, SkippedEvent;
    private DialogueBox? AnsweredCarpenter;
    private AutomatedRouteScenario? Processing;
    private object? HomeEntry, CompletedWalk;
    private TimeSpan ActionSentAt;
    private static Farmer Player => Game1.player;
    private bool CompleteRoute => this.Config.Scenario == AutomatedRouteScenario.PurchasedRouteScenario || this.Persistence is not null;
    private bool FullRoute => this.Config.FarmName == ThreeShopFarmName || this.CompleteRoute;
    private string[] Maps => this.CompleteRoute ? CompleteMaps : this.FullRoute ? ThreeShopMaps : PierreMaps;
    private string SaveName => $"{this.Config.FarmName}_{this.Config.SaveFixtureId}";
    private string CurrentMap => this.Maps[this.Leg];
    private string? NextMap => this.Leg + 1 < this.Maps.Length ? this.Maps[this.Leg + 1] : null;
    private ShopStop? Counter => Shops.FirstOrDefault(shop => shop.Map == this.CurrentMap && !this.Visits.ContainsKey(shop.Map));
    private bool OpeningDoor => this.NextMap is "SeedShop" or "ScienceHouse" or "Saloon";
    private bool OpeningHome => this.CompleteRoute && this.CurrentMap == "Farm" && this.NextMap == "FarmHouse";

    private sealed record ShopStop(string Map, string ShopId, string Token, string Owner, Purchase[] Lines);
    private sealed record Purchase(string ItemId, int Quantity, int Price, string? Recipe = null);

    internal NativeShopWalkScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && config.SaveFixtureId != 0 && !config.OpenPreview
            && (config.Scenario == "NativeShopWalk" && config.FarmName is FarmName or ThreeShopFarmName
                || config.Scenario == AutomatedRouteScenario.PurchasedRouteScenario
                    && config.FarmName == AutomatedRouteScenario.PurchasedRouteFarm
                    && config.SaveFixtureId == AutomatedRouteScenario.PurchasedRouteFixtureId
                || NativeShopRoutePersistence.IsBound(config)),
            "Bind the walking scenario to its dedicated fixture.");
        this.Helper = helper;
        this.Monitor = monitor;
        this.Config = config;
        if (NativeShopRoutePersistence.IsBound(config))
            this.Persistence = new NativeShopRoutePersistence(helper, monitor, config.NativeRecoveryPhase == "Verify");
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.Loaded = true;
    internal void OnTimeChanged(int oldTime, int newTime)
    {
        if (!this.Started || this.Finished || this.Failure is not null) return;
        if (this.Processing is null) this.Clock.Add(newTime);
        else this.Processing.OnTimeChanged(oldTime, newTime);
    }
    internal void OnSaving()
    {
        this.SavingObserved = true;
        if (this.Persistence is not null)
        {
            try { this.Persistence.OnSaving(); }
            catch (InvalidOperationException error) { this.Failure = error.Message; }
            return;
        }
        this.Failure = "A native save occurred during the walk.";
        this.Processing?.OnSaving();
    }
    internal void OnSaved()
    {
        try { this.Persistence?.OnSaved(); }
        catch (InvalidOperationException error) { this.Failure = error.Message; }
    }
    internal void OnReturnedToTitle()
    {
        if (this.Loaded) this.Failure = "The route returned to title.";
        this.Processing?.OnReturnedToTitle();
    }

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
            // The persistence world's normal Gus schedule can delay the last purchase until the evening.
            int timeoutMinutes = this.Persistence is { Verifying: false } ? 17 : this.CompleteRoute ? 12 : 8;
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(timeoutMinutes), "Timed out waiting for the native route.");
            if (this.Persistence is { Verifying: true } cold)
            {
                if (!this.Loaded || !Context.IsPlayerFree) return;
                this.CheckWorld();
                cold.VerifyColdLoad();
                this.Finish(null);
                return;
            }
            if (this.Persistence is { SleepStarted: true } bedtime)
            {
                if (!Context.IsWorldReady) return;
                this.CheckWorld();
                if (bedtime.TickBedtime()) this.Finish(null);
                return;
            }
            if (this.Loaded && !this.Started && Context.IsPlayerFree) this.Start();
            if (!this.Started) return;
            this.CheckWorld();
            if (this.Processing is not null)
            {
                this.Processing.Tick();
                if (!this.Processing.IsFinished) return;
                if (this.Persistence is null || this.Processing.Failure is not null) this.Finish(this.Processing.Failure);
                else if (Context.IsPlayerFree) this.Persistence.BeginBedtime(this.Processing.ReadCompletedMachines());
                return;
            }
            Require(ReferenceEquals(Simulator.GetValue(null), this), "The native simulator was replaced during the walk.");
            string map = Player.currentLocation.Name;
            if (map == this.NextMap)
            {
                // The native warp can update currentLocation before SMAPI publishes its Warped event.
                this.Direction = -1;
                return;
            }
            Require(map == this.CurrentMap, "Unexpected current map during the walk: " + map);
            if (this.CompleteRoute && this.NextMap is null)
            {
                if (!Context.IsPlayerFree || Game1.eventUp || Game1.fadeToBlack || Game1.isWarping) return;
                Require(this.HomeEntry is not null && this.Legs.Count == CompleteMaps.Length - 1,
                    "Missing the native farmhouse entry or return walk.");
                this.CompletedWalk = this.CreateWalkEvidence(completed: true);
                this.DetachWalkControls();
                this.Processing = new AutomatedRouteScenario(this.Helper, this.Monitor, this.Config);
                this.Processing.BeginPurchasedRoute(this.StartingMoney);
                return;
            }
            ShopStop? counter = this.Counter;

            // A warp's new map can be published before the native landing position. Never count that delta as walking.
            if (!this.AwaitingArrival && !Game1.eventUp) this.Walked += Vector2.Distance(this.LastPosition, Player.Position);
            this.LastPosition = Player.Position;
            this.Direction = -1;
            if (Game1.fadeToBlack || Game1.isWarping) return;
            this.AwaitingArrival = Game1.eventUp;
            if (Game1.activeClickableMenu is ShopMenu shop)
            {
                Require(counter is not null, "A native shop opened outside a planned counter stop.");
                this.Buy(shop, counter!);
                if (this.NextMap is null) this.Finish(null);
                else
                {
                    shop.exitThisMenu(playSound: false);
                    this.ResetLegProgress(awaitingArrival: false);
                }
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
            if (counter?.Owner == "Robin" && this.ActionSent && this.CurrentActions == 1 && !Game1.eventUp
                && Game1.activeClickableMenu is DialogueBox { isQuestion: true } carpenter)
            {
                this.AnswerCarpenter(carpenter);
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

            if (!this.OpeningDoor && !this.OpeningHome && counter is null)
            {
                // Native maps can place an exit just outside the playable bounds.
                if (this.Target != this.Standing) this.Steer(this.Target);
                return;
            }
            if (!this.OpeningHome)
            {
                if (Game1.timeOfDay < this.DoorOpens) return;
                Require(Game1.timeOfDay < this.DoorCloses, "The native shop's opening window elapsed.");
            }
            if (counter is not null && !this.OwnerAtCounter(counter)) return;
            if (Player.FacingDirection != 0) { this.Direction = 0; return; }
            if (!this.ActionSent) this.ActionQueued = true;
        }
        catch (Exception error) { this.Finish(error.Message); }
    }

    private void Start()
    {
        this.CheckWorld();
        Require(Player.currentLocation.Name == this.CurrentMap && Game1.dayOfMonth == 4 && Game1.season == Season.Spring
            && Game1.timeOfDay < 900 && !Game1.eventUp && Player.Money >= (this.FullRoute ? 4510 : 60),
            "Use the spring 4 morning fixture with funds, inside the farmhouse.");
        if (this.FullRoute)
            Require(Shops.SelectMany(shop => shop.Lines).All(line => line.Recipe is null || !Player.knowsRecipe(RecipeId(line.Recipe))),
                "Use a three-shop fixture with the prototype recipes unlearned.");
        Require(Observer is null && Simulator.GetValue(null) is null, "Another native input simulator is active.");
        this.Patch.Patch(AccessTools.Method(typeof(GameLocation), "performAction",
            new[] { typeof(string), typeof(Farmer), typeof(xTile.Dimensions.Location) }),
            postfix: new HarmonyMethod(typeof(NativeShopWalkScenario), nameof(ObserveAction)));
        if (this.CompleteRoute)
            this.Patch.Patch(AccessTools.Method(typeof(Building), nameof(Building.OnUseHumanDoor)),
                postfix: new HarmonyMethod(typeof(NativeShopWalkScenario), nameof(ObserveHomeDoor)));
        Observer = this;
        Simulator.SetValue(null, this);
        this.Options = Game1.options;
        this.OriginalPause = this.Options.pauseWhenOutOfFocus;
        this.Options.pauseWhenOutOfFocus = false;
        Player.Items.OverwriteWith(new Item[Player.MaxItems]);
        this.StartingMoney = Player.Money;
        this.LastPosition = Player.Position;
        this.Clock.Add(Game1.timeOfDay);
        this.Started = true;
    }

    internal void OnWarped(GameLocation oldLocation, GameLocation newLocation)
    {
        if (!this.Started || this.Finished || this.Persistence?.SleepStarted == true) return;
        if (oldLocation.Name != this.CurrentMap || newLocation.Name != this.NextMap || this.Counter is not null
            || this.InputFrames == 0 || this.Walked < 16 || (this.OpeningDoor && !this.ActionSent)
            || (this.OpeningHome && this.HomeEntry is null))
        {
            this.Failure = $"Unexpected native map change from {oldLocation.Name} to {newLocation.Name}.";
            return;
        }
        this.Legs.Add(new { From = oldLocation.Name, To = newLocation.Name, Time = Game1.timeOfDay, this.Walked, this.InputFrames });
        if (this.CompleteRoute)
            this.Monitor.Log($"NativeShopWalkComplete: {oldLocation.Name} to {newLocation.Name} at {Game1.timeOfDay}.", LogLevel.Info);
        this.EnteredByDoor = this.OpeningDoor && this.CurrentActions == 1;
        this.Leg++;
        this.ResetLegProgress(awaitingArrival: true);
    }

    private void ResetLegProgress(bool awaitingArrival)
    {
        this.Waypoints = null;
        this.Direction = -1;
        this.LastDirection = -1;
        this.ActionQueued = this.ActionSent = false;
        this.AwaitingArrival = awaitingArrival;
        this.LastPosition = Player.Position;
        this.Walked = 0;
        this.InputFrames = 0;
        this.CurrentActions = 0;
    }

    private void Plan(GameLocation location)
    {
        var bounds = location.Map.Layers[0];
        ShopStop? counter = this.Counter;
        if (this.OpeningHome)
        {
            Building farmhouse = Game1.getFarm().GetMainFarmHouse()
                ?? throw new InvalidOperationException("The native farmhouse building is missing.");
            this.Target = farmhouse.getPointForHumanDoor();
            this.Standing = this.Target + Below;
            this.Waypoints = PathTo(this.Standing);
            if (this.Waypoints is not null) return;
        }
        else if (!this.OpeningDoor && counter is null)
        {
            foreach (Warp exit in location.warps.Where(exit => exit.TargetName == this.NextMap && !exit.npcOnly.Value))
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
                // ScienceHouse also has a friendship-gated bedroom door. Use its public front entrance.
                bool wanted = this.OpeningDoor
                    ? action is { Length: 6 } && action[0] == "LockedDoorWarp" && action[3] == this.NextMap
                        && int.TryParse(action[4], out this.DoorOpens) && int.TryParse(action[5], out this.DoorCloses)
                    : action is { Length: > 0 } && action[0] == counter!.Token;
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
        string? expected = current.OpeningDoor ? "LockedDoorWarp" : current.Counter?.Token;
        if (expected != token || __instance.Name != current.CurrentMap
            || tileLocation.X != current.Target.X || tileLocation.Y != current.Target.Y) return;
        current.CurrentActions++;
        current.Actions.Add(new { Map = __instance.Name, Action = fullActionString, tileLocation.X, tileLocation.Y, Time = Game1.timeOfDay });
    }

    private static void ObserveHomeDoor(Building __instance, Farmer who, bool __result)
    {
        if (Observer is not { Started: true, Finished: false, OpeningHome: true, ActionSent: true } current
            || !__result || !ReferenceEquals(who, Player) || !ReferenceEquals(__instance, Game1.getFarm().GetMainFarmHouse())
            || __instance.getPointForHumanDoor() != current.Target) return;
        current.HomeEntry = new { Map = current.CurrentMap, current.Target.X, current.Target.Y, Time = Game1.timeOfDay };
    }

    private void AnswerCarpenter(DialogueBox dialogue)
    {
        Require(this.Watch.Elapsed - this.ActionSentAt < TimeSpan.FromSeconds(10), "The native Carpenter dialogue did not open its shop.");
        if (ReferenceEquals(this.AnsweredCarpenter, dialogue) || dialogue.transitioning || dialogue.safetyTimer > 0) return;
        int choice = Array.FindIndex(dialogue.responses, response => response.responseKey == "Shop");
        Require(choice >= 0, "The native Carpenter dialogue lacks a Shop response.");
        ClickableComponent? component = dialogue.responseCC?.SingleOrDefault(response => response.myID == choice);
        if (component is null) return;
        Point point = component.bounds.Center;
        dialogue.performHoverAction(point.X, point.Y);
        if (dialogue.selectedResponse != choice) return;
        dialogue.receiveLeftClick(point.X, point.Y);
        if (dialogue.transitioning || !ReferenceEquals(Game1.activeClickableMenu, dialogue)) this.AnsweredCarpenter = dialogue;
    }

    private void Buy(ShopMenu shop, ShopStop counter)
    {
        Require(this.EnteredByDoor && this.ActionSent && this.Walked >= 16 && this.InputFrames > 0 && this.Clock.Count > 1
            && this.CurrentActions == 1 && shop.ShopId == counter.ShopId && Game1.timeOfDay >= this.DoorOpens
            && Game1.timeOfDay < this.DoorCloses && this.OwnerAtCounter(counter),
            $"Missing the native door, walking, shop hours or {counter.Owner} counter evidence.");
        Purchase[] lines = this.FullRoute ? counter.Lines : new[] { new Purchase(Soybean, 1, 60) };
        int before = Player.Money;
        foreach (Purchase line in lines) BuyLine(shop, line);
        int spent = before - Player.Money;
        Require(spent == lines.Sum(line => line.Price * line.Quantity), "Wrong total native shop payment.");
        this.Visits.Add(counter.Map, new { counter.ShopId, counter.Owner, Time = Game1.timeOfDay, this.Walked, this.InputFrames,
            this.DoorOpens, this.DoorCloses, Spent = spent, Purchases = lines,
            CarpenterShopAnswer = counter.Owner == "Robin" && this.AnsweredCarpenter is not null });
        if (this.Visits.Count < (this.FullRoute ? Shops.Length : 1)) return;
        Require(this.FullRoute ? this.Visits.Count == 3 && this.Actions.Count == 6 && this.StartingMoney - Player.Money == 4510
            && Player.Items.CountId(Soybean) == 6 && Player.Items.CountId(Wheat) == 2 && Player.Items.CountId(Toon) == 1
            && Player.Items.Where(item => item is not null).All(item => item.QualifiedItemId is Soybean or Wheat or Toon)
            && Shops.SelectMany(stop => stop.Lines).All(line => line.Recipe is null || Player.knowsRecipe(RecipeId(line.Recipe)))
            : this.Visits.Count == 1 && this.Actions.Count == 2 && this.StartingMoney - Player.Money == 60
                && Player.Items.CountId(Soybean) == 1, "Wrong final purchases, inventory or recipes after the native route.");
    }

    private static void BuyLine(ShopMenu shop, Purchase line)
    {
        string? recipe = line.Recipe is null ? null : RecipeId(line.Recipe);
        var item = shop.forSale.Single(item => item.QualifiedItemId == line.ItemId && item.IsRecipe == (recipe is not null));
        Require(shop.itemPriceAndStock[item].Price == line.Price && shop.heldItem is null
            && (recipe is null || !Player.knowsRecipe(recipe)), "Unexpected native stock or recipe state for " + line.ItemId);
        int money = Player.Money;
        int quantity = Player.Items.CountId(line.ItemId);
        var purchase = AccessTools.Method(typeof(ShopMenu), "tryToPurchaseItem");
        purchase.Invoke(shop, new object?[] { item, null, line.Quantity, 0, 0 });
        if (recipe is null)
        {
            Require(shop.heldItem is Item held && held.QualifiedItemId == line.ItemId && held.Stack == line.Quantity
                && Player.addItemToInventory(held) is null, "The native shop did not deliver " + line.ItemId);
            shop.heldItem = null;
        }
        Require(Player.Money == money - line.Price * line.Quantity
            && Player.Items.CountId(line.ItemId) == quantity + (recipe is null ? line.Quantity : 0)
            && (recipe is null || Player.knowsRecipe(recipe)), "Wrong native payment, quantity or learned recipe for " + line.ItemId);
    }

    private bool OwnerAtCounter(ShopStop counter)
    {
        if (Game1.getCharacterFromName(counter.Owner) is not { } owner || !ReferenceEquals(owner.currentLocation, Player.currentLocation))
            return false;
        return counter.Owner switch
        {
            "Pierre" => owner.TilePoint == new Point(4, 17),
            "Robin" => Vector2.Distance(owner.Tile, this.Target.ToVector2()) <= 3,
            "Gus" => new Rectangle(9, 17, 10, 2).Contains(owner.TilePoint),
            _ => false
        };
    }

    private static string RecipeId(string key) => Id + "_Recipe_" + key;

    private void CheckWorld() => Require(Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && Constants.SaveFolderName == this.SaveName && Game1.uniqueIDForThisGame == this.Config.SaveFixtureId
        && Player.farmName.Value == this.Config.FarmName, "The active world does not match the dedicated walking fixture.");

    private void DetachWalkControls()
    {
        if (ReferenceEquals(Simulator.GetValue(null), this)) Simulator.SetValue(null, null);
        if (ReferenceEquals(Observer, this)) Observer = null;
        this.Patch.UnpatchAll(this.Patch.Id);
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        this.Failure = failure;
        this.DetachWalkControls();
        if (this.Options is not null) this.Options.pauseWhenOutOfFocus = this.OriginalPause;
        if (failure is not null) this.Monitor.Log(failure, LogLevel.Error);
        if (this.Persistence is not null)
            this.Persistence.Report(failure, this.Persistence.Verifying ? null : this.CompletedWalk ?? this.CreateWalkEvidence(completed: false),
                this.HomeEntry, this.Processing?.ReadPurchasedEvidence());
        else if (this.CompleteRoute)
            this.Helper.Data.WriteJsonFile("native-shop-walk-chain-result.json", new
            {
                SchemaVersion = 1, Completed = failure is null && !this.SavingObserved && this.Processing?.IsFinished == true,
                this.Failure, this.SavingObserved, ProcessId = Environment.ProcessId, FixtureId = this.Config.SaveFixtureId,
                Walk = this.CompletedWalk ?? this.CreateWalkEvidence(completed: false),
                this.HomeEntry, Processing = this.Processing?.ReadPurchasedEvidence(),
                Preparation = "After the native return, the harness supplied only wood, stone and a copper bar and positioned the farmer for the machine and kitchen actions."
            });
        else
            this.Helper.Data.WriteJsonFile(this.FullRoute ? "native-shop-walk-three-result.json" : "native-shop-walk-result.json",
                this.CreateWalkEvidence(failure is null && !this.SavingObserved));
    }

    private object CreateWalkEvidence(bool completed) => new
    {
        SchemaVersion = 1, Completed = completed, this.Failure, this.SavingObserved,
        ProcessId = Environment.ProcessId, FixtureId = this.Config.SaveFixtureId, this.Leg, this.Legs, this.Clock,
        this.Actions, this.Walked, this.InputFrames, this.Standing, this.Target, this.SkippedEvent, Visits = this.Visits.Values,
        MoneySpent = this.Started ? this.StartingMoney - Player.Money : 0,
        Soybeans = this.Started ? Player.Items.CountId(Soybean) : 0,
        Preparation = "The harness cleared the disposable backpack, disabled pause while unfocused, and supplied movement and action through the native input simulator."
    };

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
