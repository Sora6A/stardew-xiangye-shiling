using System.Diagnostics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace GameHarness;

/// <summary>
/// 被动观察实际SDK输入链：MonoGame轮询、游戏InputState、聊天过滤结果以及UpdateControlInput旧帧。
/// 这些读数在同一回调中依次取得，不声称原子快照或操作系统原始输入；5Hz可能漏掉短按。
/// 不订阅推进动作、不模拟输入、不自动加载、不改CanMove/时钟，也不触发保存或API事务。
/// 每进程只观察首次SaveLoaded，世界离开/重载/异常即停止，不能拼接下一世界的证据。
/// </summary>
internal sealed class InputObservationScenario
{
    private static readonly Keys[] AllowedKeys = { Keys.W, Keys.A, Keys.S, Keys.D, Keys.E, Keys.Escape };
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Settings;
    private readonly Stopwatch Clock = new();
    private readonly List<object> Samples = new();
    private readonly List<object> Events = new();
    private readonly string ReportName = $"input-observation-{Guid.NewGuid():N}.json";
    private bool Attempted, Watching;
    private long LastSampleMs = -1, LastReportMs = -1;
    private int RedactedEvents, DroppedEvents;
    private string Status = "NotStarted";
    private string? Failure;
    private DateTimeOffset? StartedAt;

    public InputObservationScenario(IModHelper helper, IMonitor monitor, Config settings)
    { this.Helper = helper; this.Monitor = monitor; this.Settings = settings; }

    public void OnLoaded()
    {
        if (this.Attempted)
        {
            if (this.Watching) this.Stop("Stopped", "SaveLoaded occurred again; this episode cannot be reused.");
            return;
        }
        this.Attempted = true;
        try
        {
            if (!this.ScopeMatches())
            {
                this.Stop("Rejected", "Requires the configured XYSNET save folder, nonzero world ID and main player; preview/split screen are forbidden.");
                return;
            }
            this.StartedAt = DateTimeOffset.UtcNow;
            this.Clock.Start(); this.Watching = true; this.Status = "Observing";
            this.Monitor.Log($"Read-only input observation started for 180 seconds: {this.ReportName}", LogLevel.Info);
            this.Tick();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { this.Stop("Faulted", ex.Message); }
    }

    public void Tick()
    {
        if (!this.Watching) return;
        try
        {
            if (!this.ScopeMatches()) { this.Stop("Stopped", "Observation world/ownership is no longer valid."); return; }
            long elapsed = this.Clock.ElapsedMilliseconds;
            if (elapsed >= InputObservationPolicy.DurationMs) { this.Stop("Completed", null); return; }
            if (InputObservationPolicy.CanSample(elapsed, this.LastSampleMs, this.Samples.Count))
            {
                this.Samples.Add(this.Capture(elapsed)); this.LastSampleMs = elapsed;
            }
            if (this.LastReportMs < 0 || elapsed - this.LastReportMs >= 1000) this.WriteReport();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { this.Stop("Faulted", ex.Message); }
    }

    public void OnButtonPressed(SButton button)
    {
        if (!this.Watching || !InputObservationPolicy.IsAllowedKey(button.ToString())) return;
        try
        {
            if (!this.ScopeMatches()) { this.Stop("Stopped", "Input event belongs to an invalid world/owner."); return; }
            long elapsed = this.Clock.ElapsedMilliseconds;
            if (elapsed >= InputObservationPolicy.DurationMs) { this.Stop("Completed", null); return; }
            if (!InputObservationPolicy.CanRecordKeys(Game1.IsChatting, Game1.keyboardDispatcher?.Subscriber is not null))
            { this.RedactedEvents++; return; }
            if (!InputObservationPolicy.CanRecordEvent(elapsed, this.Events.Count)) { this.DroppedEvents++; return; }
            // IsSuppressed只读SMAPI现态；不调用Suppress/IgnoreKeys。事件前后阶段不与UpdateTicked强行对齐。
            this.Events.Add(new { ElapsedMs = elapsed, Button = button.ToString(),
                SuppressedAtCallback = this.Helper.Input.IsSuppressed(button), State = this.Capture(elapsed) });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { this.Stop("Faulted", ex.Message); }
    }

    public void OnReturnedToTitle()
    { if (this.Watching) this.Stop("Stopped", "ReturnedToTitle before the observation deadline."); }

    private bool ScopeMatches() => InputObservationPolicy.ScopeMatches(Context.IsWorldReady, Context.IsMainPlayer,
        Context.IsSplitScreen, Context.ScreenId, this.Settings.OpenPreview, this.Settings.FarmName,
        this.Settings.SaveFixtureId, Context.IsWorldReady ? Game1.player.farmName.Value : null,
        Context.IsWorldReady ? Game1.uniqueIDForThisGame : 0, Constants.SaveFolderName);

    private object Capture(long elapsed)
    {
        bool chatting = Game1.IsChatting;
        string? subscriber = Game1.keyboardDispatcher?.Subscriber?.GetType().FullName;
        bool recordKeys = InputObservationPolicy.CanRecordKeys(chatting, subscriber is not null);
        string[] Filter(KeyboardState state) => AllowedKeys.Where(state.IsKeyDown).Select(k => k.ToString()).ToArray();
        var player = Game1.player;
        return new
        {
            ElapsedMs = elapsed, Tick = Game1.ticks, TimeOfDay = Game1.timeOfDay,
            Day = Game1.dayOfMonth, Season = Game1.currentSeason, Year = Game1.year,
            Active = Game1.game1.IsActive, ActiveWithoutOverlay = Game1.game1.IsActiveNoOverlay,
            KeyboardFocus = Game1.game1.HasKeyboardFocus(), Chatting = chatting, TextSubscriberType = subscriber,
            KeysRedacted = !recordKeys,
            // 仅六键，文本输入时不读取键盘。InputState可能已被SMAPI替换/抑制；不能称作OS原始键。
            MonoGameKeys = recordKeys ? Filter(Keyboard.GetState()) : null,
            InputStateKeys = recordKeys ? Filter(Game1.input.GetKeyboardState()) : null,
            ChatFilteredKeys = recordKeys ? Filter(Game1.GetKeyboardState()) : null,
            PreviousControlKeys = recordKeys ? Filter(Game1.oldKBState) : null,
            SmapiDown = recordKeys ? AllowedKeys.Where(k => this.Helper.Input.IsDown(Enum.Parse<SButton>(k.ToString())))
                .Select(k => k.ToString()).ToArray() : null,
            SmapiSuppressed = recordKeys ? AllowedKeys.Where(k => this.Helper.Input.IsSuppressed(Enum.Parse<SButton>(k.ToString())))
                .Select(k => k.ToString()).ToArray() : null,
            MenuType = Game1.activeClickableMenu?.GetType().FullName,
            FreezeControls = Game1.freezeControls, GlobalFade = Game1.globalFade,
            Paused = Game1.paused, PauseTime = Game1.pauseTime, NewDay = Game1.newDay,
            EventUp = Game1.eventUp, FarmEventType = Game1.farmEvent?.GetType().FullName,
            LocationRequestPending = Game1.locationRequest is not null,
            Location = player.currentLocation?.NameOrUniqueName,
            CanMove = player.CanMove, UsingTool = player.UsingTool, Eating = player.isEating,
            Sitting = player.IsSitting(), FreezePause = player.freezePause, NoMovementPause = player.noMovementPause,
            AnimationPaused = player.FarmerSprite.PauseForSingleAnimation,
            ControllerType = player.controller?.GetType().FullName,
            InBed = player.isInBed.Value, TimeWentToBed = player.timeWentToBed.Value, PassedOut = player.passedOut,
            X = player.Position.X, Y = player.Position.Y, MovementDirections = player.movementDirections.ToArray(),
            GamepadControls = Game1.options.gamepadControls, PauseWhenOutOfFocus = Game1.options.pauseWhenOutOfFocus
        };
    }

    private void Stop(string status, string? failure)
    {
        this.Watching = false; this.Status = status; this.Failure = failure; this.Clock.Stop();
        this.WriteReport();
    }

    private void WriteReport()
    {
        try
        {
            // 仅写本进程独有的报告名，既有报告不覆盖；写盘失败停止观察并明确日志，不能谎称有报告。
            this.Helper.Data.WriteJsonFile(this.ReportName, new
            {
                SchemaVersion = 1, Scenario = "InputObservation", Status = this.Status, Failure = this.Failure,
                StartedAtUtc = this.StartedAt, ElapsedMs = this.Clock.ElapsedMilliseconds,
                ConfiguredFarm = this.Settings.FarmName, ConfiguredWorldId = this.Settings.SaveFixtureId,
                DurationMs = InputObservationPolicy.DurationMs, SampleIntervalMs = InputObservationPolicy.SampleIntervalMs,
                MaxSamples = InputObservationPolicy.MaxSamples, MaxEvents = InputObservationPolicy.MaxEvents,
                SavedByObserver = false, PlayerStateWritten = false, InputSimulated = false, NetworkProbesSent = false,
                InputRecoveryVerified = false, FullNet02Accepted = false,
                this.RedactedEvents, this.DroppedEvents, Samples = this.Samples.ToArray(), Events = this.Events.ToArray()
            });
            this.LastReportMs = this.Clock.ElapsedMilliseconds;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            this.Watching = false; this.Status = "ReportWriteFailed"; this.Failure = ex.Message; this.Clock.Stop();
            this.Monitor.Log($"Input observation report write failed; any existing file may be incomplete: {ex.Message}", LogLevel.Error);
        }
    }
}
