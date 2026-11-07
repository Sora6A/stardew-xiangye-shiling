using System.Diagnostics;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>
/// 独立、默认禁用的主动重放模式。先复用完全被动的同进程重连观测，冻结原三检查点，
/// 再由Host等待两秒后调用固定旧Welcome入口一次；Client必须已留重放前新Ready快照并实际读到拒绝。
/// 不加载/操控/冻结时间/保存/修改物品。五分钟从首次SaveLoaded计，失败或超时不恢复不拼接。
/// </summary>
internal sealed class NetworkWelcomeReplayScenario
{
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Settings;
    private readonly NetworkLifecycleScenario Lifecycle;
    private readonly Stopwatch Clock = new();
    private NetworkLifecycleReport? FrozenLifecycle;
    private LifecycleCheckpoint? Before, After;
    private string Status = "WaitingLifecycle";
    private string? Failure;
    private bool Saved, Attempted;
    private long ReadyAt, LastReport = -1;
    private bool Host => this.Settings.NetworkRole == "Host";

    public NetworkWelcomeReplayScenario(IModHelper helper, IMonitor monitor, Config settings)
    {
        this.Helper = helper; this.Monitor = monitor; this.Settings = settings;
        this.Lifecycle = new(helper, monitor, settings, persistReports: false);
    }
    public void OnLoaded()
    {
        if (this.Failure is not null) return;
        if (!this.Clock.IsRunning) this.Clock.Start();
        this.Lifecycle.OnLoaded(); this.Tick();
    }
    public void Tick()
    {
        if (!this.Clock.IsRunning || this.Failure is not null || this.Status == "Completed") return;
        try
        {
            Require(this.Clock.ElapsedMilliseconds < NetworkLifecycleEvidence.DurationMilliseconds, "Welcome replay exceeded five minutes.");
            this.Lifecycle.Tick();
            var life = this.Lifecycle.ReadReport();
            Require(life.Failure is null, life.Failure ?? "Passive lifecycle failed.");
            if (life.Status != "Completed") { this.PeriodicReport(); return; }
            Require(NetworkLifecycleEvidence.ScopeMatches(Context.IsWorldReady, Context.IsMainPlayer, Context.IsSplitScreen,
                Context.ScreenId, this.Settings.OpenPreview, this.Settings.NetworkRole, this.Settings.FarmName,
                this.Settings.SaveFixtureId, Game1.player.farmName.Value, Game1.uniqueIDForThisGame), "Replay world/role changed.");
            var service = this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
                ?? throw new InvalidOperationException("Production diagnostic API unavailable.");
            if (this.FrozenLifecycle is null)
            {
                this.FrozenLifecycle = life;
                this.Before = this.Lifecycle.Capture(service.ReadNetworkDiagnostics());
                Require(NetworkLifecycleEvidence.Ready(this.Before.Protocol, this.Host), "Replay requires an actual fresh Ready before the send.");
                this.ReadyAt = this.Clock.ElapsedMilliseconds;
                this.Status = this.Host ? "WaitingReplayDelay" : "WaitingOldWelcome";
                this.Report();
            }
            if (this.Host)
            {
                // 给客户端机会先捕获实际新Welcome。没有捕获到的轮次必须失败，不能事后补造Before。
                if (this.Clock.ElapsedMilliseconds - this.ReadyAt < 2000) return;
                Require(!this.Attempted && service.TryReplayPreviousNetworkWelcome(this.Before!.Protocol.Peers[0].ConnectionId),
                    "Fixed old Welcome replay was not attempted; do not substitute caller-generated JSON.");
                this.Attempted = true;
                this.After = this.Lifecycle.Capture(service.ReadNetworkDiagnostics());
                this.Status = "Completed";
                Require(NetworkWelcomeReplayEvidence.Endpoint(this.ReadReport(), true), "Host replay trace/state did not match the fixed old challenge.");
                this.Monitor.Log("One old Welcome send attempt recorded on the new connection; client evidence is still required.", LogLevel.Info);
                this.Report();
            }
            else
            {
                var snapshot = service.ReadNetworkDiagnostics();
                Require(NetworkLifecycleEvidence.Healthy(snapshot, allowWelcomeReplay: true), "Client replay evidence lost or bridge faulted.");
                if (snapshot.LastSequence == this.Before!.Protocol.LastSequence) { this.PeriodicReport(); return; }
                this.After = this.Lifecycle.Capture(snapshot); this.Status = "Completed";
                Require(NetworkWelcomeReplayEvidence.Endpoint(this.ReadReport(), false), "Client did not read and reject the bound old Welcome with state unchanged.");
                this.Monitor.Log("Actual old Welcome read and rejected after fresh Ready; full NET02 remains false.", LogLevel.Info);
                this.Report();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { this.Fail(ex.Message); }
    }
    public void OnReturnedToTitle()
    {
        this.Lifecycle.OnReturnedToTitle();
        if (this.FrozenLifecycle is not null && this.Status != "Completed") this.Fail("Title interrupted the replay stage.");
    }
    public void OnSaving()
    {
        this.Saved = true; this.Lifecycle.OnSaving(); this.Fail("Native saving observed during no-save replay; inspect disk independently.");
    }
    private NetworkWelcomeReplayReport ReadReport() => new(1, this.FrozenLifecycle ?? this.Lifecycle.ReadReport(),
        this.Status, this.Failure, this.Saved, this.Attempted, false, false, this.Before, this.After);
    private void PeriodicReport() { if (this.Clock.ElapsedMilliseconds - this.LastReport >= 1000) this.Report(); }
    private void Report()
    {
        this.LastReport = this.Clock.ElapsedMilliseconds;
        var report = this.ReadReport();
        this.Helper.Data.WriteJsonFile($"network-welcome-replay-{report.Lifecycle.HarnessRunId:N}.json", report);
    }
    private void Fail(string reason)
    {
        if (this.Failure is not null) return;
        this.Failure = reason; this.Status = "Failed"; this.Monitor.Log(reason, LogLevel.Warn); this.Report();
    }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
}
