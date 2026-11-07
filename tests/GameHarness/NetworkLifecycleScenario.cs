using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>
/// 一次进程内同ID重连的被动观察；不发包/自动加载/操控玩家/冻结时间/保存。
/// Host保留原世界等待client离开再回来；Client保留首次Ready及标题快照，第二次SaveLoaded不重置历史。
/// 任意额外读档、错世界、队列丢弃、trace淘汰、指纹改变或五分钟截止均失败，不拼接新进程。
/// </summary>
internal sealed class NetworkLifecycleScenario
{
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly Config Settings;
    private readonly bool PersistReports;
    private readonly Guid RunId = Guid.NewGuid();
    private readonly Stopwatch Clock = new();
    private readonly string ProductionHash = Sha(File.ReadAllBytes(typeof(MachineService).Assembly.Location));
    private readonly string HarnessHash = Sha(File.ReadAllBytes(typeof(ModEntry).Assembly.Location));
    private MachineService? Service;
    private LifecycleCheckpoint? Initial, Gap, Rejoined, ClosingTitle;
    private Item?[]? EpisodeReferences;
    private string Status = "NotStarted";
    private string? Failure;
    private int LoadCount;
    private long LastReport = -1;
    private bool Host => this.Settings.NetworkRole == "Host";
    private string ReportName => $"network-lifecycle-{this.RunId:N}.json";

    public NetworkLifecycleScenario(IModHelper helper, IMonitor monitor, Config settings, bool persistReports = true)
    { this.Helper = helper; this.Monitor = monitor; this.Settings = settings; this.PersistReports = persistReports; }

    public void OnLoaded()
    {
        try
        {
            if (this.Failure is not null) return;
            this.LoadCount++;
            Require(this.ScopeMatches(), "Lifecycle observer requires the configured independent multiplayer world/role and preview=false.");
            if (this.LoadCount == 1)
            {
                this.Clock.Start(); this.Status = "WaitingInitialReady";
                this.Monitor.Log($"Passive reconnect observation started for five minutes: {this.ReportName}", LogLevel.Info);
            }
            else Require(!this.Host && this.LoadCount == 2 && this.Status == "WaitingReconnect" && this.Gap is not null,
                "Unexpected SaveLoaded; do not replace or splice a lifecycle observation.");
            // Native client reload reconstructs Item objects. Reference preservation is per load;
            // value/raw hashes remain bound to the initial Ready across the reconnect.
            this.EpisodeReferences = null;
            if (this.LoadCount == 1) this.Tick(); else this.Report();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { this.Fail(ex.Message); }
    }

    public void Tick()
    {
        if (!this.Clock.IsRunning || this.Failure is not null || this.Status == "Completed") return;
        try
        {
            Require(this.Clock.ElapsedMilliseconds < NetworkLifecycleEvidence.DurationMilliseconds,
                "Lifecycle observation exceeded five minutes; retain this run and restart independently.");
            if (!Context.IsWorldReady)
            {
                Require(!this.Host && this.Status == "WaitingReconnect" && this.Gap is not null,
                    "World disappeared before an observed client title transition.");
                if (this.Clock.ElapsedMilliseconds - this.LastReport >= 1000) this.Report();
                return;
            }
            Require(this.ScopeMatches(), "Observation world/role changed.");
            this.Service ??= this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
                ?? throw new InvalidOperationException("Main module diagnostic API is unavailable.");
            var snapshot = this.Service.ReadNetworkDiagnostics();
            Require(!snapshot.Faulted && snapshot.ProbeAllowed && !snapshot.WritesEnabled
                && snapshot.QueueRejected == 0 && snapshot.Evicted == 0, "Diagnostic gate closed or evidence was lost.");
            if (this.Initial is null)
            {
                if (NetworkLifecycleEvidence.Ready(snapshot, this.Host))
                {
                    this.EpisodeReferences = Game1.player.Items.ToArray();
                    this.Initial = this.Capture(snapshot);
                    this.Status = this.Host ? "WaitingDisconnect" : "WaitingTitle";
                    this.Monitor.Log("First actual Ready recorded; waiting for client title/disconnect.", LogLevel.Info);
                }
            }
            else
            {
                // SaveLoaded resets the production bridge before its next UpdateTicked creates
                // a client session. A valid second load is not a new Ready observation yet.
                if (!this.Host && this.LoadCount == 2 && !snapshot.Active)
                { if (this.Clock.ElapsedMilliseconds - this.LastReport >= 1000) this.Report(); return; }
                Require(snapshot.RunId == this.Initial.Protocol.RunId
                    && snapshot.LocalPlayerId == this.Initial.Protocol.LocalPlayerId,
                    "Local protocol or farmer identity changed across this observation.");
                if (this.EpisodeReferences is null) this.EpisodeReferences = Game1.player.Items.ToArray();
                var point = this.Capture(snapshot);
                Require(point.InventorySha256 == this.Initial.InventorySha256
                    && point.VisibleMachineRawSha256 == this.Initial.VisibleMachineRawSha256
                    && point.VisibleMachineCount == this.Initial.VisibleMachineCount
                    && point.InventoryReferencesUnchanged is true, "Local inventory/reference scope or visible raw changed.");
                if (this.Host && this.Gap is null && NetworkLifecycleEvidence.HostGap(this.Initial.Protocol, snapshot))
                {
                    this.Gap = point; this.Status = "WaitingReconnect";
                    this.Monitor.Log("Host observed the peer gap; original host world session retained.", LogLevel.Info);
                }
                else if (this.Gap is not null && NetworkLifecycleEvidence.Reconnected(this.Initial.Protocol, this.Gap.Protocol, snapshot, this.Host))
                {
                    this.Rejoined = point; this.Status = "Completed";
                    this.Monitor.Log("Same peer ID has a new connection and fresh actual handshake; FullNET02 remains false.", LogLevel.Info);
                }
                else if (this.Gap is null)
                    Require(snapshot.WorldGeneration == this.Initial.Protocol.WorldGeneration
                        && (snapshot.Peers.Count == 0 || snapshot.Peers.All(p => p.PlayerId == this.Initial.Protocol.Peers[0].PlayerId
                            && p.ConnectionId == this.Initial.Protocol.Peers[0].ConnectionId)),
                        "Connection changed without a captured disconnect/title gap.");
            }
            if (this.Status == "Completed" || this.Clock.ElapsedMilliseconds - this.LastReport >= 1000) this.Report();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { this.Fail(ex.Message); }
    }

    public void OnReturnedToTitle()
    {
        if (!this.Clock.IsRunning || this.Failure is not null) return;
        try
        {
            var snapshot = this.Service?.ReadNetworkDiagnostics()
                ?? throw new InvalidOperationException("Title arrived before an actual diagnostic Ready.");
            var point = new LifecycleCheckpoint(snapshot, this.LoadCount, 0, null, null, null, null, null);
            if (this.Status == "Completed") this.ClosingTitle = point;
            else
            {
                Require(!this.Host && this.LoadCount == 1 && this.Initial is not null && this.Gap is null
                    && NetworkLifecycleEvidence.ClientTitle(this.Initial.Protocol, snapshot),
                    "Unexpected/uncleared title; original lifecycle cannot be resumed.");
                this.Gap = point; this.EpisodeReferences = null; this.Status = "WaitingReconnect";
                this.Monitor.Log("Client title cleared the local session; history retained for one same-process rejoin.", LogLevel.Info);
            }
            this.Report();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { this.Fail(ex.Message); }
    }

    public void OnSaving() => this.Fail("Native saving was observed during a no-save diagnostic; inspect disk separately.");

    private bool ScopeMatches() => NetworkLifecycleEvidence.ScopeMatches(Context.IsWorldReady, Context.IsMainPlayer,
        Context.IsSplitScreen, Context.ScreenId, this.Settings.OpenPreview, this.Settings.NetworkRole,
        this.Settings.FarmName, this.Settings.SaveFixtureId, Context.IsWorldReady ? Game1.player.farmName.Value : null,
        Context.IsWorldReady ? Game1.uniqueIDForThisGame : 0);

    // 独立重放模式在被动观测完成后读取不可变原检查点，再单独记录有副作用的重放阶段。
    // 这里不放宽被动门禁，也不替调用方发包。
    internal LifecycleCheckpoint Capture(ProtocolDiagnosticSnapshot snapshot) => new(snapshot, this.LoadCount,
        Game1.uniqueIDForThisGame, Game1.player.farmName.Value, Hash(Game1.player.Items.Select(item => item is null ? null : new
        { item.QualifiedItemId, item.Stack, item.Quality, Meta = item.modData.Pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray() }).ToArray()),
        Hash(Game1.locations.OrderBy(l => l.NameOrUniqueName, StringComparer.Ordinal).SelectMany(l => l.objects.Pairs
            .Where(p => MachineService.TryGetKind(p.Value, out _)).OrderBy(p => p.Key.X).ThenBy(p => p.Key.Y)
            .Select(p => new { Location = l.NameOrUniqueName, p.Key.X, p.Key.Y, p.Value.QualifiedItemId,
                HasRaw = p.Value.modData.ContainsKey(MachineService.StateKey), Raw = p.Value.modData.GetValueOrDefault(MachineService.StateKey) })).ToArray()),
        Game1.locations.Sum(l => l.objects.Values.Count(o => MachineService.TryGetKind(o, out _))),
        this.EpisodeReferences is not null && this.EpisodeReferences.Length == Game1.player.Items.Count
            && this.EpisodeReferences.Select((item, i) => ReferenceEquals(item, Game1.player.Items[i])).All(value => value));

    private void Fail(string reason)
    {
        if (this.Failure is not null) return;
        this.Failure = reason; this.Status = "Failed";
        this.Monitor.Log(reason, LogLevel.Warn); this.Report();
    }
    private void Report()
    {
        this.LastReport = this.Clock.ElapsedMilliseconds;
        // 复用观测器的主动重放模式仅写自己的报告，避免重放后另落一个ProbeSent=false的被动最终报告。
        if (this.PersistReports) this.Helper.Data.WriteJsonFile(this.ReportName, this.ReadReport());
    }
    internal NetworkLifecycleReport ReadReport() => new(1, this.RunId, Environment.ProcessId,
            this.Settings.NetworkRole, this.Settings.SaveFixtureId, this.Settings.FarmName, this.ProductionHash,
            this.HarnessHash, this.Status, this.Failure, false, false, false, false, false,
            this.Initial, this.Gap, this.Rejoined, this.ClosingTitle);
    private static string Hash<T>(T value) => Sha(JsonSerializer.SerializeToUtf8Bytes(value));
    private static string Sha(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
}
