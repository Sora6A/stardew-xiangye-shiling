using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

internal sealed partial class LanMachineTransactionScenario
{
    private const string HostFirstSignalType = "LanMachineHostFirstFixture";
    private readonly bool VerifyMenuEscape, VerifyHostFirst;
    private Guid? EscapeRequest, HostFirstLocalStart, HostFirstRefused;
    private string? EscapeJournal, EscapeReceipt, EscapeFeedback, EscapeCancelledFeedback, HostFirstFeedback;
    private bool HostFirstSignalled;
    private int HostFirstQueuedMessages;

    [EventPriority(EventPriority.High)]
    internal void OnMenuRaceTick(object? sender, UpdateTickedEventArgs e)
    {
        if (!this.Host || !this.Bootstrap.Ready || this.Completed || this.Failure is not null || this.Before is null
            || !Context.IsMainPlayer || !Context.IsWorldReady || Game1.uniqueIDForThisGame != this.FixtureId
            || this.PreparedMachine is not { } machine
            || Game1.GetPlayer(NativeNetworkBootstrap.ClientId, onlyOnline: true) is not { } owner) return;
        try
        {
            Farmer host = Game1.player;
            if (this.VerifyMenuEscape && this.EscapeJournal is null
                && host.modData.ContainsKey(ReservationKey)
                && owner.modData.GetValueOrDefault(JournalKey) is { } declined
                && declined.EndsWith(":Declined", StringComparison.Ordinal))
            {
                Require(Snapshot(owner).SequenceEqual(this.Before!), "Escape changed the farmhand inventory before cancellation.");
                this.EscapeJournal = declined;
                this.Report();
            }
            if (!this.VerifyHostFirst || !this.HostFirstSignalled || this.HostFirstLocalStart.HasValue) return;
            // Only act in the actual contention window with production work already queued on the host.
            this.HostFirstQueuedMessages = this.Service!.ReadNetworkDiagnostics().QueueCount;
            if (this.HostFirstQueuedMessages == 0) return;
            this.StartMachineBeforeFarmhandGrant(host, owner, machine);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { this.Finish(error.Message); }
    }

    private bool ReadHostFirstSignal(ModMessageReceivedEventArgs args)
    {
        if (args.Type != HostFirstSignalType) return false;
        if (this.VerifyHostFirst && this.Host && this.PreparedRevision == 0 && !this.Completed && this.Failure is null
            && args.FromModID == HarnessId && args.FromPlayerID == NativeNetworkBootstrap.ClientId
            && args.ReadAs<Guid>() == this.InstanceId)
            this.HostFirstSignalled = true;
        return true;
    }

    private bool AdvanceMenuEscape(Farmer host, Farmer owner, SObject machine, MachineState state, ReadySignal ready)
    {
        if (this.EscapeRequest is null)
        {
            Request cancelled = this.ClickFarmhandMenu(machine, owner, state, "Start");
            this.EscapeRequest = cancelled.Id;
            MachineMenu menu = this.ActiveFarmhandMenu!;
            menu.receiveKeyPress(Keys.Escape);
            this.EscapeFeedback = this.Helper.Reflection.GetField<string>(menu, "Feedback").GetValue();
            Require(ReferenceEquals(Game1.activeClickableMenu, menu) && !menu.readyToClose()
                && this.Helper.Reflection.GetField<Request?>(menu, "FarmhandRequest").GetValue() == cancelled
                && this.EscapeFeedback == this.Translate("machine.network-cancelling"),
                "Real Escape did not request cancellation on the pending native menu.");
            this.Report();
            return false;
        }

        if (!ready.AfterCancellation || !this.IsSettled(host, owner, this.EscapeRequest)) return false;
        var retained = this.ActiveFarmhandMenu!;
        Require(ReferenceEquals(Game1.activeClickableMenu, retained), "Escape closed the native pending menu.");
        if (this.Helper.Reflection.GetField<Request?>(retained, "FarmhandRequest").GetValue() is not null) return false;
        this.EscapeCancelledFeedback = this.Helper.Reflection.GetField<string>(retained, "Feedback").GetValue();
        Require(retained.readyToClose() && this.EscapeCancelledFeedback == this.Translate("machine.network-cancelled")
            && state.Revision == 0 && state.Batch is null && Snapshot(owner).SequenceEqual(this.Before!),
            "The real menu did not confirm cancellation with unchanged inventory and machine.");
        this.EscapeReceipt = host.modData[ReceiptKey];
        this.StartRequest = this.ClickCurrentFarmhandMenu(retained, machine, owner, state, "Start");
        Require(this.StartRequest.Id != this.EscapeRequest, "Retry reused the cancelled request.");
        return true;
    }

    private void StartMachineBeforeFarmhandGrant(Farmer host, Farmer owner, SObject machine)
    {
        Require(!host.modData.ContainsKey(ReservationKey) && !host.modData.ContainsKey(RefusalKey)
            && !owner.modData.ContainsKey(JournalKey) && Snapshot(owner).SequenceEqual(this.Before!)
            && MachineService.TryRead(machine, out var before) && before is { Revision: 0, Batch: null }
            && Game1.activeClickableMenu is null && host.Items.CountId(Soybean) == 3,
            "The planned host-first menu opportunity was unavailable.");
        if (!machine.checkForAction(host) || Game1.activeClickableMenu is not MachineMenu menu)
            throw new InvalidOperationException("The host's actual machine menu did not open.");
        try
        {
            Point action = this.Helper.Reflection.GetMethod(menu, "ActionBounds").Invoke<Rectangle>(0).Center;
            menu.receiveLeftClick(action.X, action.Y);
            this.HostFirstFeedback = this.Helper.Reflection.GetField<string>(menu, "Feedback").GetValue();
            Require(this.HostFirstFeedback == this.Translate("machine.started") + " " + this.Translate("machine.close-to-process"),
                "The real host-first menu did not confirm its local Start.");
        }
        finally { if (ReferenceEquals(Game1.activeClickableMenu, menu)) menu.exitThisMenu(); }
        Require(MachineService.TryRead(machine, out var after) && after is { Revision: 1, Batch: not null }
            && after.LastActorId == host.UniqueMultiplayerID && after.LastRequestId is not null,
            "The host's actual menu did not establish its own revision-one batch.");
        this.HostFirstLocalStart = after!.LastRequestId;
        this.Report();
    }

    private void AdvanceHostFirst(Farmer host, Farmer owner, SObject machine, MachineState state)
    {
        if (this.Host && this.PreparedRevision == -1)
        {
            this.SendReady(0);
            return;
        }
        if (!this.Host && this.StartRequest is null)
        {
            if (this.Ready?.InstanceId != this.InstanceId || !this.CanOpenNormalFarmhandMenu()) return;
            Require(state.Revision == 0 && Snapshot(owner).SequenceEqual(this.Before!),
                "The client no longer saw the starting revision before its real menu attempt.");
            // This fixture-only signal gives the host a same-tick action opportunity. No transaction uses this signal.
            this.Helper.Multiplayer.SendMessage(this.InstanceId, HostFirstSignalType, new[] { HarnessId },
                new[] { NativeNetworkBootstrap.HostId });
            this.StartRequest = this.ClickFarmhandMenu(machine, owner, state, "Start");
            this.Report();
            return;
        }
        if (state.Revision != 1 || !host.modData.TryGetValue(RefusalKey, out string raw)
            || this.ReadRefusedRequest(raw) is not Guid refused) return;
        Require(state.LastActorId == host.UniqueMultiplayerID && state.LastRequestId is Guid local && local != refused
            && state.Batch is { RecipeId: Recipe, Output: { ItemId: Tofu, Quantity: 2 } }
            && !host.modData.ContainsKey(ReservationKey) && !host.modData.ContainsKey(ReceiptKey) && !owner.modData.ContainsKey(JournalKey)
            && Snapshot(owner).SequenceEqual(this.Before!),
            "The refused request changed ownership, journal or the farmhand's inventory.");
        var hostAfter = this.HostInventory!.ToArray();
        hostAfter[InputSlot] = null;
        if (!Snapshot(host).SequenceEqual(hostAfter))
        {
            if (!this.Host) return; // Inventory and refusal use different native net fields.
            throw new InvalidOperationException("The host menu did not consume only its own planned three soybeans.");
        }
        if (this.Host)
        {
            Require(this.HostFirstLocalStart == state.LastRequestId && this.Lan?.LastConfirmed is { MessageType: "XYS/MachineIntent/3" } observed
                && observed.ActorId == owner.UniqueMultiplayerID && observed.ConnectionId == this.Capability?.Connection,
                "The host-first refusal did not follow the real native contender on its verified connection.");
            this.NativeMachineIntent = this.Lan!.LastConfirmed;
        }
        else
        {
            Require(this.StartRequest?.Id == refused && this.ActiveFarmhandMenu is not null
                && ReferenceEquals(Game1.activeClickableMenu, this.ActiveFarmhandMenu),
                "The refused native request was not the actual farmhand menu intent.");
            var menu = this.ActiveFarmhandMenu!;
            if (this.Helper.Reflection.GetField<Request?>(menu, "FarmhandRequest").GetValue() is not null) return;
            this.HostFirstFeedback = this.Helper.Reflection.GetField<string>(menu, "Feedback").GetValue();
            Require(this.HostFirstFeedback == this.Translate("machine.network-refused") && menu.readyToClose(),
                "The farmhand menu did not display its exact no-grant result.");
            this.HostFirstLocalStart = state.LastRequestId;
            menu.exitThisMenu();
        }
        this.HostFirstRefused = refused;
        this.Finish(null);
    }
}
