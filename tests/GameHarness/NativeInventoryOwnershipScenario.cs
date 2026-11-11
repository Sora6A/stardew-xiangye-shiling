using System.Collections.Concurrent;
using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Inventories;
using XiangyeShiling.GameIntegration;

namespace GameHarness;

/// <summary>
/// 在专用双端进程中观察原生背包同步。双方明确启用并经定向握手同意后，
/// 仅使用农工的一格空槽。报告记录真实 Item 对象上的读写，不调用生产加工或保存。
/// </summary>
internal sealed class NativeInventoryOwnershipScenario
{
    private const string HarnessId = "Sora6A.XiangyeShiling.GameHarness";
    private const string MessageType = "NativeInventoryOwnershipV1";
    private const string MarkerKey = HarnessId + "/InventoryOwnership";
    private const string MarkerItem = "(O)388";
    private const string HostName = "XYSNETHost";
    private const int SlotIndex = 11;
    private const int ClientStack = 3;
    private const int HostStack = 2;
    private const int InboxCapacity = 12;

    private enum Phase
    {
        Connecting, WaitingConsent, WaitingPermit, WaitingClientItem, WaitingHostItem,
        WaitingClientClear, WaitingHostConfirmation, Completed, Failed
    }

    private readonly IModHelper Helper;
    private readonly IMonitor Monitor;
    private readonly bool Host;
    private readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly Guid RunId = Guid.NewGuid();
    private readonly ConcurrentQueue<Signal> Inbox = new();
    private readonly List<Observation> Stages = new();
    private IMultiplayerPeer? Peer;
    private InventoryBaseline? HostInventory;
    private InventoryBaseline? ClientInventory;
    private MachineService? Production;
    private Guid PeerRunId;
    private Phase Status;
    private long LastConsent = -1000;
    private bool ClientConfirmedReplacement;
    private bool SavingObserved;

    internal bool Completed => this.Status == Phase.Completed;
    internal string? Failure { get; private set; }

    internal NativeInventoryOwnershipScenario(IModHelper helper, IMonitor monitor, Config config)
    {
        Require(config.Enabled && config.EnableNativeInventoryOwnership && config.Scenario == "NativeInventoryOwnership"
            && !config.OpenPreview && config.FarmName == NativeNetworkBootstrap.FarmName
            && config.SaveFixtureId == NativeNetworkBootstrap.FixtureId && config.NetworkRole is "Host" or "Client",
            "Inventory ownership requires explicit opt-in and the dedicated Host or Client role.");
        this.Helper = helper;
        this.Monitor = monitor;
        this.Host = config.NetworkRole == "Host";
    }

    /// <summary>由主线程调用。connected 仅代表引导完成，仍逐帧验证实际连接及角色。</summary>
    internal void Tick(bool connected)
    {
        if (this.Completed || this.Failure is not null) return;
        try
        {
            Require(this.Clock.Elapsed < TimeSpan.FromMinutes(2), "Timed out waiting for native inventory evidence.");
            if (!connected)
            {
                Require(this.Peer is null, "Native connection became unavailable during the test.");
                return;
            }

            var actors = this.FindActors();
            if (actors is null) return;
            var (peer, host, client) = actors.Value;
            if (this.Peer is null) this.Bind(peer, host, client);
            Require(ReferenceEquals(this.Peer, peer), "The connected test peer changed.");
            this.HostInventory!.Verify(host, -1);
            this.ClientInventory!.Verify(client, SlotIndex);

            while (this.Inbox.TryDequeue(out var signal)) this.Handle(signal, client);
            this.ObserveInventory(client);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            this.Fail(error.Message);
        }
    }

    internal void OnMessageReceived(ModMessageReceivedEventArgs args)
    {
        if (this.Peer is not { } peer || this.Completed || this.Failure is not null
            || args.FromModID != HarnessId || args.Type != MessageType || args.FromPlayerID != peer.PlayerID
            || this.Inbox.Count >= InboxCapacity) return;
        try
        {
            if (args.ReadAs<Signal>() is { RunId: var run } signal && run != Guid.Empty)
                this.Inbox.Enqueue(signal);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // 无效消息不会授权写入。缺少有效同意的场景由主线程期限判为失败。
        }
    }

    internal void OnSaving()
    {
        this.SavingObserved = true;
        this.Fail("Saving was observed during the no-save inventory test.");
    }

    internal void OnBootstrapFailed(string reason) => this.Fail("Native connection bootstrap failed: " + reason);

    internal void OnReturnedToTitle()
    {
        if (!this.Completed && this.Peer is not null) this.Fail("The test world closed before both observations completed.");
    }

    private (IMultiplayerPeer Peer, Farmer Host, Farmer Client)? FindActors()
    {
        Require(Context.IsWorldReady && Context.IsMultiplayer && !Context.IsSplitScreen && Context.ScreenId == 0
            && Context.IsMainPlayer == this.Host && Game1.uniqueIDForThisGame == NativeNetworkBootstrap.FixtureId
            && Game1.player.farmName.Value == NativeNetworkBootstrap.FarmName,
            "The native world or local role does not match the dedicated fixture.");
        long localId = this.Host ? NativeNetworkBootstrap.HostId : NativeNetworkBootstrap.ClientId;
        long expectedPeerId = this.Host ? NativeNetworkBootstrap.ClientId : NativeNetworkBootstrap.HostId;
        Require(Game1.player.UniqueMultiplayerID == localId, "The local farmer does not match the dedicated identity.");
        var peers = this.Helper.Multiplayer.GetConnectedPlayers().ToArray();
        if (peers.Length == 0 && this.Peer is null) return null;
        Require(peers.Length == 1 && peers[0].PlayerID == expectedPeerId && peers[0].IsHost != this.Host
            && !peers[0].IsSplitScreen && peers[0].HasSmapi && peers[0].GetMod(HarnessId) is not null,
            "Expected exactly the dedicated remote test peer.");

        Farmer? remote = Game1.getOnlineFarmers().SingleOrDefault(f => f.UniqueMultiplayerID == expectedPeerId);
        if (remote is null && this.Peer is null) return null;
        Require(remote is not null && !ReferenceEquals(remote, Game1.player), "The live remote farmer is absent or aliases the local farmer.");
        Farmer host = this.Host ? Game1.player : remote!;
        Farmer client = this.Host ? remote! : Game1.player;
        Require(host.Name == HostName && client.Name == NativeNetworkBootstrap.ClientName,
            "The host or farmhand name does not match the dedicated identities.");
        return (peers[0], host, client);
    }

    private void Bind(IMultiplayerPeer peer, Farmer host, Farmer client)
    {
        this.Production = this.Helper.ModRegistry.GetApi(MachineService.Prefix) as MachineService
            ?? throw new InvalidOperationException("The production module is unavailable.");
        Require(!this.Production.ReadNetworkDiagnostics().WritesEnabled, "The production multiplayer write gate is open.");
        this.HostInventory = new InventoryBaseline(host);
        this.ClientInventory = new InventoryBaseline(client);
        Require(!ReferenceEquals(host.Items, client.Items) && client.Items.Count > SlotIndex
            && client.MaxItems > SlotIndex && client.Items[SlotIndex] is null && client.CurrentToolIndex != SlotIndex,
            "The dedicated client must have an unused empty test slot inside its current capacity.");
        this.Peer = peer;
        this.Status = this.Host ? Phase.WaitingConsent : Phase.WaitingPermit;
        this.Record("BoundNativeActors", client.Items[SlotIndex]);
    }

    private void Handle(Signal signal, Farmer client)
    {
        if (this.Host && signal.Kind == "Consent" && signal.ReplyTo == Guid.Empty)
        {
            if (this.Status == Phase.WaitingConsent)
            {
                Require(client.Items[SlotIndex] is null, "The client test slot changed before consent.");
                this.PeerRunId = signal.RunId;
                this.Status = Phase.WaitingClientItem;
                this.Record("ReceivedClientConsent", null);
            }
            if (this.Status == Phase.WaitingClientItem && signal.RunId == this.PeerRunId)
                this.Send("Permit");
            return;
        }

        if (signal.ReplyTo != this.RunId) return;
        if (!this.Host && signal.Kind == "Permit" && this.Status == Phase.WaitingPermit)
        {
            this.PeerRunId = signal.RunId;
            Require(ReferenceEquals(client, Game1.player) && client.Items[SlotIndex] is null
                && client.CurrentToolIndex != SlotIndex, "The client no longer owns an empty unused test slot.");
            this.Record("ReceivedHostConsent", null);
            this.WriteMarker(client, ClientStack);
            this.Status = Phase.WaitingHostItem;
            this.Record("ClientWroteOwnSlot", client.Items[SlotIndex]);
            return;
        }

        if (signal.RunId != this.PeerRunId) return;
        if (this.Host && signal.Kind == "Observed" && this.Status == Phase.WaitingClientClear)
            this.ClientConfirmedReplacement = true;
        else if (!this.Host && signal.Kind == "Finished" && this.Status == Phase.WaitingHostConfirmation)
        {
            Require(client.Items[SlotIndex] is null, "The client test slot did not stay empty.");
            this.Status = Phase.Completed;
            this.Record("ReceivedHostClearConfirmation", null);
        }
    }

    private void ObserveInventory(Farmer client)
    {
        Item? current = client.Items[SlotIndex];
        switch (this.Status)
        {
            case Phase.WaitingPermit when this.Clock.ElapsedMilliseconds - this.LastConsent >= 1000:
                this.LastConsent = this.Clock.ElapsedMilliseconds;
                this.Send("Consent");
                break;
            case Phase.WaitingClientItem when current is not null:
                Require(this.IsMarker(current, ClientStack), "Host observed a different item in the client test slot.");
                Require(!ReferenceEquals(client, Game1.player) && client.UniqueMultiplayerID == NativeNetworkBootstrap.ClientId
                    && client.CurrentToolIndex != SlotIndex && this.Production is not null
                    && !this.Production.ReadNetworkDiagnostics().WritesEnabled,
                    "The host target is no longer the dedicated remote farmer with production writes disabled.");
                this.Record("HostObservedClientNativeItem", current);
                this.WriteMarker(client, HostStack);
                Require(!ReferenceEquals(current, client.Items[SlotIndex]), "The host did not replace the remote slot reference.");
                this.Status = Phase.WaitingClientClear;
                this.Record("HostReplacedRemoteSlot", client.Items[SlotIndex]);
                break;
            case Phase.WaitingHostItem:
                if (this.IsMarker(current, ClientStack)) break;
                Require(this.IsMarker(current, HostStack) && ReferenceEquals(client, Game1.player)
                    && client.CurrentToolIndex != SlotIndex, "The client did not observe the expected native host replacement.");
                this.Record("ClientObservedHostNativeReplacement", current);
                client.Items[SlotIndex] = null;
                Require(client.Items[SlotIndex] is null, "The client failed to restore the empty test slot.");
                this.Status = Phase.WaitingHostConfirmation;
                this.Record("ClientRestoredOwnSlot", null);
                this.Send("Observed");
                break;
            case Phase.WaitingClientClear:
                Require(current is null || this.IsMarker(current, HostStack), "The remote test slot contains an unexpected item after the host write.");
                if (current is not null || !this.ClientConfirmedReplacement) break;
                this.Send("Finished");
                this.Status = Phase.Completed;
                this.Record("HostObservedClientNativeClear", null);
                break;
        }
    }

    private string MarkerToken => (this.Host ? this.PeerRunId : this.RunId).ToString("N");

    private bool IsMarker(Item? item, int stack) => item is not null && item.QualifiedItemId == MarkerItem
        && item.Stack == stack && item.Quality == 0 && item.modData.Count() == 1
        && item.modData.TryGetValue(MarkerKey, out string? token) && token == this.MarkerToken;

    private void WriteMarker(Farmer client, int stack)
    {
        Item marker = ItemRegistry.Create(MarkerItem, stack);
        marker.modData[MarkerKey] = this.MarkerToken;
        client.Items[SlotIndex] = marker;
        Require(ReferenceEquals(client.Items[SlotIndex], marker) && this.IsMarker(client.Items[SlotIndex], stack),
            "The native inventory setter did not retain the expected marker object.");
    }

    private void Send(string kind)
    {
        var peer = this.Peer ?? throw new InvalidOperationException("No bound test peer.");
        this.Helper.Multiplayer.SendMessage(new Signal(kind, this.RunId, this.PeerRunId), MessageType,
            modIDs: new[] { HarnessId }, playerIDs: new[] { peer.PlayerID });
    }

    private void Record(string stage, Item? item)
    {
        string? token = item?.modData.GetValueOrDefault(MarkerKey);
        this.Stages.Add(new Observation(stage, this.Clock.ElapsedMilliseconds, item?.QualifiedItemId, item?.Stack, item?.Quality, token));
        this.Monitor.Log($"NativeInventoryOwnership {stage}.", LogLevel.Info);
        this.Report();
    }

    private void Fail(string reason)
    {
        if (this.Failure is null)
        {
            this.Failure = reason;
            this.Status = Phase.Failed;
            this.Monitor.Log(reason, LogLevel.Warn);
        }
        this.Report();
    }

    private void Report() => this.Helper.Data.WriteJsonFile("native-inventory-ownership-result.json", new
    {
        SchemaVersion = 1, this.RunId, this.PeerRunId, ProcessId = Environment.ProcessId,
        FarmName = NativeNetworkBootstrap.FarmName, FixtureId = NativeNetworkBootstrap.FixtureId,
        NetworkRole = this.Host ? "Host" : "Client", HostPlayerId = this.HostInventory?.ActorId,
        ClientPlayerId = this.ClientInventory?.ActorId, SlotIndex, ClientCapacity = this.ClientInventory?.Capacity,
        ClientSlotCount = this.ClientInventory?.SlotCount, this.Completed, Status = this.Status.ToString(),
        this.Failure, this.SavingObserved, this.Stages
    });

    public sealed record Signal(string Kind, Guid RunId, Guid ReplyTo);
    private sealed record Observation(string Stage, long ElapsedMilliseconds, string? QualifiedItemId,
        int? Stack, int? Quality, string? Token);

    private sealed class InventoryBaseline
    {
        private readonly Farmer Actor;
        private readonly IInventory Items;
        private readonly SlotState[] Slots;
        internal long ActorId { get; }
        internal int Capacity { get; }
        internal int SlotCount => this.Slots.Length;

        internal InventoryBaseline(Farmer actor)
        {
            this.Actor = actor;
            this.ActorId = actor.UniqueMultiplayerID;
            this.Capacity = actor.MaxItems;
            this.Items = actor.Items;
            this.Slots = actor.Items.Select(item => new SlotState(item, item?.QualifiedItemId, item?.Stack,
                item?.Quality, ReadMetadata(item))).ToArray();
        }

        internal void Verify(Farmer current, int excludedSlot)
        {
            Require(ReferenceEquals(this.Actor, current) && this.ActorId == current.UniqueMultiplayerID
                && this.Capacity == current.MaxItems && ReferenceEquals(this.Items, current.Items)
                && this.SlotCount == current.Items.Count, "A farmer, capacity or inventory reference changed.");
            for (int i = 0; i < this.Slots.Length; i++)
            {
                if (i == excludedSlot) continue;
                Item? item = current.Items[i];
                var before = this.Slots[i];
                Require(ReferenceEquals(before.Reference, item) && before.Id == item?.QualifiedItemId
                    && before.Stack == item?.Stack && before.Quality == item?.Quality
                    && before.Metadata.SequenceEqual(ReadMetadata(item)), "An inventory slot outside the test scope changed.");
            }
        }

        private static KeyValuePair<string, string>[] ReadMetadata(Item? item) => item?.modData.Pairs
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray() ?? Array.Empty<KeyValuePair<string, string>>();

        private sealed record SlotState(Item? Reference, string? Id, int? Stack, int? Quality,
            KeyValuePair<string, string>[] Metadata);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
