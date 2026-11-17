using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;

namespace GameHarness;

/// <summary>
/// 用四个原版进程验证同一农工的库存与提交记录跨断线、主机写盘和冷启动的行为。
/// 只允许对明确复制的合作档保存。写入轮不发送成功确认，断线由本地启动器在观察到同步后触发。
/// </summary>
internal sealed class NativeInventoryRecoveryScenario
{
    private const string HarnessId = "Sora6A.XiangyeShiling.GameHarness";
    private const string MessageType = "NativeInventoryRecoveryV1";
    internal const string JournalKey = HarnessId + "/RecoveryCommitJournal";
    internal const string MarkerKey = HarnessId + "/RecoveryItem";
    private const string ControlFile = "native-inventory-recovery-disconnect.txt";
    private const string ReportFile = "native-inventory-recovery-result.json";
    private const string MarkerItem = "(O)388";
    private const int SlotIndex = 11;
    private const string WheatItem = "(O)262";
    private const int WheatSlot = 10;
    private const int BeforeStack = 3;
    private const int AfterStack = 2;

    private enum State { Connecting, Prepared, CommitRequested, Committed, Disconnecting, Saving, ReplayObserved, Completed, Failed }
    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly NativeNetworkBootstrap Bootstrap;
    private readonly bool Host;
    private readonly bool Write;
    private readonly bool Bedtime;
    private readonly string FarmName;
    private readonly ulong FixtureId;
    private readonly ReadSlot? ReadFingerprint;
    private readonly Stopwatch Clock = new();
    private readonly Guid RunId = Guid.NewGuid();
    private readonly ConcurrentQueue<Signal> Inbox = new();
    private readonly List<Observation> Stages = new();
    private State Stage;
    private Guid PeerRunId;
    private Guid RequestId;
    private IMultiplayerPeer? Peer;
    private string? HostInventory;
    private string? OtherClientInventory;
    private bool NativePeerDisconnected;
    private bool ColdHostRead;
    private bool NativeSaveStarted;
    private bool NativeSaveCompleted;
    private bool SavingObserved;
    private bool SavedObserved;
    private int SavingEvents;
    private int SavedEvents;
    private int? StartTotalDays;
    private int? SavedTotalDays;
    private NativeBedtimeInteraction? BedtimeInteraction;
    private string? WheatDigest;
    private int OwnerWrites;
    private int OwnerReplays;
    private long LastHello = -1000;
    private IEnumerator<int>? Save;
    private DialogueBox? SaveMenu;

    internal bool Completed => this.Stage == State.Completed;
    internal string? Failure { get; private set; }

    internal NativeInventoryRecoveryScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && config.EnableNativeInventoryOwnership && config.Scenario == "NativeInventoryRecovery"
            && config.NativeRecoveryPhase is "Write" or "Verify" && !config.OpenPreview
            && NativeNetworkBootstrap.IsRecoveryFixture(config),
            "Native recovery requires its exact disposable world and an explicit Write or Verify phase.");
        this.Helper = helper;
        this.Monitor = monitor;
        this.Host = config.NetworkRole == "Host";
        this.Write = config.NativeRecoveryPhase == "Write";
        this.FarmName = config.FarmName;
        this.FixtureId = config.SaveFixtureId;
        this.Bedtime = this.FixtureId == NativeNetworkBootstrap.BedtimeFixtureId;
        if (this.Bedtime)
        {
            Type type = typeof(MachineService).Assembly.GetType(
                "XiangyeShiling.GameIntegration.OwnerInventoryFingerprint", throwOnError: true)!;
            this.ReadFingerprint = (type.GetMethod("TryReadSlot", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(type.FullName, "TryReadSlot")).CreateDelegate<ReadSlot>();
        }
        this.Bootstrap = new NativeNetworkBootstrap(monitor, config);
    }

    internal void QueueStart()
    {
        this.Clock.Start();
        this.Bootstrap.QueueStart();
    }

    internal void Tick()
    {
        if (!this.Clock.IsRunning || this.Completed || this.Failure is not null) return;
        try
        {
            Require(this.Clock.Elapsed < TimeSpan.FromMinutes(3), "Timed out waiting for native recovery evidence.");
            this.Bootstrap.Tick();
            if (this.Bootstrap.Failure is { } bootstrapFailure) throw new InvalidOperationException(bootstrapFailure);
            if (!this.Bootstrap.Ready || this.Stage == State.Disconnecting) return;
            if (this.Stage == State.Saving)
            {
                if (this.Bedtime) this.AdvanceBedtime();
                else
                {
                    this.VerifyWorld();
                    this.AdvanceSave();
                }
                return;
            }
            this.VerifyWorld();

            var peers = this.Helper.Multiplayer.GetConnectedPlayers().ToArray();
            if (this.Host && !this.Write && !this.ColdHostRead)
            {
                Require(peers.Length == 0, "The cold host must read persisted values before the client rejoins.");
                var offline = Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: false);
                Require(offline is not null && !Game1.getOnlineFarmers().Any(f => f.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId),
                    "The offline farmer is absent from the cold-loaded world.");
                this.ReadColdCommit(offline!);
                this.HostInventory = InventoryHash(Game1.player, -1);
                this.OtherClientInventory = this.ClientInventoryHash(offline!);
                this.VerifyWheat(offline!);
                this.ColdHostRead = true;
                this.Record("ColdHostReadBeforeRejoin", offline);
            }

            if (this.Host && this.Write && this.Stage == State.Committed && this.NativePeerDisconnected)
            {
                if (peers.Length == 0 && !Game1.getOnlineFarmers().Any(f => f.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId))
                    this.BeginSave();
                return;
            }

            if (peers.Length == 0)
            {
                Require(this.Peer is null || (this.Host && this.Write && this.Stage == State.Committed),
                    "The bound peer left before the expected disconnect checkpoint.");
                return;
            }

            var actors = this.BindActors(peers);
            if (actors is null) return;
            var (host, client) = actors.Value;
            this.HostInventory ??= InventoryHash(host, -1);
            this.OtherClientInventory ??= this.ClientInventoryHash(client);
            Require(this.HostInventory == InventoryHash(host, -1) && this.OtherClientInventory == this.ClientInventoryHash(client),
                "Inventory outside the test slots changed.");

            if (!this.Host && this.PeerRunId == Guid.Empty && this.Clock.ElapsedMilliseconds - this.LastHello >= 500)
            {
                this.Send("Hello");
                this.LastHello = this.Clock.ElapsedMilliseconds;
            }
            while (this.Inbox.TryDequeue(out var signal)) this.Handle(signal, client);

            if (this.Host && this.Write && this.Stage == State.Prepared && this.Matches(client, BeforeStack)
                && !client.modData.ContainsKey(JournalKey) && (!this.Bedtime || this.HasWheat(client)))
            {
                this.VerifyWheat(client);
                this.Stage = State.CommitRequested;
                this.Record("HostObservedPreparedSlot", client);
                this.Send("Commit");
            }
            if (this.Host && this.Write && this.Stage == State.CommitRequested && this.MatchesCommit(client))
            {
                this.VerifyWheat(client);
                this.Stage = State.Committed;
                this.Record("HostObservedCommitBeforeConfirmation", client);
            }
            if (!this.Host && this.Write && this.Stage == State.Committed)
                this.DisconnectIfRequested(client);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Fail(error.Message);
        }
    }

    internal void OnMessage(ModMessageReceivedEventArgs args)
    {
        if (this.Peer is not { } peer || this.Completed || this.Failure is not null || this.Inbox.Count >= 12
            || args.FromModID != HarnessId || args.Type != MessageType || args.FromPlayerID != peer.PlayerID) return;
        try
        {
            if (args.ReadAs<Signal>() is { RunId: var id } signal && id != Guid.Empty && signal.Write == this.Write)
                this.Inbox.Enqueue(signal);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { }
    }

    internal void OnPeerDisconnected(long playerId)
    {
        if (this.Host && this.Write && this.Peer?.PlayerID == playerId)
            this.NativePeerDisconnected = true;
    }

    internal void OnSaving()
    {
        if (this.Completed || this.Failure is not null) return;
        this.SavingEvents++;
        this.SavingObserved = true;
        if (!this.Host || !this.Write || !this.NativeSaveStarted || this.Stage != State.Saving)
            this.Fail("Saving was observed outside the one authorized host write.");
        else if (this.Bedtime && (this.BedtimeInteraction?.AnswerSent != true || this.SavingEvents != 1))
            this.Fail("The bedtime save did not follow the one authorized native Yes response.");
        else if (this.Bedtime)
            this.Record("HostObservedSmapiSaving", null);
    }

    internal void OnSaved()
    {
        if (this.Completed || this.Failure is not null) return;
        this.SavedEvents++;
        if (this.Host && this.Write && this.NativeSaveStarted)
            this.SavedObserved = true;
        if (!this.Bedtime) return;
        if (!this.SavingObserved || this.SavingEvents != 1 || this.SavedEvents != 1 || !this.SavedObserved || this.Stage != State.Saving)
            this.Fail("Saved was observed outside the one authorized bedtime save.");
        else
            this.Record("HostObservedSmapiSaved", null);
    }

    internal void OnReturnedToTitle()
    {
        if (this.Completed || this.Failure is not null) return;
        if (!this.Host && this.Write && this.Stage == State.Disconnecting)
            this.Complete("ClientReturnedToTitleWithoutConfirmation", null);
        else if (this.Bootstrap.Ready)
            this.Fail("The bound world returned to title before its recovery checkpoint.");
    }

    private (Farmer Host, Farmer Client)? BindActors(IMultiplayerPeer[] peers)
    {
        long remoteId = this.Host ? NativeNetworkBootstrap.ClientId : NativeNetworkBootstrap.HostId;
        Require(peers.Length == 1 && peers[0].PlayerID == remoteId && peers[0].IsHost != this.Host
            && peers[0].HasSmapi && !peers[0].IsSplitScreen && peers[0].GetMod(HarnessId) is not null,
            "The connected peer is not the expected recovery endpoint.");
        var remote = Game1.getOnlineFarmers().SingleOrDefault(f => f.UniqueMultiplayerID == remoteId);
        if (remote is null && this.Peer is null) return null;
        this.Peer ??= peers[0];
        Require(ReferenceEquals(this.Peer, peers[0]), "The bound transport peer changed.");
        Require(remote is not null && !ReferenceEquals(Game1.player, remote), "The connected native farmer is absent or aliases the local farmer.");
        return this.Host ? (Game1.player, remote!) : (remote!, Game1.player);
    }

    private void Handle(Signal signal, Farmer client)
    {
        if (this.Host && signal.Kind == "Hello" && this.PeerRunId == Guid.Empty && signal.ReplyTo == Guid.Empty)
        {
            this.PeerRunId = signal.RunId;
            if (this.Write)
            {
                this.RequireFresh(client);
                this.RequestId = Guid.NewGuid();
                this.Stage = State.Prepared;
                this.Send("Prepare");
            }
            else
            {
                Require(this.ColdHostRead && this.MatchesCommit(client), "Rejoined farmer differs from the cold host journal and slot.");
                this.VerifyWheat(client);
                this.Stage = State.CommitRequested;
                this.Record("ColdHostObservedActualRejoin", client);
                this.Send("Commit");
            }
            return;
        }

        if (signal.ReplyTo != this.RunId || signal.RequestId == Guid.Empty) return;
        if (!this.Host && this.PeerRunId == Guid.Empty && signal.Kind == (this.Write ? "Prepare" : "Commit"))
        {
            this.PeerRunId = signal.RunId;
            this.RequestId = signal.RequestId;
        }
        if (signal.RunId != this.PeerRunId || signal.RequestId != this.RequestId) return;

        if (!this.Host && this.Write && signal.Kind == "Prepare" && this.Stage == State.Connecting)
        {
            this.RequireFresh(client);
            this.WriteMarker(client, BeforeStack);
            if (this.Bedtime)
            {
                client.Items[WheatSlot] = ItemRegistry.Create(WheatItem, 1);
                this.VerifyWheat(client);
            }
            this.Stage = State.Prepared;
            this.Record("ClientPreparedOwnSlot", client);
        }
        else if (!this.Host && signal.Kind == "Commit" && this.Stage == (this.Write ? State.Prepared : State.Connecting))
        {
            this.ApplyOwnerCommit(client);
            this.Stage = this.Write ? State.Committed : State.ReplayObserved;
            this.Record(this.Write ? "ClientCommittedBeforeConfirmation" : "ColdClientReplayedWithoutSecondDebit", client);
            if (!this.Write) this.Send("ReplayObserved");
        }
        else if (this.Host && !this.Write && signal.Kind == "ReplayObserved" && this.Stage == State.CommitRequested)
        {
            Require(this.MatchesCommit(client), "The repeated commit changed the remote inventory or journal.");
            this.VerifyWheat(client);
            this.Send("VerifyComplete");
            this.Complete("ColdHostObservedNoSecondDebit", client);
        }
        else if (!this.Host && !this.Write && signal.Kind == "VerifyComplete" && this.Stage == State.ReplayObserved)
        {
            Require(this.MatchesCommit(client) && this.OwnerWrites == 0 && this.OwnerReplays == 1, "Cold replay performed an additional write.");
            this.VerifyWheat(client);
            this.Complete("ColdClientConfirmedSameInventoryAndJournal", client);
        }
    }

    private void ApplyOwnerCommit(Farmer client)
    {
        Require(!this.Host && ReferenceEquals(client, Game1.player) && client.CurrentToolIndex != SlotIndex,
            "Only the local farmer can apply its test inventory operation.");
        this.VerifyWheat(client);
        if (client.modData.TryGetValue(JournalKey, out string? journal))
        {
            Require(journal == this.Journal && this.Matches(client, AfterStack), "The persisted commit and inventory disagree.");
            this.OwnerReplays++;
            return;
        }

        Require(this.Write && this.Matches(client, BeforeStack), "The fresh owner commit is missing its prepared input.");
        this.WriteMarker(client, AfterStack);
        client.modData[JournalKey] = this.Journal;
        Require(this.MatchesCommit(client), "The client did not retain its own slot and commit journal.");
        this.OwnerWrites++;
    }

    private void DisconnectIfRequested(Farmer client)
    {
        string path = Path.Combine(this.Helper.DirectoryPath, ControlFile);
        if (!File.Exists(path)) return;
        Require(File.ReadAllText(path).Trim() == this.RequestId.ToString("D") && this.MatchesCommit(client),
            "The local disconnect request or committed values differ.");
        this.Stage = State.Disconnecting;
        this.Record("ClientRequestedNativeDisconnectBeforeConfirmation", client);
        Game1.ExitToTitle();
    }

    private void BeginSave()
    {
        this.VerifyWorld();
        string fixture = $"{this.FarmName}_{this.FixtureId}";
        Require(this.Host && this.Write && !this.NativeSaveStarted && Constants.SaveFolderName == fixture
            && Game1.player.Name == this.FarmName, "Refusing to save any other world or save twice.");
        var offline = Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: false);
        if (this.Bedtime)
        {
            Require(offline is not null && this.MatchesCommit(offline), "The disconnected farmhand no longer has its committed item and journal.");
            this.VerifyWheat(offline!);
        }
        this.Record("HostObservedNativeDisconnect", offline);
        this.Stage = State.Saving;
        this.NativeSaveStarted = true;
        if (this.Bedtime)
        {
            this.BeginBedtime();
            return;
        }
        this.SaveMenu = new DialogueBox("专用合作档保存验证中。");
        Game1.activeClickableMenu = this.SaveMenu;
        this.Save = SaveGame.Save();
        this.Record("HostStartedSingleNativeSave", offline);
    }

    private void AdvanceSave()
    {
        if (this.Save!.MoveNext()) return;
        this.Save.Dispose();
        this.Save = null;
        if (ReferenceEquals(Game1.activeClickableMenu, this.SaveMenu)) Game1.exitActiveMenu();
        this.NativeSaveCompleted = true;
        this.Complete("HostFinishedSingleNativeSave", Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: false));
    }

    private void BeginBedtime()
    {
        this.BedtimeInteraction = new NativeBedtimeInteraction();
        this.StartTotalDays = this.BedtimeInteraction.StartTotalDays;
        this.Record("HostApproachedNativeBed", null);
    }

    private void AdvanceBedtime()
    {
        if (!Context.IsWorldReady) return;
        this.VerifyWorld();
        if (this.SavedObserved)
        {
            Require(Game1.Date.TotalDays == this.StartTotalDays + 1, "Native bedtime did not advance exactly one day.");
            this.SavedTotalDays = Game1.Date.TotalDays;
            var offline = Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: false);
            Require(offline is not null && this.MatchesCommit(offline), "Native bedtime changed the offline farmhand commit.");
            this.VerifyWheat(offline!);
            this.NativeSaveCompleted = true;
            this.Complete("HostFinishedNativeBedtime", offline);
            return;
        }
        this.BedtimeInteraction!.Tick(name => this.Record(name, null));
    }

    private void VerifyWorld()
    {
        Require(Context.IsWorldReady && Context.IsMultiplayer && !Context.IsSplitScreen && Context.ScreenId == 0
            && Context.IsMainPlayer == this.Host && Game1.uniqueIDForThisGame == this.FixtureId
            && Game1.player.farmName.Value == this.FarmName
            && Game1.player.UniqueMultiplayerID == (this.Host ? NativeNetworkBootstrap.HostId : NativeNetworkBootstrap.ClientId),
            "The active world or farmer is outside the recovery fixture.");
    }

    private void ReadColdCommit(Farmer farmer)
    {
        string? raw = farmer.modData.GetValueOrDefault(JournalKey);
        Require(Guid.TryParseExact(raw?.Split(':').Last(), "N", out this.RequestId) && this.RequestId != Guid.Empty
            && this.MatchesCommit(farmer), "The cold host did not load the exact committed slot and journal.");
    }

    private string Journal => $"1:Committed:{SlotIndex}:{BeforeStack}:{AfterStack}:{this.RequestId:N}";

    private void RequireFresh(Farmer farmer) => Require(farmer.Items.Count > SlotIndex && farmer.Items[SlotIndex] is null
        && farmer.CurrentToolIndex != SlotIndex && !farmer.modData.ContainsKey(JournalKey)
        && (!this.Bedtime || (farmer.Items[WheatSlot] is null && farmer.CurrentToolIndex != WheatSlot)),
        "Expected fresh empty test slots and no commit journal.");

    private bool MatchesCommit(Farmer farmer) => this.Matches(farmer, AfterStack) && farmer.modData.GetValueOrDefault(JournalKey) == this.Journal;

    private bool Matches(Farmer farmer, int stack) => farmer.Items.Count > SlotIndex && farmer.Items[SlotIndex] is { } item
        && item.QualifiedItemId == MarkerItem && item.Stack == stack && item.Quality == 0 && item.modData.Count() == 1
        && item.modData.GetValueOrDefault(MarkerKey) == this.RequestId.ToString("N");

    private void WriteMarker(Farmer farmer, int stack)
    {
        Require(ReferenceEquals(Game1.player, farmer) && farmer.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId,
            "Refuse to write anything except the local test farmer's slot.");
        Item item = ItemRegistry.Create(MarkerItem, stack);
        item.modData[MarkerKey] = this.RequestId.ToString("N");
        farmer.Items[SlotIndex] = item;
        Require(ReferenceEquals(farmer.Items[SlotIndex], item) && this.Matches(farmer, stack), "The native inventory rejected the test item.");
    }

    private bool HasWheat(Farmer farmer) => farmer.Items.Count > WheatSlot && farmer.Items[WheatSlot] is { } item
        && item.QualifiedItemId == WheatItem && item.Stack == 1 && item.Quality == 0 && !item.modData.Pairs.Any();

    private void VerifyWheat(Farmer farmer)
    {
        if (this.ReadFingerprint is null) return;
        Require(this.HasWheat(farmer), "The test farmhand has no unmarked native wheat in its witness slot.");
        if (!this.ReadFingerprint(farmer, WheatSlot, out string? digest) || digest is not { Length: 64 })
            throw new InvalidOperationException("The production fingerprint rejected the native wheat witness.");
        this.WheatDigest ??= digest;
        Require(this.WheatDigest == digest, "The production fingerprint changed for the native wheat witness.");
    }

    private string ClientInventoryHash(Farmer farmer) => InventoryHash(farmer, SlotIndex, this.Bedtime ? WheatSlot : -1);

    private static string InventoryHash(Farmer farmer, int excludedSlot, int secondExcludedSlot = -1)
    {
        var snapshot = new
        {
            farmer.MaxItems,
            Slots = farmer.Items.Select((item, slot) => slot == excludedSlot || slot == secondExcludedSlot || item is null ? null : new
            {
                item.QualifiedItemId, item.Stack, item.Quality,
                Metadata = item.modData.Pairs.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
            }).ToArray()
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot)));
    }

    private void Send(string kind) => this.Helper.Multiplayer.SendMessage(new Signal(kind, this.RunId, this.PeerRunId, this.RequestId, this.Write),
        MessageType, modIDs: new[] { HarnessId }, playerIDs: new[] { this.Peer!.PlayerID });

    private void Record(string name, Farmer? farmer)
    {
        Item? item = farmer is not null && farmer.Items.Count > SlotIndex ? farmer.Items[SlotIndex] : null;
        this.Stages.Add(new Observation(name, this.Clock.ElapsedMilliseconds, item?.QualifiedItemId, item?.Stack,
            item?.Quality, item?.modData.GetValueOrDefault(MarkerKey), farmer?.modData.GetValueOrDefault(JournalKey), this.WheatDigest));
        this.Monitor.Log("NativeInventoryRecovery " + name, LogLevel.Info);
        this.Report();
    }

    private void Complete(string name, Farmer? farmer)
    {
        this.Stage = State.Completed;
        this.Bootstrap.RestoreOptions();
        this.Record(name, farmer);
    }

    private void Fail(string reason)
    {
        this.Failure = reason;
        this.Stage = State.Failed;
        this.Save?.Dispose();
        this.Save = null;
        this.Bootstrap.RestoreOptions();
        this.Monitor.Log(reason, LogLevel.Warn);
        this.Report();
    }

    private void Report() => this.Helper.Data.WriteJsonFile(ReportFile, new
    {
        SchemaVersion = 1, this.RunId, this.PeerRunId, this.RequestId, ProcessId = Environment.ProcessId,
        this.FarmName, this.FixtureId, SaveStrategy = this.Bedtime ? "Bedtime" : "Direct",
        NetworkRole = this.Host ? "Host" : "Client", Phase = this.Write ? "Write" : "Verify", SlotIndex,
        this.Completed, Status = this.Stage.ToString(), this.Failure, this.NativePeerDisconnected,
        this.NativeSaveStarted, this.NativeSaveCompleted, this.SavingObserved, this.SavedObserved,
        this.SavingEvents, this.SavedEvents, this.StartTotalDays, this.SavedTotalDays, this.WheatDigest,
        this.OwnerWrites, this.OwnerReplays, this.HostInventory, this.OtherClientInventory, this.Stages
    });

    public sealed record Signal(string Kind, Guid RunId, Guid ReplyTo, Guid RequestId, bool Write);
    private delegate bool ReadSlot(Farmer? owner, int slot, out string? fingerprint);
    private sealed record Observation(string Name, long ElapsedMilliseconds, string? QualifiedItemId, int? Stack, int? Quality,
        string? Token, string? Journal, string? WheatDigest);

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
