using System.Diagnostics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace GameHarness;

/// <summary>
/// P1 自然工时子集。只在明确绑定的专用单人档准备两台设备和库存，
/// 以生产 API 开工、领取。等待阶段只读取原版 TimeChanged，不设置游戏时钟或调用推进函数。
/// </summary>
internal sealed class NaturalClockScenario
{
    private const string Prefix = MachineService.Prefix;
    private const string FarmName = "XYSP1Menu";
    private const string Soy = "(O)" + Prefix + "_Soybean";
    private const string Wheat = "(O)262";
    private const string Tofu = "(O)" + Prefix + "_Tofu";
    private const string Sauce = "(O)" + Prefix + "_SoySauce";
    private const int BeanMinutes = 120, CrockMinutes = 240, ClockStep = 10;
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Settings;
    private readonly Stopwatch Watch = new();
    private readonly List<ClockSample> Samples = new();
    private readonly List<string> Passed = new();
    private readonly List<SObject> Fixtures = new();
    private Farmer? Player;
    private GameLocation? Location;
    private Item[]? Original;
    private MachineService Service = null!;
    private SObject Bean = null!, Crock = null!;
    private string BeanRaw = "", CrockRaw = "";
    private long StartedAt;
    private int StartClock, LastClock;
    private bool Active, Done, SavingObserved, Restored;

    private sealed record ClockSample(int OldTime, int NewTime, int ElapsedMinutes, long WallMilliseconds,
        string BeanPhase, string CrockPhase);

    public NaturalClockScenario(IModHelper helper, IMonitor monitor, Config settings)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        this.Settings = settings;
    }

    private bool MatchesWorld() => Context.IsWorldReady && Context.IsMainPlayer && !Context.IsMultiplayer
        && !this.Settings.OpenPreview && this.Settings.FarmName == FarmName && Game1.player.farmName.Value == FarmName
        && this.Settings.SaveFixtureId != 0 && Game1.uniqueIDForThisGame == this.Settings.SaveFixtureId
        && Constants.SaveFolderName == $"{FarmName}_{this.Settings.SaveFixtureId}";

    public void OnLoaded()
    {
        if (this.Done || this.Active || !this.MatchesWorld()) return;
        this.Player = Game1.player;
        this.Active = true;
        this.Watch.Start();
    }

    public void Tick()
    {
        if (!this.Active) return;
        this.Guard(() =>
        {
            Require(this.MatchesWorld() && ReferenceEquals(this.Player, Game1.player), "Dedicated world changed.");
            Require(this.Watch.Elapsed < TimeSpan.FromMinutes(6), "Timed out waiting for native time; keep the game active and menus closed.");
            if (this.Original is null && Context.IsPlayerFree) this.Prepare();
        });
    }

    private void Prepare()
    {
        Farmer player = this.Player!;
        GameLocation location = this.Location = player.currentLocation;
        Require(Game1.timeOfDay >= 600 && Game1.timeOfDay <= 2000 && Game1.activeClickableMenu is null,
            "Load a daytime fixture with no open menu.");
        this.Service = this.Helper.ModRegistry.GetApi(Prefix) as MachineService
            ?? throw new InvalidOperationException("Production MachineService is unavailable.");
        this.Original = player.Items.ToArray();
        var slots = new Item[player.MaxItems];
        slots[0] = ItemRegistry.Create(Soy, 6);
        slots[1] = ItemRegistry.Create(Wheat, 2);
        player.Items.OverwriteWith(slots);

        this.Bean = Place("BeanWorkshop");
        this.Crock = Place("FermentationCrock");
        this.StartedAt = MachineService.Now();
        this.LastClock = this.StartClock = Game1.timeOfDay;
        this.BeanRaw = Start(this.Bean, "Tofu", BeanMinutes);
        this.CrockRaw = Start(this.Crock, "SoySauce", CrockMinutes);
        this.ExpectStock(0, 0);
        this.Pass("started both at the same game minute and consumed 6 soybeans and 2 wheat");
        this.WriteReport(false, false, null);

        SObject Place(string key)
        {
            // Fixtures only occupy previously empty neighboring tiles. They aren't native placement evidence.
            var machine = ItemRegistry.Create<SObject>("(BC)" + Prefix + "_" + key);
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                Vector2 tile = player.Tile + new Vector2(x, y);
                if (tile == player.Tile || !location.isTileOnMap(tile) || location.objects.ContainsKey(tile)
                    || !Utility.playerCanPlaceItemHere(location, machine, (int)tile.X * 64 + 32, (int)tile.Y * 64 + 32, player)) continue;
                machine.TileLocation = tile;
                location.objects.Add(tile, machine);
                this.Fixtures.Add(machine);
                MachineService.Initialize(machine);
                return machine;
            }
            throw new InvalidOperationException("Two empty neighboring fixture tiles are required.");
        }

        string Start(SObject machine, string recipe, int duration)
        {
            var state = State(machine);
            var result = this.Service.Execute(machine, player, NewRequest(state), Prefix + "_Recipe_" + recipe);
            Require(result.Applied, recipe + " start failed: " + result.Message);
            var batch = State(machine).Batch;
            Require(batch?.StartedAt == this.StartedAt && batch.ReadyAt == this.StartedAt + duration,
                recipe + " stored the wrong start or ready time.");
            return machine.modData[MachineService.StateKey];
        }
    }

    public void OnTimeChanged(int oldTime, int newTime)
    {
        if (!this.Active || this.BeanRaw.Length == 0 || this.CrockRaw.Length == 0) return;
        this.Guard(() =>
        {
            Require(this.MatchesWorld() && ReferenceEquals(this.Player, Game1.player) && Game1.activeClickableMenu is null,
                "Time changed outside the bound player or with a menu open.");
            int elapsed = (this.Samples.Count + 1) * ClockStep;
            Require(oldTime == this.LastClock && newTime == Game1.timeOfDay && MachineService.Now() == this.StartedAt + elapsed,
                "Clock skipped or repeated a native ten-minute step.");
            this.LastClock = newTime;

            MachinePhase bean = Observe(this.Bean, this.BeanRaw, BeanMinutes);
            MachinePhase crock = Observe(this.Crock, this.CrockRaw, CrockMinutes);
            this.Samples.Add(new(oldTime, newTime, elapsed, this.Watch.ElapsedMilliseconds, bean.ToString(), crock.ToString()));
            this.ExpectStock(elapsed > BeanMinutes ? 2 : 0, 0);
            if (elapsed == BeanMinutes)
            {
                this.CheckCollection(this.Bean, true);
                this.ExpectStock(2, 0);
                this.Pass("bean collected exactly 2 tofu after 12 native ticks; crock still processing");
                this.WriteReport(false, false, null);
            }
            if (elapsed == CrockMinutes)
            {
                this.CheckCollection(this.Crock, true);
                this.ExpectStock(2, 4);
                this.Pass("crock collected exactly 4 soy sauce after 24 native ticks");
                this.Finish(null);
            }

            MachinePhase Observe(SObject machine, string raw, int duration)
            {
                Require(this.Location!.objects.TryGetValue(machine.TileLocation, out var placed) && ReferenceEquals(placed, machine),
                    "Fixture machine moved during the wait.");
                var state = State(machine);
                var phase = state.PhaseAt(MachineService.Now());
                var expected = elapsed < duration ? MachinePhase.Processing : elapsed == duration ? MachinePhase.Ready : MachinePhase.Idle;
                Require(phase == expected, state.Kind + " wrong phase at minute " + elapsed);
                if (phase != MachinePhase.Idle)
                    Require(machine.modData[MachineService.StateKey] == raw, state.Kind + " pending batch changed during waiting.");
                if (elapsed == duration - ClockStep)
                {
                    this.CheckCollection(machine, false);
                    this.Pass(state.Kind + " rejected collection one native tick before its deadline");
                }
                return phase;
            }
        });
    }

    private void CheckCollection(SObject machine, bool shouldApply)
    {
        var before = State(machine);
        string raw = machine.modData[MachineService.StateKey];
        Item[] inventory = this.Player!.Items.ToArray();
        int[] counts = inventory.Select(item => item?.Stack ?? 0).ToArray();
        var result = this.Service.Execute(machine, this.Player, this.NewRequest(before), null);
        Require(result.Applied == shouldApply, before.Kind + " unexpected collection result: " + result.Message);
        if (!shouldApply)
        {
            Require(machine.modData[MachineService.StateKey] == raw && this.Player.Items.SequenceEqual(inventory)
                && this.Player.Items.Select(item => item?.Stack ?? 0).SequenceEqual(counts),
                before.Kind + " early collection changed machine or inventory.");
            return;
        }
        var after = State(machine);
        Require(after.Batch is null && after.InstanceId == before.InstanceId && after.Revision == before.Revision + 1,
            before.Kind + " did not clear exactly one batch.");
        Require(!this.Service.Execute(machine, this.Player, this.NewRequest(after), null).Applied
            && State(machine) == after, before.Kind + " collected a second time or changed empty state.");
    }

    private Request NewRequest(MachineState state) => new(Guid.NewGuid(), state.InstanceId, state.Revision, this.Player!.UniqueMultiplayerID);

    private static MachineState State(SObject machine) => MachineService.TryRead(machine, out var state)
        ? state! : throw new InvalidOperationException("Invalid fixture machine state.");

    private void ExpectStock(int tofu, int sauce)
    {
        int Count(string id) => this.Player!.Items.Where(item => item?.QualifiedItemId == id).Sum(item => item.Stack);
        Require(Count(Soy) == 0 && Count(Wheat) == 0 && Count(Tofu) == tofu && Count(Sauce) == sauce, "Unexpected ingredient or product counts.");
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { this.Finish(ex.Message); }
    }

    private void Pass(string message)
    {
        this.Passed.Add(message);
        this.Monitor.Log("NaturalClock PASS: " + message, LogLevel.Info);
    }

    public void OnSaving()
    {
        if (!this.MatchesWorld() || this.Player is null) return;
        this.SavingObserved = true;
        if (this.Active) this.Finish("Saving observed during clock verification.");
        else if (this.Done) this.WriteReport(false, this.Restored, "Saving observed after clock verification.");
    }

    public void OnReturnedToTitle()
    {
        if (this.Active) this.Finish("Returned to title before the native clock check finished.");
    }

    private void Finish(string? failure)
    {
        this.Active = false;
        this.Done = true;
        bool restored = this.Original is null;
        if (this.Original is not null && this.MatchesWorld() && ReferenceEquals(this.Player, Game1.player))
        {
            foreach (var machine in this.Fixtures)
            {
                // Don't remove anything that replaced a fixture while the clock was running.
                if (this.Location!.objects.TryGetValue(machine.TileLocation, out var current) && ReferenceEquals(current, machine))
                    this.Location.objects.Remove(machine.TileLocation);
            }
            this.Player!.Items.OverwriteWith(this.Original);
            restored = this.Player.Items.SequenceEqual(this.Original)
                && this.Fixtures.All(machine => !this.Location!.objects.Values.Any(current => ReferenceEquals(current, machine)));
        }
        this.Restored = restored;
        if (!restored && failure is null) failure = "Fixture objects or inventory could not be restored.";
        this.WriteReport(failure is null, this.Restored, failure);
        this.Monitor.Log(failure is null ? "NaturalClock completed; close the test process without saving."
            : "NaturalClock failed: " + failure, failure is null ? LogLevel.Info : LogLevel.Error);
    }

    private void WriteReport(bool completed, bool restored, string? failure) => this.Helper.Data.WriteJsonFile("natural-clock-result.json", new
    {
        SchemaVersion = 1, ProcessId = Environment.ProcessId, FixtureId = this.Settings.SaveFixtureId,
        Completed = completed, Restored = restored, Failure = failure, this.SavingObserved,
        this.StartClock, this.Samples, this.Passed
    });

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
