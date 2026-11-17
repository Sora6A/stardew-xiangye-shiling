using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using StardewValley.Locations;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>Creates exactly one new farm through the native title menu and verifies the installed production package.</summary>
internal sealed class P3FreshInstallScenario
{
    internal const string Scenario = "P3FreshInstall";
    internal const string FarmName = "XYSP3Fresh";
    private const string ModId = MachineService.Prefix;
    private const string Favorite = "Soybean";
    private const string Soybean = "(O)" + ModId + "_Soybean", Tofu = "(O)" + ModId + "_Tofu";
    private const string Workshop = "(BC)" + ModId + "_BeanWorkshop", TofuRecipe = ModId + "_Recipe_Tofu";
    private static readonly Vector2 Below = Vector2.UnitY;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Stopwatch Watch = new();
    private readonly List<string> Evidence = new();
    private Stage Current;
    private int Delay = -1, CreatingEvents, CreatedEvents, SavingEvents, SavedEvents, LoadEvents;
    private bool Finished, PreviousPause;
    private string? Failure, NativeFolder;
    private ulong NativeWorld;
    private Options? GameOptions;
    private TitleMenu? Title;
    private CharacterCustomization? Creator;
    private SObject? Machine;
    private string? RawBatch;
    private string[] InitialSaveHashes = Array.Empty<string>();
    private int StartedAt, CollectedAt;
    private static Farmer Player => Game1.player;
    private enum Stage { Title, Name, Confirm, FirstMorning, Processing }

    internal P3FreshInstallScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && !config.OpenPreview && config.Scenario == Scenario && config.FarmName == FarmName
            && config.SaveFixtureId == 0, "Only the exact new-farm fixture may create a world.");
        this.Helper = helper;
        this.Monitor = monitor;
    }

    internal void QueueStart() => this.Delay = 180;

    internal void OnSaveCreating()
    {
        this.CreatingEvents++;
        if (this.Current != Stage.FirstMorning || !ExactFarmer() || this.CreatingEvents != 1)
            this.Failure = "Unexpected native SaveCreating outside the new-farm action.";
    }

    internal void OnSaveCreated()
    {
        this.CreatedEvents++;
        if (this.Current != Stage.FirstMorning || !ExactFarmer() || this.CreatingEvents != 1 || this.CreatedEvents != 1)
            this.Failure = "Unexpected native SaveCreated outside the new-farm action.";
    }

    internal void OnSaving()
    {
        this.SavingEvents++;
        if (this.Current != Stage.FirstMorning || !ExactFarmer() || this.SavingEvents != 1)
            this.Failure = "Unexpected native Saving outside the first-morning creation.";
    }

    internal void OnSaved()
    {
        this.SavedEvents++;
        if (this.Current != Stage.FirstMorning || !ExactFarmer() || this.SavingEvents != 1 || this.SavedEvents != 1)
            this.Failure = "Unexpected native Saved outside the first-morning creation.";
    }

    internal void OnLoaded()
    {
        this.LoadEvents++;
        if (this.Current != Stage.FirstMorning || this.LoadEvents != 1 || !ExactFarmer())
            this.Failure = "Unexpected loaded farmer.";
    }

    internal void OnReturnedToTitle()
    {
        if (this.Current >= Stage.FirstMorning && !this.Finished)
            this.Failure = "The native world returned to title.";
    }

    internal void Tick()
    {
        if (this.Finished) { Game1.game1.Exit(); return; }
        if (this.Delay < 0 || (this.Delay > 0 && --this.Delay > 0)) return;
        if (!this.Watch.IsRunning) this.Watch.Start();
        try
        {
            Require(this.Failure is null, this.Failure ?? "Fresh install failed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(5), $"Timed out during {this.Current}.");
            switch (this.Current)
            {
                case Stage.Title:
                    if (Game1.activeClickableMenu is not TitleMenu { isTransitioningButtons: false } title) return;
                    this.RequireNewTitle();
                    this.Title = title;
                    title.skipToTitleButtons();
                    title.performButtonAction("New");
                    this.Current = Stage.Name;
                    this.Record("Invoked the original title New action.");
                    return;
                case Stage.Name:
                    if (TitleMenu.subMenu is not CharacterCustomization creator || this.Title!.isTransitioningButtons) return;
                    Require(this.Title == Game1.activeClickableMenu && creator.source == CharacterCustomization.Source.NewGame,
                        "The original title did not present single-player character creation.");
                    this.Creator = creator;
                    this.Helper.Reflection.GetField<TextBox>(creator, "nameBox").GetValue().Text = FarmName;
                    this.Helper.Reflection.GetField<TextBox>(creator, "farmnameBox").GetValue().Text = FarmName;
                    this.Helper.Reflection.GetField<TextBox>(creator, "favThingBox").GetValue().Text = Favorite;
                    this.Current = Stage.Confirm;
                    return;
                case Stage.Confirm:
                    Require(this.Creator == TitleMenu.subMenu && this.Title == Game1.activeClickableMenu,
                        "Character creation closed before confirmation.");
                    // Native CharacterCustomization.draw transfers the text boxes into the original farmer model.
                    if (!ExactFarmer()) return;
                    this.RequireNewTitle();
                    if (!this.Helper.Reflection.GetField<bool>(this.Creator!, "skipIntro").GetValue())
                    {
                        ClickCreator(this.Creator!.skipIntroButton);
                        Require(this.Helper.Reflection.GetField<bool>(this.Creator!, "skipIntro").GetValue(), "The original Skip Intro click was rejected.");
                    }
                    this.Current = Stage.FirstMorning;
                    ClickCreator(this.Creator!.okButton);
                    this.Record("Confirmed the original named single-player creation with Skip Intro.");
                    return;
                case Stage.FirstMorning:
                    if (this.CreatedEvents != 1 || this.LoadEvents == 0 || !Context.IsPlayerFree || Game1.newDay || Game1.fadeToBlack) return;
                    this.CheckFirstMorning();
                    this.VerifyInstalledContent();
                    this.StartTofu();
                    this.Current = Stage.Processing;
                    return;
                case Stage.Processing:
                    this.CheckWorld();
                    if (this.CollectTofu()) this.Finish(null);
                    return;
            }
        }
        catch (Exception error) { this.Finish(error.ToString()); }
    }

    private void RequireNewTitle()
    {
        Require(!Context.IsWorldReady && !Context.IsMultiplayer && !Context.IsSplitScreen,
            "The title must be outside a loaded world and multiplayer.");
        if (!Directory.Exists(Constants.SavesPath)) return;
        Require(!Directory.EnumerateFileSystemEntries(Constants.SavesPath).Any(path =>
            Path.GetFileName(path).StartsWith(FarmName, StringComparison.OrdinalIgnoreCase)), "A fresh-farm directory or file already exists.");
    }

    private void ClickCreator(ClickableTextureComponent component)
    {
        Point button = component.bounds.Center;
        this.Creator!.receiveLeftClick(button.X, button.Y, playSound: false);
    }

    private void CheckFirstMorning()
    {
        Require(this.CreatingEvents == 1 && this.CreatedEvents == 1 && this.LoadEvents == 1 && this.SavingEvents == this.SavedEvents
            && this.SavedEvents <= 1, "Expected exactly one native creation and load with no unmatched save event.");
        this.NativeWorld = Game1.uniqueIDForThisGame;
        this.NativeFolder = $"{FarmName}_{this.NativeWorld}";
        this.CheckWorld();
        Require(Game1.year == 1 && Game1.season == Season.Spring && Game1.dayOfMonth == 1
            && Player.currentLocation is FarmHouse { upgradeLevel: 0 } && Player.Money == 500 && Player.MaxItems == 12
            && Player.Items.Any(item => item?.QualifiedItemId == "(T)Hoe") && Count(Soybean) == 0 && Count(Tofu) == 0
            && !Player.knowsRecipe(ModId + "_Recipe_BeanWorkshop"), "The original start did not produce an unmodified Spring 1 farmer.");
        this.InitialSaveHashes = this.ReadNativeSaveHashes();
        this.Record("Reached Spring 1 in the original newly created, initially saved farmhouse with 500g and starting tools.");
    }

    private void VerifyInstalledContent()
    {
        Require(this.Helper.ModRegistry.IsLoaded(ModId) && this.Helper.ModRegistry.IsLoaded("Pathoschild.ContentPatcher")
            && this.Helper.ModRegistry.IsLoaded(ModId + ".Content"), "The normal production mod, content pack or Content Patcher did not load.");
        var objects = this.Helper.GameContent.Load<Dictionary<string, ObjectData>>("Data/Objects");
        var equipment = this.Helper.GameContent.Load<Dictionary<string, BigCraftableData>>("Data/BigCraftables");
        var crops = this.Helper.GameContent.Load<Dictionary<string, CropData>>("Data/Crops");
        var cooking = this.Helper.GameContent.Load<Dictionary<string, string>>("Data/CookingRecipes");
        var smoker = this.Helper.GameContent.Load<Dictionary<string, MachineData>>("Data/Machines")["(BC)FishSmoker"];
        Require(objects.Keys.Count(ModKey) == 38 && equipment.Keys.Count(ModKey) == 2 && crops.Keys.Count(ModKey) == 2
            && cooking.Keys.Count(ModKey) == 16 && smoker.OutputRules.Count(rule => ModKey(rule.Id)) == 2,
            "The installed asset set is incomplete.");
        foreach (var (asset, width, height) in new[] { ("Objects", 128, 80), ("Machines", 32, 32), ("Crops", 256, 32) })
        {
            var texture = this.Helper.GameContent.Load<Texture2D>($"Mods/{ModId}/{asset}");
            Require(texture.Width == width && texture.Height == height, $"Installed texture bounds differ for {asset}.");
        }
        var service = this.Helper.ModRegistry.GetApi(ModId) as MachineService;
        Require(service?.Recipes.Count == 10, "The installed production service did not load all device rules.");
        this.Record("Loaded the candidate's 38 objects, two devices, two crops, 16 kitchen recipes, two native smoker rules and three original textures.");
        static bool ModKey(string key) => key.StartsWith(ModId + "_", StringComparison.Ordinal);
    }

    private void StartTofu()
    {
        this.GameOptions = Game1.options;
        this.PreviousPause = this.GameOptions.pauseWhenOutOfFocus;
        this.GameOptions.pauseWhenOutOfFocus = false;
        Player.Money = 3000;
        Require(Player.addItemToInventory(ItemRegistry.Create("(O)334")) is null, "The prepared copper bar did not fit the starter inventory.");
        NativeRouteActions.Buy(this.Helper, this.Monitor, "SeedShop", Soybean, 3);
        NativeRouteActions.Buy(this.Helper, this.Monitor, "Carpenter", Workshop, 1, recipe: true);
        NativeRouteActions.Buy(this.Helper, this.Monitor, "Carpenter", "(O)388", 50);
        NativeRouteActions.Buy(this.Helper, this.Monitor, "Carpenter", "(O)390", 20);
        Require(Player.Money == 420 && Count(Soybean) == 3 && Player.knowsRecipe(ModId + "_Recipe_BeanWorkshop"),
            "Early spring native purchases did not supply exact soybeans, materials and blueprint.");
        this.Machine = Place((FarmHouse)Player.currentLocation, NativeRouteActions.Craft("BeanWorkshop"));
        Require(Count(Workshop) == 0 && Count("(O)388") == 0 && Count("(O)390") == 0 && Count("(O)334") == 0,
            "Native crafting or placement did not consume exactly the early building materials.");
        Require(this.Machine.checkForAction(Player) && Game1.activeClickableMenu is MachineMenu, "The installed machine menu did not open.");
        var menu = (MachineMenu)Game1.activeClickableMenu;
        Click(menu, "Next");
        Click(menu, TofuRecipe);
        menu.exitThisMenu(playSound: false);
        Require(MachineService.TryRead(this.Machine, out var state) && state is { Revision: 1, Batch: { } batch }
            && batch.RecipeId == TofuRecipe && batch.ReadyAt == batch.StartedAt + 120 && Count(Soybean) == 0,
            "The early machine did not commit the purchased tofu ingredients.");
        this.RawBatch = this.Machine.modData[MachineService.StateKey];
        this.StartedAt = Game1.timeOfDay;
        this.Record("Bought spring soybeans, blueprint, wood and stone via original vendor menus, crafted and placed a bean workshop, and started tofu through the original production menu.");
    }

    private bool CollectTofu()
    {
        SObject machine = this.Machine!;
        Require(MachineService.TryRead(machine, out var state) && state!.Batch is not null && machine.modData[MachineService.StateKey] == this.RawBatch,
            "The early batch changed before collection.");
        if (state!.PhaseAt(MachineService.Now()) != MachinePhase.Ready || !Context.IsPlayerFree) return false;
        Require(MachineService.Now() == state.Batch!.ReadyAt, "Collection did not reach the batch's first ready clock tick.");
        Require(machine.checkForAction(Player) && Game1.activeClickableMenu is MachineMenu, "The ready production menu did not open.");
        var menu = (MachineMenu)Game1.activeClickableMenu;
        Click(menu, "Collect");
        menu.exitThisMenu(playSound: false);
        Require(MachineService.TryRead(machine, out state) && state is { Revision: 2, Batch: null } && Count(Tofu) == 2 && Count(Soybean) == 0,
            "The original ready menu did not collect exactly two tofu.");
        this.CollectedAt = Game1.timeOfDay;
        Require(this.InitialSaveHashes.SequenceEqual(this.ReadNativeSaveHashes()), "The initial disk save changed during the in-memory tofu route.");
        this.Record("Native clock reached the original 120-minute deadline and the installed production menu delivered two tofu.");
        return true;
    }

    private string[] ReadNativeSaveHashes()
    {
        string folder = Path.Combine(Constants.SavesPath, this.NativeFolder!);
        return new[] { this.NativeFolder!, "SaveGameInfo" }.Select(file =>
        {
            using var contents = File.OpenRead(Path.Combine(folder, file));
            using var algorithm = SHA256.Create();
            return Convert.ToHexString(algorithm.ComputeHash(contents));
        }).ToArray();
    }

    private static SObject Place(FarmHouse house, SObject carried)
    {
        int slot = Player.Items.ToList().FindIndex(item => ReferenceEquals(item, carried));
        Require(slot is >= 0 and < 12, "The crafted device is not in a native toolbar slot.");
        Player.CurrentToolIndex = slot;
        var bounds = house.Map.Layers[0];
        for (int y = 2; y + 2 < bounds.LayerHeight; y++)
        for (int x = 2; x + 2 < bounds.LayerWidth; x++)
        {
            Vector2 tile = new(x, y);
            if (house.isTileOnWall(x, y) || house.isTileOnWall(x, y + 1) || !house.CanItemBePlacedHere(tile)
                || !house.CanItemBePlacedHere(tile + Below, collisionMask: CollisionMask.All & ~CollisionMask.Farmers)
                || house.doesTileHaveProperty(x, y + 1, "TouchAction", "Back") is not null) continue;
            Player.setTileLocation(tile + Below);
            Player.faceDirection(0);
            int pixelX = x * 64 + 32, pixelY = y * 64 + 32;
            if (!Utility.playerCanPlaceItemHere(house, carried, pixelX, pixelY, Player)) continue;
            Require(Utility.tryToPlaceItem(house, carried, pixelX, pixelY), "The original placement rejected the crafted device.");
            return house.objects[tile];
        }
        throw new InvalidOperationException("No clear original farmhouse tile for the device.");
    }

    private static void Click(MachineMenu menu, string component)
    {
        Point point = menu.allClickableComponents.Single(item => item.name == component).bounds.Center;
        menu.receiveLeftClick(point.X, point.Y, playSound: false);
    }

    private void CheckWorld() => Require(ExactFarmer() && Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && !Context.IsSplitScreen && this.NativeWorld != 0 && Game1.uniqueIDForThisGame == this.NativeWorld
        && Constants.SaveFolderName == this.NativeFolder, "The current world is not the newly created disposable farm.");

    private static bool ExactFarmer() => Player is not null && Player.Name == FarmName && Player.farmName.Value == FarmName
        && Player.favoriteThing.Value == Favorite;
    private static int Count(string id) => Player.Items.CountId(id);

    private void Record(string step)
    {
        this.Evidence.Add(step);
        this.Monitor.Log(step, LogLevel.Info);
    }

    private void Finish(string? failure)
    {
        this.Finished = true;
        if (this.GameOptions is not null) this.GameOptions.pauseWhenOutOfFocus = this.PreviousPause;
        if (failure is not null) this.Monitor.Log(failure, LogLevel.Error);
        this.Helper.Data.WriteJsonFile("p3-fresh-install-result.json", new
        {
            SchemaVersion = 1, ProcessId = Environment.ProcessId, FarmName, this.NativeFolder, this.NativeWorld,
            Completed = failure is null, Failure = failure, Stage = this.Current.ToString(), this.CreatingEvents,
            this.CreatedEvents, this.LoadEvents, this.SavingEvents, this.SavedEvents,
            this.StartedAt, this.CollectedAt, this.InitialSaveHashes, this.Evidence,
            Preparation = "The fixture fills the stock character creation text boxes and clicks original buttons. After the initial native save, it adds funds and a copper bar and sets placement and menu positions. Vendor purchases, game clock, placement and transactions are native. Later progress stays in memory."
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
