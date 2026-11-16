using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Exercises the installed native menu and complete transactions in one unsaved disposable world.</summary>
internal sealed class P2RecipesScenario
{
    internal const string Scenario = "P2Recipes", FarmName = "XYSP2Recipes";
    internal const ulong FixtureId = 620261130;
    private const string Id = MachineService.Prefix + "_";
    private const string Soy = "(O)" + Id + "Soybean", Fava = "(O)" + Id + "BroadBeans", Napa = "(O)" + Id + "NapaCabbage";
    private const int Slots = 36;
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private static readonly string[] Bean = { "SoyMilk", "TofuPudding", "Tofu", "Yuba", "TofuSheets", "DriedTofu" };
    private static readonly string[] Crock = { "SoySauce", "SoybeanPaste", "SichuanDouban", "SourCabbage" };
    private static readonly Vector2[] Adjacent = { Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY, Vector2.UnitX };

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<RecipeProof> Passed = new();
    private readonly Dictionary<MachineKind, Device> Devices = new();
    private IReadOnlyList<MachineRecipe>? Schedule;
    private MachineMenu? Menu;
    private Pending? Current;
    private int LoadDelay = -1, LoadCount, SavingEvents;
    private bool Finished;
    private string? Failure;

    internal P2RecipesScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "Bind P2 recipe verification to the exact disposable farm.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.LoadDelay = 180;
    internal void OnLoaded() => this.LoadCount++;
    internal void OnSaving()
    {
        if (this.Finished) return;
        this.SavingEvents++;
        this.Failure = "Unexpected native save during the in-memory recipe tests.";
    }

    internal void OnReturnedToTitle()
    {
        if (this.LoadCount > 0 && !this.Finished) this.Failure = "Returned to title before recipe verification completed.";
    }

    internal void Tick()
    {
        if (this.Finished)
        {
            if (!Context.IsWorldReady || IsBoundWorld(Game1.player)) Game1.game1.Exit();
            return;
        }

        try
        {
            if (this.LoadDelay > 0 && --this.LoadDelay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "The exact recipe save is absent or another world is loaded.");
                this.Watch.Start();
                Game1.activeClickableMenu = null;
                SaveGame.Load(SaveName);
                return;
            }
            Require(this.Failure is null, this.Failure ?? "Recipe verification failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(2), $"Timed out following {this.Passed.Count} recipes.");
            if (this.LoadCount == 0 || !Context.IsWorldReady) return;
            Farmer player = Game1.player;
            Require(this.LoadCount == 1 && IsBoundWorld(player), "The loaded world or farmer differs from the recipe fixture.");

            if (this.Current is { } current)
            {
                Require(ReferenceEquals(Game1.activeClickableMenu, this.Menu), "The production recipe menu unexpectedly closed or changed.");
                if (current.ReadyShown) this.Collect(player, current);
                else this.VerifyProcessing(player, current);
                return;
            }
            if (!Context.IsPlayerFree || Game1.isWarping || Game1.fadeToBlack) return;
            this.Schedule ??= this.Prepare(player);
            if (this.Passed.Count == this.Schedule.Count) this.Finish(null);
            else this.Start(player, this.Schedule[this.Passed.Count]);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.ToString()); }
    }

    private IReadOnlyList<MachineRecipe> Prepare(Farmer player)
    {
        Require(Game1.dayOfMonth <= 21 && Game1.timeOfDay is >= 600 and <= 1800,
            "Use a daytime fixture before the last week of its season.");
        var service = this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
            ?? throw new InvalidOperationException("The production machine API is unavailable.");
        var bean = service.Recipes.Where(recipe => recipe.Machine == MachineKind.BeanWorkshop).ToArray();
        var crock = service.Recipes.Where(recipe => recipe.Machine == MachineKind.FermentationCrock).ToArray();
        Require(service.Recipes.Count == 10 && bean.Select(recipe => recipe.Id).SequenceEqual(Bean.Select(RecipeId))
            && crock.Select(recipe => recipe.Id).SequenceEqual(Crock.Select(RecipeId)), "The native menu catalog does not contain the pinned six plus four recipes in order.");

        foreach (var (recipe, materials) in bean.Select(row => (row, new[] { Soy })).Concat(new[]
        {
            (crock[0], new[] { Soy, "(O)262" }),
            (crock[1], new[] { Soy }),
            (crock[2], new[] { Fava, "(O)260" }),
            (crock[3], new[] { Napa })
        }))
        {
            string output = "(O)" + Id + recipe.Id[RecipeId("").Length..];
            Require(recipe.Inputs.Select(ingredient => ingredient.ItemId).SequenceEqual(materials)
                && recipe.Inputs.All(ingredient => ingredient.Quantity > 0)
                && recipe.Output.ItemId == output && recipe.Output.Quantity > 0 && recipe.Minutes > 1,
                $"Invalid installed input, output or duration for {recipe.Id}.");
        }

        var quantities = service.Recipes.SelectMany(recipe => recipe.Inputs).GroupBy(ingredient => ingredient.ItemId)
            .Select(group => (Id: group.Key, Quantity: group.Sum(ingredient => ingredient.Quantity) + 2))
            .Append((Id: "(O)388", Quantity: 5)).ToArray();
        var stock = new Item[Slots];
        for (int i = 0; i < quantities.Length; i++)
        {
            var (id, quantity) = quantities[i];
            stock[i] = ItemRegistry.Create(id, quantity, allowNull: true);
            Require(stock[i] is not null && stock[i].QualifiedItemId == id && stock[i].Stack == quantity, $"Could not prepare exact stock for {id}.");
        }
        player.maxItems.Value = Slots;
        player.Items.OverwriteWith(stock);

        var beanDevice = Place(player, "BeanWorkshop");
        this.Devices.Add(MachineKind.BeanWorkshop, beanDevice);
        this.Devices.Add(MachineKind.FermentationCrock, Place(player, "FermentationCrock", beanDevice.Standing));
        // Test both last rows first, so the evidence retains the important coverage if a later recipe fails.
        return new[] { bean[^1], crock[^1] }.Concat(bean.Take(bean.Length - 1)).Concat(crock.Take(crock.Length - 1)).ToArray();
    }

    private void Start(Farmer player, MachineRecipe recipe)
    {
        Device device = this.Devices[recipe.Machine];
        MachineState idle = ReadState(device.Machine);
        Require(idle.Batch is null, $"A previous recipe is still active on {recipe.Machine}.");
        string[] choiceIds = (recipe.Machine == MachineKind.BeanWorkshop ? Bean : Crock).Select(RecipeId).ToArray();
        int selectedPage = Array.IndexOf(choiceIds, recipe.Id) / 2;
        var menu = Open(player, device);
        for (int page = 0; page <= selectedPage; page++)
        {
            Require(ActionNames(menu).SequenceEqual(choiceIds.Skip(page * 2).Take(2)), $"Wrong visible rows on {recipe.Machine} page {page + 1}.");
            if (page < selectedPage)
            {
                if (page == 0) Click(menu, "Next");
                else menu.receiveKeyPress(Keys.PageDown);
            }
        }
        if (selectedPage == (choiceIds.Length - 1) / 2)
        {
            menu.receiveKeyPress(Keys.PageDown);
            Require(ActionNames(menu).SequenceEqual(choiceIds.Skip(selectedPage * 2)), $"Next wrapped past the final page of {recipe.Machine}.");
        }

        var visibleRecipes = ActionNames(menu);
        var stock = ReadInventory(player);
        var day = Game1.dayOfMonth;
        var time = Game1.timeOfDay;
        var now = MachineService.Now();
        Click(menu, recipe.Id);
        MachineState running = ReadState(device.Machine);
        Batch batch = running.Batch ?? throw new InvalidOperationException($"Menu start did not commit {recipe.Id}.");
        Require(running.InstanceId == idle.InstanceId && running.Revision == idle.Revision + 1 && running.LastActorId == player.UniqueMultiplayerID
            && batch.RecipeId == recipe.Id && batch.Output == recipe.Output && batch.StartedAt == now && batch.ReadyAt == now + recipe.Minutes
            && batch.Inputs.All(input => input.Quality == 0)
            && batch.Inputs.GroupBy(input => input.ItemId).ToDictionary(group => group.Key, group => group.Sum(input => input.Quantity))
                .OrderBy(pair => pair.Key).SequenceEqual(recipe.Inputs.ToDictionary(input => input.ItemId, input => input.Quantity).OrderBy(pair => pair.Key)),
            $"The frozen batch for {recipe.Id} did not match the selected installed rule.");
        foreach (var input in recipe.Inputs) AdaptStock(stock, input.ItemId, 0, -input.Quantity);
        VerifyInventory(player, stock, $"start of {recipe.Id}");
        this.Current = new(recipe, device, running, Raw(device.Machine), stock, day, time, selectedPage + 1, visibleRecipes);
        Close(menu);
        this.Menu = Open(player, device);
        this.VerifyDisplayedBatch(this.Menu, running, MachinePhase.Processing);
    }

    private void VerifyProcessing(Farmer player, Pending active)
    {
        var menu = this.Menu!;
        Batch batch = active.State.Batch!;
        SetTime(batch.ReadyAt - 1);
        this.VerifyDisplayedBatch(menu, active.State, MachinePhase.Processing);
        Click(menu, "Collect");
        Require(Raw(active.Device.Machine) == active.Raw, $"The processing menu changed {batch.RecipeId} one minute early.");
        VerifyInventory(player, active.Stock, $"early collect attempt for {batch.RecipeId}");
        Close(menu);

        SetTime(batch.ReadyAt);
        this.Menu = Open(player, active.Device);
        this.VerifyDisplayedBatch(this.Menu, active.State, MachinePhase.Ready);
        this.Current = active with { ReadyShown = true };
    }

    private void Collect(Farmer player, Pending active)
    {
        var menu = this.Menu!;
        Batch batch = active.State.Batch!;
        this.VerifyDisplayedBatch(menu, active.State, MachinePhase.Ready);
        Require(Raw(active.Device.Machine) == active.Raw, $"{batch.RecipeId} was rewritten before collection.");
        Point click = Component(menu, "Collect").bounds.Center;
        menu.receiveLeftClick(click.X, click.Y);
        MachineState idle = ReadState(active.Device.Machine);
        Require(idle.InstanceId == active.State.InstanceId && idle.Revision == active.State.Revision + 1 && idle.Batch is null
            && idle.LastActorId == player.UniqueMultiplayerID, $"Collection did not clear exactly one {batch.RecipeId} batch.");
        AdaptStock(active.Stock, batch.Output.ItemId, batch.Output.Quality, batch.Output.Quantity);
        VerifyInventory(player, active.Stock, $"collection of {batch.RecipeId}");
        string after = Raw(active.Device.Machine);
        menu.receiveLeftClick(click.X, click.Y);
        Require(Raw(active.Device.Machine) == after, $"A second menu click changed the collected {batch.RecipeId} state.");
        VerifyInventory(player, active.Stock, $"second collect click for {batch.RecipeId}");
        Close(menu);
        this.Passed.Add(new(batch.RecipeId, active.Recipe.Machine.ToString(), active.Page, active.VisibleRecipes,
            batch.Inputs, batch.Output, batch.StartedAt, batch.ReadyAt, active.State.Revision, idle.Revision));
        Game1.dayOfMonth = active.ClockDay;
        Game1.timeOfDay = active.ClockTime;
        this.Current = null;
        this.Menu = null;
    }

    private void VerifyDisplayedBatch(MachineMenu menu, MachineState state, MachinePhase phase)
    {
        MachineState? display = this.Helper.Reflection.GetField<MachineState?>(menu, "DisplayedState").GetValue();
        var expected = state.Batch!;
        Require(display is { Batch: { } shown } && display.InstanceId == state.InstanceId && display.Revision == state.Revision
            && display.PhaseAt(MachineService.Now()) == phase && shown.Id == expected.Id && shown.RecipeId == expected.RecipeId
            && shown.StartedAt == expected.StartedAt && shown.ReadyAt == expected.ReadyAt
            && shown.Output == expected.Output && shown.Inputs.SequenceEqual(expected.Inputs)
            && ActionNames(menu).SequenceEqual(new[] { "Collect" })
            && menu.allClickableComponents.All(component => component.name is not ("Previous" or "Next")),
            $"The {phase} menu did not display the frozen selected batch {expected.RecipeId}.");
    }

    private static Device Place(Farmer player, string key, Vector2? reservedStanding = null)
    {
        GameLocation location = player.currentLocation;
        var machine = ItemRegistry.Create<SObject>("(BC)" + Id + key);
        for (int dy = -4; dy <= 4; dy++)
        for (int dx = -4; dx <= 4; dx++)
        {
            Vector2 tile = player.Tile + new Vector2(dx, dy);
            if (tile == player.Tile || tile == reservedStanding || !FreeTile(location, tile)) continue;
            for (int direction = 0; direction < Adjacent.Length; direction++)
            {
                Vector2 standing = tile + Adjacent[direction];
                if (!FreeTile(location, standing)) continue;
                machine.TileLocation = tile;
                location.objects.Add(tile, machine);
                MachineService.Initialize(machine);
                return new(machine, location, standing, direction);
            }
        }
        throw new InvalidOperationException($"No ordinary open fixture tile with an approach for {key}.");
    }

    private static bool FreeTile(GameLocation location, Vector2 tile) =>
        location.isTileOnMap(tile) && !location.objects.ContainsKey(tile)
        && location.CanItemBePlacedHere(tile, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
        && location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "TouchAction", "Back") is null;

    private static MachineMenu Open(Farmer player, Device device)
    {
        Require(Game1.activeClickableMenu is null && ReferenceEquals(player.currentLocation, device.Location)
            && device.Location.objects.TryGetValue(device.Machine.TileLocation, out var placed) && ReferenceEquals(placed, device.Machine),
            "The expected test device or its native location changed.");
        player.setTileLocation(device.Standing);
        player.faceDirection(device.Direction);
        return device.Machine.checkForAction(player) && Game1.activeClickableMenu is MachineMenu menu
            ? menu : throw new InvalidOperationException("The ordinary device action did not open the production MachineMenu.");
    }

    private static string[] ActionNames(MachineMenu menu) => menu.allClickableComponents
        .Where(component => component.name == "Collect" || component.name?.StartsWith(RecipeId(""), StringComparison.Ordinal) == true)
        .Select(component => component.name).ToArray();
    private static ClickableComponent Component(MachineMenu menu, string name) =>
        menu.allClickableComponents.Single(component => component.name == name);
    private static void Click(MachineMenu menu, string name)
    {
        Point point = Component(menu, name).bounds.Center;
        menu.receiveLeftClick(point.X, point.Y);
    }

    private static void Close(MachineMenu menu)
    {
        Point point = menu.upperRightCloseButton.bounds.Center;
        menu.receiveLeftClick(point.X, point.Y);
        Require(Game1.activeClickableMenu is null, "The ordinary menu close control did not close the menu.");
    }

    private static void SetTime(long minutes)
    {
        int days = checked((int)(minutes / 1440) - Game1.Date.TotalDays);
        int minute = (int)(minutes % 1440);
        Require(Game1.dayOfMonth + days is >= 1 and <= 28, "The sampled ready time crosses a season boundary.");
        Game1.dayOfMonth += days;
        Game1.timeOfDay = minute / 60 * 100 + minute % 60;
        Require(MachineService.Now() == minutes, "The sampled machine clock did not resolve to the exact minute.");
    }

    private static Dictionary<(string Item, int Quality), int> ReadInventory(Farmer player) =>
        player.Items.Where(item => item is not null).GroupBy(item => (item.QualifiedItemId, item.Quality))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Stack));

    private static void AdaptStock(Dictionary<(string Item, int Quality), int> stock, string id, int quality, int delta)
    {
        int expected = stock.GetValueOrDefault((id, quality)) + delta;
        Require(expected >= 0, $"Prepared stock for {id} is insufficient.");
        if (expected == 0) stock.Remove((id, quality));
        else stock[(id, quality)] = expected;
    }

    private static void VerifyInventory(Farmer player, Dictionary<(string Item, int Quality), int> expected, string action)
    {
        var actual = ReadInventory(player);
        Require(actual.Count == expected.Count && expected.All(pair => actual.GetValueOrDefault(pair.Key) == pair.Value),
            $"Exact item and quality counts differ after {action}.");
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        this.Watch.Stop();
        if (this.Current is { } pending && Context.IsWorldReady && IsBoundWorld(Game1.player))
        {
            Game1.dayOfMonth = pending.ClockDay;
            Game1.timeOfDay = pending.ClockTime;
        }
        this.Helper.Data.WriteJsonFile("p2-recipes-result.json", new
        {
            SchemaVersion = 1, Scenario, FixtureId, ProcessId = Environment.ProcessId,
            Completed = failure is null, Failure = failure, this.LoadCount, this.SavingEvents,
            Scope = "Native menu and inventory transactions with prepared fixtures and controlled in-memory deadlines.",
            CurrentRecipe = this.Current?.Recipe.Id,
            this.Passed
        });
        this.Monitor.Log(failure ?? "All P2 recipe menu transactions completed.", failure is null ? LogLevel.Info : LogLevel.Error);
    }

    private static bool IsBoundWorld(Farmer player) =>
        Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen && Constants.SaveFolderName == SaveName
        && Game1.uniqueIDForThisGame == FixtureId && player.farmName.Value == FarmName && player.Name == FarmName;
    private static string RecipeId(string name) => Id + "Recipe_" + name;
    private static string Raw(SObject machine) => machine.modData[MachineService.StateKey];
    private static MachineState ReadState(SObject machine) => MachineService.TryRead(machine, out var state)
        ? state! : throw new InvalidOperationException("The fixture device has no valid production state.");
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    private sealed record Device(SObject Machine, GameLocation Location, Vector2 Standing, int Direction);
    private sealed record Pending(MachineRecipe Recipe, Device Device, MachineState State, string Raw,
        Dictionary<(string Item, int Quality), int> Stock, int ClockDay, int ClockTime, int Page, string[] VisibleRecipes, bool ReadyShown = false);
    private sealed record RecipeProof(string Recipe, string Device, int Page, string[] VisibleRecipes, IReadOnlyList<ConsumedStack> Inputs,
        Product Output, long StartedAt, long ReadyAt, long StartRevision, long CollectRevision);
}
