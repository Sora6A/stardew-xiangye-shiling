using Microsoft.Xna.Framework;
using StardewValley;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Machines;
using XiangyeShiling.Menus;
using SObject = StardewValley.Object;

namespace GameHarness;

internal sealed partial class LanMachineTransactionScenario
{
    private const string LatePagePhase = "P3LatePage";
    private const string LatePageRecipe = Id + "_Recipe_DriedTofu";
    private const string LatePageOutput = "(O)" + Id + "_DriedTofu";
    private const int LatePageMinutes = 220;
    private readonly bool VerifyLatePage;
    private readonly List<LatePageMenuEvidence> LatePageSelections = new();
    private readonly List<LatePageSettlement> LatePageSettlements = new();
    private readonly List<LatePageClockEdge> LatePageClock = new();
    private string SelectedRecipe => this.VerifyLatePage ? LatePageRecipe : Recipe;
    private string SelectedOutput => this.VerifyLatePage ? LatePageOutput : Tofu;
    private int StartDebit => this.VerifyLatePage ? 4 : 3;

    private int NavigateLatePageMenu(MachineMenu menu, SObject machine, Farmer owner, MachineState state, string action)
    {
        int PageIndex() => this.Helper.Reflection.GetProperty<int>(menu, "PageIndex").GetValue();
        int Rows() => this.Helper.Reflection.GetProperty<int>(menu, "VisibleActionCount").GetValue();
        bool PageControls() => this.Helper.Reflection.GetProperty<bool>(menu, "ShowPageControls").GetValue();
        int pageCount = this.Helper.Reflection.GetProperty<int>(menu, "PageCount").GetValue();
        Require(PageIndex() == 0 && pageCount == 3, "The complete Bean Workshop catalog must open at page one of three.");

        bool starting = action == "Start";
        int row = starting ? 1 : 0;
        string buttonName = starting ? LatePageRecipe : "Collect";
        int pageTurns = 0;
        if (starting)
        {
            var before = Snapshot(owner);
            string raw = machine.modData[MachineService.StateKey];
            Point next = this.Helper.Reflection.GetProperty<Rectangle>(menu, "NextBounds").GetValue().Center;
            Require(state.Batch is null && PageControls() && Rows() == 2, "The idle Bean Workshop did not expose two recipe rows and its pager.");
            for (int expected = 1; expected < pageCount; expected++)
            {
                menu.receiveLeftClick(next.X, next.Y);
                Require(PageIndex() == expected && Rows() == 2, "The original pager did not advance to the next complete recipe page.");
                pageTurns++;
            }
            var session = this.Helper.Reflection.GetField<MenuActionSession>(menu, "Session").GetValue();
            Require(this.Helper.Reflection.GetField<Request?>(session, "Pending").GetValue() is null
                && this.Helper.Reflection.GetField<Request?>(menu, "FarmhandRequest").GetValue() is null
                && machine.modData[MachineService.StateKey] == raw && before.SequenceEqual(Snapshot(owner)),
                "Two original next-page clicks created an operation or changed the farmhand inventory or device.");
        }
        else
            Require(action == "Collect" && state.PhaseAt(MachineService.Now()) == MachinePhase.Ready
                && state.Batch?.RecipeId == LatePageRecipe && Rows() == 1 && !PageControls(),
                "The finished late-page batch did not reopen with a single original Collect row.");

        var bounds = this.Helper.Reflection.GetMethod(menu, "ActionBounds").Invoke<Rectangle>(row);
        var button = menu.allClickableComponents.SingleOrDefault(component => component.bounds == bounds);
        Require(button?.name == buttonName, $"The original {action} control in row {row + 1} was not {buttonName}.");
        this.LatePageSelections.Add(new(action, PageIndex(), pageCount, pageTurns, Rows(), row, buttonName, PageControls()));
        return row;
    }

    private bool ObserveLatePageHostClock(Batch batch)
    {
        long elapsed = MachineService.Now() - batch.StartedAt;
        Require(elapsed >= 0 && elapsed <= LatePageMinutes && elapsed % 10 == 0,
            $"The native host clock moved away from the late-page batch deadline at minute {elapsed}.");
        if (this.LatePageClock.Count == 0)
            Require(elapsed <= 10, "Start settlement was not observed before the first native clock edge.");
        else if (this.LatePageClock[^1].Elapsed == elapsed)
            return elapsed == LatePageMinutes;
        else
            Require(elapsed == this.LatePageClock[^1].Elapsed + 10, "The host skipped a ten-minute clock increment.");
        this.LatePageClock.Add(new(Game1.timeOfDay, elapsed, batch.StartedAt, batch.ReadyAt));
        return elapsed == LatePageMinutes;
    }

    private void RecordLatePageSettlement(Farmer host, Farmer owner, MachineState state, string? receipt)
    {
        if (state.Revision is not (1 or 2) || this.LatePageSettlements.Any(record => record.Revision == state.Revision)
            || !this.IsSettled(host, owner, state.LastRequestId)) return;
        bool hostInventoryUnchanged = this.HostInventory is not null && Snapshot(host).SequenceEqual(this.HostInventory);
        Slot?[]? expected = state.Revision == 1 ? this.Started : this.Collected;
        bool ownerInventoryExact = expected is not null && Snapshot(owner).SequenceEqual(expected);
        Require(hostInventoryUnchanged && ownerInventoryExact && state.Revision == this.LatePageSettlements.Count + 1
            && receipt is not null && state.LastRequestId is not null,
            "The shared late-page receipt did not match the original host and exact owner inventory and revision.");
        this.LatePageSettlements.Add(new(state.Revision, state.LastRequestId!.Value, receipt!, owner.Items.CountId(Soybean),
            owner.Items.CountId(LatePageOutput), hostInventoryUnchanged, ownerInventoryExact));
    }

    private sealed record LatePageMenuEvidence(string Action, int PageIndex, int PageCount, int PageTurns,
        int VisibleRows, int ActionRow, string ButtonName, bool PageControls);
    private sealed record LatePageSettlement(long Revision, Guid Request, string Receipt, int OwnerSoybeans,
        int OwnerDriedTofu, bool HostInventoryUnchanged, bool OwnerInventoryExact);
    private sealed record LatePageClockEdge(int Clock, long Elapsed, long StartedAt, long ReadyAt);
}
