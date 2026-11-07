using System.Security.Cryptography;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Networking;

namespace GameHarness;

/// <summary>
/// 默认关闭的NET-02真实传输观察器。只读取本地库存/本端可见机器raw，客户端发送固定合成意图；
/// 不准备材料/机器/时钟，不加载或保存农场。客户端六步只是基础probe，断线/换档需另留同进程证据。
/// </summary>
internal sealed class NetworkScenario
{
    private readonly IModHelper Helper;
    private readonly Config Settings;
    private readonly Guid RunId = Guid.NewGuid();
    private readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly List<string> Passed = new();
    private readonly List<ProbeCase> Cases = new();
    private readonly List<Guid> BurstIds = new();
    private MachineService? Service;
    private string? InventoryHash;
    private string? MachineHash;
    private Item?[]? InventoryReferences;
    private string? Failure;
    private long Deadline;
    private long NextSend;
    private long LastReport;
    private long InitialEvicted;
    private EpisodeBaseline? Baseline;
    private string? CurrentInventoryHash;
    private string? CurrentMachineHash;
    private bool InventoryReferencesUnchanged;
    private int Step;
    private int Episode;
    private bool Watching;
    private bool BurstSent;
    private bool BasicCompleted;
    private Guid? SessionId;
    private Guid? ProtocolRunId;
    private ProbeEvidenceContext? Evidence;
    private long BurstAfterSequence;
    private int VisibleMachineCount;
    private readonly string ProductionDllSha256 = Convert.ToHexString(SHA256.HashData(
        File.ReadAllBytes(typeof(MachineService).Assembly.Location))).ToLowerInvariant();

    public NetworkScenario(IModHelper helper, Config settings) { this.Helper = helper; this.Settings = settings; }

    public void OnLoaded()
    {
        this.Episode++;
        this.Passed.Clear(); this.Cases.Clear(); this.BurstIds.Clear();
        this.InventoryHash = this.MachineHash = this.Failure = null;
        this.InventoryReferences = null; this.BasicCompleted = this.BurstSent = false;
        this.Service = null; this.SessionId = this.ProtocolRunId = null; this.Step = 0;
        this.Evidence = null;
        this.Baseline = null;
        this.CurrentInventoryHash = this.CurrentMachineHash = null;
        this.InventoryReferencesUnchanged = false;
        this.BurstAfterSequence = 0;
        this.Watching = true;
        this.Deadline = this.Clock.ElapsedMilliseconds + 60000;
    }

    public void Tick()
    {
        if (!this.Watching || this.Failure is not null || !Context.IsWorldReady) return;
        // 本Tick读取成功才保存观测；读取前失败仍为null，不带入上一帧/连接的快照。
        ProtocolDiagnosticSnapshot? observed = null;
        try
        {
            Require(this.ScopeMatches(), "Network probe requires the configured independent XYSNET world and host/client role.");
            this.Service ??= this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
                ?? throw new InvalidOperationException("Main module diagnostic API is unavailable.");
            var snapshot = this.Service.ReadNetworkDiagnostics();
            observed = snapshot;
            Require(snapshot.ProbeConfigured && snapshot.ProbeAllowed && !snapshot.WritesEnabled && !snapshot.Faulted,
                "Main module probe gate is closed or diagnostic session is faulted.");
            if (this.Evidence is not null)
                Require(NetworkProbeEvidence.ContextMatches(snapshot, this.Evidence),
                    "Probe connection/generation or evidence buffer changed; do not splice episodes.");
            if (!snapshot.Active || snapshot.SessionId is null || !snapshot.Peers.Any(p => p.Ready))
            {
                Require(this.Clock.ElapsedMilliseconds < this.Deadline, "Timed out waiting for a real connected and ready peer.");
                this.Report(snapshot);
                return;
            }
            if (this.InventoryHash is null)
            {
                this.InventoryHash = HashInventory(); this.MachineHash = HashMachines();
                this.InventoryReferences = Game1.player.Items.ToArray();
                this.InitialEvicted = snapshot.Evicted;
                this.SessionId = snapshot.SessionId; this.ProtocolRunId = snapshot.RunId;
                this.Baseline = new(snapshot.RunId, snapshot.SessionId.Value, snapshot.WorldGeneration,
                    snapshot.DisconnectGeneration, snapshot.LocalPlayerId, Environment.CurrentManagedThreadId,
                    snapshot.QueueRejected, snapshot.Evicted, snapshot.LastSequence, snapshot.Peers.ToArray());
                this.VisibleMachineCount = Game1.locations.Sum(l => l.objects.Values.Count(o => MachineService.TryGetKind(o, out _)));
                if (!snapshot.IsHost)
                {
                    var host = snapshot.Peers.Single(p => p.IsHost && p.Ready);
                    this.Evidence = new(snapshot.RunId, snapshot.SessionId.Value, snapshot.WorldGeneration, snapshot.DisconnectGeneration,
                        host.PlayerId, host.ConnectionId, snapshot.QueueRejected, snapshot.Evicted, Environment.CurrentManagedThreadId);
                }
                this.NextSend = this.Clock.ElapsedMilliseconds + 1300;
                this.Deadline = this.Clock.ElapsedMilliseconds + 20000;
            }
            Require(snapshot.SessionId == this.SessionId && snapshot.RunId == this.ProtocolRunId,
                "Connection/world changed during this probe episode; restart and retain both reports.");
            Require(snapshot.Evicted == this.InitialEvicted, "Diagnostic trace overflowed; missing evidence cannot pass.");
            this.CurrentInventoryHash = HashInventory(); this.CurrentMachineHash = HashMachines();
            this.InventoryReferencesUnchanged = this.InventoryReferences!.Length == Game1.player.Items.Count
                && this.InventoryReferences.Where((item, i) => !ReferenceEquals(item, Game1.player.Items[i])).Any() == false;
            Require(this.InventoryHash == this.CurrentInventoryHash && this.MachineHash == this.CurrentMachineHash
                && this.InventoryReferencesUnchanged,
                "Local inventory references/values or visible machine raw changed during read-only probes.");
            if (!snapshot.IsHost && !this.BasicCompleted) this.RunClient(snapshot);
            this.Report(snapshot);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            this.Failure = ex.Message;
            // 这是本Tick检查前取得的观测，可能早于随后发包/异常；不再次读API冒充异常后的最终状态。
            // Failure保持非null，保留Protocol只供调查，不能让失败报告通过离线配对。
            this.Report(observed, force: true);
        }
    }

    private void RunClient(ProtocolDiagnosticSnapshot snapshot)
    {
        long now = this.Clock.ElapsedMilliseconds;
        if (this.Step < 5)
        {
            var kind = (ProtocolProbeKind)this.Step;
            if (this.Cases.Count == this.Step && now >= this.NextSend)
            {
                Guid id = Guid.NewGuid();
                Require(this.Service!.TrySendNetworkProbe(kind, id), "Probe send attempt was refused.");
                this.Cases.Add(new(kind, id, ProtocolProbePlan.ExpectedReason(kind), snapshot.LastSequence));
                this.Deadline = now + 10000;
            }
            if (this.Cases.Count > this.Step)
            {
                var probe = this.Cases[this.Step];
                Guid? expectedId = kind == ProtocolProbeKind.Malformed ? null : probe.RequestId;
                if (NetworkProbeEvidence.HasRejection(snapshot, this.Evidence!, probe.SentAfterSequence, expectedId, probe.Expected))
                {
                    this.Passed.Add(kind.ToString()); this.Step++;
                    this.NextSend = now + 1300;
                }
                else Require(now < this.Deadline, "No correlated real host rejection for " + kind);
            }
            return;
        }
        if (!this.BurstSent && now >= this.NextSend)
        {
            this.BurstAfterSequence = snapshot.LastSequence;
            // Nine sends in one client frame; real host trace must independently show eight reads plus one quota drop.
            for (int i = 0; i < 9; i++)
            {
                Guid id = Guid.NewGuid();
                Require(this.Service!.TrySendNetworkProbe(ProtocolProbeKind.Start, id), "Burst send attempt was refused.");
                this.BurstIds.Add(id);
            }
            this.BurstSent = true; this.NextSend = now + 3000;
        }
        if (this.BurstSent && now >= this.NextSend)
        {
            var responses = this.BurstIds.Where(id => NetworkProbeEvidence.HasRejection(snapshot, this.Evidence!,
                this.BurstAfterSequence, id, ProtocolRejection.WritesDisabled)).ToArray();
            Require(responses.Length == 8, "Nine-to-eight response observation failed; inspect host timing/trace before concluding quota behavior.");
            this.Passed.Add("BurstEightResponsesCandidate"); this.BasicCompleted = true; this.Step = 6;
        }
    }

    public void OnReturnedToTitle()
    {
        if (!this.Watching) return;
        var snapshot = this.Service?.ReadNetworkDiagnostics();
        this.Report(snapshot, force: true, title: true);
        this.Watching = false;
    }

    private bool ScopeMatches() => Context.IsMultiplayer && !Context.IsSplitScreen && Context.ScreenId == 0
        && this.Settings.SaveFixtureId != 0 && Game1.uniqueIDForThisGame == this.Settings.SaveFixtureId
        && this.Settings.FarmName is { Length: >= 6 and <= 20 }
        && this.Settings.FarmName.StartsWith("XYSNET", StringComparison.Ordinal)
        && Game1.player.farmName.Value == this.Settings.FarmName
        && (this.Settings.NetworkRole == "Host" ? Context.IsMainPlayer : this.Settings.NetworkRole == "Client" && !Context.IsMainPlayer);

    private void Report(ProtocolDiagnosticSnapshot? snapshot, bool force = false, bool title = false)
    {
        if (!force && this.Clock.ElapsedMilliseconds - this.LastReport < 1000) return;
        this.LastReport = this.Clock.ElapsedMilliseconds;
        // Keep the last active-world observation separate from title lifecycle evidence. An offline pair
        // must not silently accept an inactive title snapshot or lose the basic trace when the user exits.
        this.Helper.Data.WriteJsonFile(title ? "network-probe-title-result.json" : "network-probe-result.json", new
        {
            SchemaVersion = 2, HarnessRunId = this.RunId, ProcessId = Environment.ProcessId, this.Episode, this.Settings.NetworkRole,
            this.ProductionDllSha256, ExpectedFixtureId = this.Settings.SaveFixtureId, BaselineVisibleMachineCount = this.VisibleMachineCount,
            TargetIsSynthetic = true, BasicProbesCompleted = this.BasicCompleted, FullNet02Accepted = false,
            ReconnectVerified = false, ReturnedToTitleObserved = title,
            TitleSessionCleared = title && snapshot is { Active: false, SessionId: null, QueueCount: 0 },
            Saved = false, Failure = this.Failure, Step = this.Step, Passed = this.Passed,
            ProbeCases = this.Cases, BurstRequestIds = this.BurstIds,
            BurstSentAfterSequence = this.BurstAfterSequence, this.Baseline,
            BaselineInventorySha256 = this.InventoryHash, BaselineVisibleMachineRawSha256 = this.MachineHash,
            CurrentInventorySha256 = this.CurrentInventoryHash, CurrentVisibleMachineRawSha256 = this.CurrentMachineHash,
            this.InventoryReferencesUnchanged,
            ObservationOnly = this.Settings.NetworkRole == "Host", Protocol = snapshot,
            Evidence = "Local inventory and visible raw fingerprints only; requires matching separate host/client reports and host quota trace. No writes, save, or authentication proof."
        });
    }

    private static string HashInventory() => Hash(Game1.player.Items.Select(item => item is null ? null : new
    {
        item.QualifiedItemId, item.Stack, item.Quality,
        Meta = item.modData.Pairs.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray()
    }).ToArray());

    private static string HashMachines() => Hash(Game1.locations.OrderBy(l => l.NameOrUniqueName, StringComparer.Ordinal)
        .SelectMany(l => l.objects.Pairs.Where(p => MachineService.TryGetKind(p.Value, out _)).OrderBy(p => p.Key.X).ThenBy(p => p.Key.Y)
            .Select(p => new { Location = l.NameOrUniqueName, TileX = p.Key.X, TileY = p.Key.Y, p.Value.QualifiedItemId,
                HasRaw = p.Value.modData.ContainsKey(MachineService.StateKey), Raw = p.Value.modData.GetValueOrDefault(MachineService.StateKey) })).ToArray());
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record ProbeCase(ProtocolProbeKind Kind, Guid RequestId, ProtocolRejection Expected, long SentAfterSequence);
    /// <summary>单端首个真实Ready的所有权/连接基线。两端本地generation/线程/连接GUID不要求彼此相等。</summary>
    private sealed record EpisodeBaseline(Guid RunId, Guid SessionId, long WorldGeneration, long DisconnectGeneration,
        long LocalPlayerId, int OwnerThreadId, long QueueRejected, long Evicted, long LastSequence,
        IReadOnlyList<ProtocolPeerStatus> Peers);
}
