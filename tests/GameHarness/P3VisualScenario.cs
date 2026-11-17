using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Renders the actual equipped game and menu sprites in one bound, unsaved candidate session.</summary>
internal sealed class P3VisualScenario
{
    internal const string Scenario = "P3Visual", FarmName = "XYSP3Visual";
    internal const ulong FixtureId = 620261135;
    private const string Prefix = MachineService.Prefix + "_";
    private static readonly string SaveName = $"{FarmName}_{FixtureId}";
    private static Farmer Player => Game1.player;

    private readonly Stopwatch Watch = new();
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly List<string> Frames = new();
    private int Stage, LoadCount, SaveCount, Delay = -1, DrawDelay;
    private bool Finished;
    private bool? PreviousPause;
    private string? PendingFrame, Failure;
    private MachineMenu? Menu;
    private SObject[]? Devices;
    private int StartMinutes;

    internal P3VisualScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == FixtureId, "Use the exact disposable P3 visual fixture.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueLoad() => this.Delay = 180;
    internal void OnLoaded() => this.LoadCount++;
    internal void OnSaving() { this.SaveCount++; this.Failure = "Unexpected visual fixture save."; }
    internal void Tick()
    {
        if (this.Finished) { Game1.game1.Exit(); return; }
        try
        {
            if (this.Delay > 0 && --this.Delay == 0)
            {
                Require(!Context.IsWorldReady && File.Exists(Path.Combine(Constants.SavesPath, SaveName, SaveName)),
                    "The visual test copy is missing or another world is active.");
                this.Watch.Start();
                Game1.activeClickableMenu = null;
                SaveGame.Load(SaveName);
            }
            if (!this.Watch.IsRunning) return;
            Require(this.Failure is null, this.Failure ?? "A render failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromSeconds(80), $"Visual timeout at stage {this.Stage}.");
            if (!Context.IsWorldReady || this.LoadCount == 0 || this.PendingFrame is not null) return;
            Require(this.LoadCount == 1 && Context.IsMainPlayer && !Context.IsMultiplayer && !Context.IsSplitScreen
                && Game1.uniqueIDForThisGame == FixtureId && Player.Name == FarmName && Player.farmName.Value == FarmName,
                "The visual fixture changed its bound actor or world.");
            if (this.PreviousPause is null)
            {
                this.PreviousPause = Game1.options.pauseWhenOutOfFocus;
                Game1.options.pauseWhenOutOfFocus = false;
            }
            this.Advance();
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.ToString()); }
    }

    private void Advance()
    {
        switch (this.Stage)
        {
            case 0 when Context.IsPlayerFree && !Game1.fadeToBlack:
                string[] items = { "Soybean", "NapaCabbageSeeds", "BambooShoots", "WildScallion", "ChineseToon", "Mugwort",
                    "SoyMilk", "SichuanDouban", "SmokedTofu", "MapoTofu" };
                for (int i = 0; i < items.Length; i++) Player.Items[i] = ItemRegistry.Create("(O)" + Prefix + items[i]);
                Player.Items[10] = ItemRegistry.Create("(BC)" + Prefix + "BeanWorkshop");
                Player.Items[11] = ItemRegistry.Create("(BC)" + Prefix + "FermentationCrock");
                Game1.activeClickableMenu = new GameMenu();
                this.Frame("01-inventory");
                break;
            case 1:
                CloseMenu();
                Require(Utility.TryOpenShopMenu("Saloon", "Gus", playOpenSound: false)
                    && Game1.activeClickableMenu is ShopMenu, "The original saloon menu was unavailable.");
                var shop = (ShopMenu)Game1.activeClickableMenu;
                int itemIndex = shop.forSale.FindIndex(item => item.QualifiedItemId == "(O)" + Prefix + "Pork");
                Require(itemIndex >= 0, "Native saloon is missing the new food stock.");
                this.Helper.Reflection.GetField<int>(shop, "currentItemIndex").SetValue(itemIndex);
                this.Frame("02-saloon");
                break;
            case 2:
                CloseMenu();
                Game1.warpFarmer(Game1.getFarm().Name, 41, 7, false);
                this.Stage++;
                break;
            case 3 when ReferenceEquals(Player.currentLocation, Game1.getFarm()) && !Game1.fadeToBlack && !Game1.isWarping:
                this.PreparePlots();
                this.Frame("03-farm-crops-forage-equipment");
                break;
            case 4: this.OpenLastPage(0); this.Frame("04-bean-last-page"); break;
            case 5:
                Click(this.Menu!, "Guide");
                this.Menu!.receiveGamePadButton(Buttons.RightShoulder);
                this.Frame("05-guide-narrow");
                break;
            case 6:
                Click(this.Menu!, "Guide");
                Click(this.Menu!, Prefix + "Recipe_DriedTofu");
                Require(Read(this.Devices![0]).Batch?.RecipeId == Prefix + "Recipe_DriedTofu", "Bean recipe did not start.");
                this.Frame("06-bean-processing");
                break;
            case 7: this.OpenLastPage(1); this.Frame("07-crock-last-page"); break;
            case 8:
                Click(this.Menu!, Prefix + "Recipe_SourCabbage");
                Require(Read(this.Devices![1]).Batch?.RecipeId == Prefix + "Recipe_SourCabbage", "Crock recipe did not start.");
                this.Frame("08-crock-processing");
                break;
            case 9:
                CloseMenu();
                Player.Position = new Vector2(41, 7) * 64;
                int ready = this.StartMinutes + 310;
                Game1.timeOfDay = ready / 60 * 100 + ready % 60;
                Require(this.Devices!.All(machine => Read(machine).PhaseAt(MachineService.Now()) == MachinePhase.Ready), "Expected both batches ready.");
                this.Frame("09-farm-ready-markers");
                break;
            case 10: this.Open(0); this.Frame("10-bean-ready"); break;
            case 11: this.Open(1); this.Frame("11-crock-ready"); break;
            case 12: this.Finish(null); break;
        }
    }

    private void PreparePlots()
    {
        var farm = Game1.getFarm();
        Require(farm.terrainFeatures.TryGetValue(new Vector2(40, 4), out var soy) && soy is HoeDirt { crop: not null }
            && farm.terrainFeatures.TryGetValue(new Vector2(40, 6), out var napa) && napa is HoeDirt { crop: not null },
            "Clone the verified Fall 2 farm with the two actual crop plots.");
        this.Devices = new SObject[2];
        string[] devices = { "BeanWorkshop", "FermentationCrock" };
        for (int i = 0; i < devices.Length; i++)
        {
            var key = new Vector2(42 + i, 5);
            var item = ItemRegistry.Create<SObject>("(BC)" + Prefix + devices[i]);
            Require(!farm.objects.ContainsKey(key) && item.placementAction(farm, (int)key.X * 64, (int)key.Y * 64, Player), "Visual equipment tile was unavailable.");
            this.Devices[i] = farm.objects[key];
        }
        string[] forage = { "BambooShoots", "WildScallion", "ChineseToon", "Mugwort" };
        var plots = Enumerable.Range(0, 16).Select(i => new Vector2(38 + i % 8, 8 + i / 8))
            .Where(tile => !farm.objects.ContainsKey(tile) && !farm.terrainFeatures.ContainsKey(tile)).Take(forage.Length).ToArray();
        Require(plots.Length == forage.Length, "Not enough free ground tiles for the visual forage sample.");
        for (int i = 0; i < forage.Length; i++)
        {
            Vector2 tile = plots[i];
            var item = ItemRegistry.Create<SObject>("(O)" + Prefix + forage[i]);
            item.TileLocation = tile;
            item.IsSpawnedObject = true;
            farm.objects.Add(tile, item);
        }
        Player.Items[0] = ItemRegistry.Create("(O)" + Prefix + "Soybean", 6);
        Player.Items[1] = ItemRegistry.Create("(O)" + Prefix + "NapaCabbage", 4);
        this.StartMinutes = Game1.timeOfDay / 100 * 60 + Game1.timeOfDay % 100;
        Require(this.StartMinutes < 16 * 60, "Use the visual fixture in daytime.");
    }

    private void Open(int index)
    {
        CloseMenu();
        SObject machine = this.Devices![index];
        Player.Position = (machine.TileLocation + Vector2.UnitY) * 64;
        Player.faceDirection(0);
        Require(machine.checkForAction(Player) && Game1.activeClickableMenu is MachineMenu, "The native world did not open the equipment menu.");
        this.Menu = (MachineMenu)Game1.activeClickableMenu!;
    }

    private void OpenLastPage(int index)
    {
        this.Open(index);
        this.Menu!.receiveGamePadButton(Buttons.RightShoulder);
        if (index == 0) this.Menu.receiveKeyPress(Keys.PageDown);
        string expected = Prefix + (index == 0 ? "Recipe_DriedTofu" : "Recipe_SourCabbage");
        Require(this.Menu.allClickableComponents.Any(component => component.name == expected), "Expected last-page recipes were hidden.");
    }

    private static MachineState Read(SObject machine) => MachineService.TryRead(machine, out MachineState? state)
        ? state! : throw new InvalidOperationException("Invalid visual batch state.");
    private static void Click(MachineMenu menu, string name)
    {
        Point point = menu.allClickableComponents.Single(component => component.name == name).bounds.Center;
        menu.receiveLeftClick(point.X, point.Y);
    }
    private static void CloseMenu() => Game1.activeClickableMenu?.exitThisMenu(playSound: false);

    private void Frame(string name)
    {
        this.PendingFrame = name;
        this.DrawDelay = 4;
        this.Stage++;
    }

    internal void OnRendered()
    {
        if (this.PendingFrame is not { } name || this.DrawDelay-- > 0) return;
        try
        {
            GraphicsDevice device = Game1.graphics.GraphicsDevice;
            if (Game1.activeClickableMenu is { } menu) this.DrawMenu(device, menu, name);
            else
            {
                int width = device.PresentationParameters.BackBufferWidth, height = device.PresentationParameters.BackBufferHeight;
                var pixels = new Color[checked(width * height)];
                device.GetBackBufferData(pixels);
                using var texture = new Texture2D(device, width, height);
                texture.SetData(pixels);
                this.Save(texture, name);
            }
            this.Frames.Add(name + ".png");
            this.PendingFrame = null;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Failure = error.ToString(); }
    }

    private void DrawMenu(GraphicsDevice device, IClickableMenu menu, string name)
    {
        // SMAPI Rendered occurs before the final UI composite on the native profile. Render the actual live menu into a capture target.
        var oldViewport = Game1.uiViewport;
        var targets = device.GetRenderTargets();
        bool narrow = name == "05-guide-narrow";
        if (narrow) Game1.uiViewport = new xTile.Dimensions.Rectangle(0, 0, 800, 520);
        using var target = new RenderTarget2D(device, Game1.uiViewport.Width, Game1.uiViewport.Height);
        using var batch = new SpriteBatch(device);
        try
        {
            device.SetRenderTarget(target);
            device.Clear(new Color(30, 27, 22));
            batch.Begin(samplerState: SamplerState.PointClamp);
            menu.draw(batch);
            batch.End();
        }
        finally
        {
            device.SetRenderTargets(targets);
            Game1.uiViewport = oldViewport;
            if (narrow) menu.gameWindowSizeChanged(Rectangle.Empty, Rectangle.Empty);
        }
        this.Save(target, name);
    }

    private void Save(Texture2D texture, string name)
    {
        using var stream = File.Create(Path.Combine(this.Helper.DirectoryPath, name + ".png"));
        texture.SaveAsPng(stream, texture.Width, texture.Height);
    }

    private void Finish(string? failure)
    {
        if (this.PreviousPause is bool pause) Game1.options.pauseWhenOutOfFocus = pause;
        if (failure is not null) this.Monitor.Log(failure, LogLevel.Error);
        this.Helper.Data.WriteJsonFile("p3-visual-result.json", new
        {
            Scenario, FixtureId, ProcessId = Environment.ProcessId, Completed = failure is null && this.Frames.Count == 11 && this.SaveCount == 0,
            Failure = failure, SavingEvents = this.SaveCount, this.Frames,
            Scope = "Live game sprites and menus; fixture placed samples and advanced clock, gamepad event dispatch without hardware."
        });
        this.Finished = true;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
