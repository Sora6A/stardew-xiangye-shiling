using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using XiangyeShiling.GameIntegration;
using XiangyeShiling.Menus;
using XiangyeShiling.Machines;
using SObject = StardewValley.Object;

namespace GameHarness;

internal static class MachineMenuNavigationScenarios
{
    internal static void Run(IModHelper helper, Farmer player, SObject machine)
    {
        const string soy = "(O)" + MachineService.Prefix + "_Soybean";
        string Raw() => machine.modData[MachineService.StateKey];
        string before = Raw();
        Item[] inventory = player.Items.ToArray();
        var previousMenu = Game1.activeClickableMenu;
        var viewport = Game1.uiViewport;
        int clock = Game1.timeOfDay;
        Point cursor = Game1.getMousePositionRaw();
        bool snappy = Game1.options.snappyMenus;
        bool gamepad = Game1.options.gamepadControls;
        InputButton[] actionButtons = Game1.options.actionButton;
        try
        {
            Game1.options.snappyMenus = false;
            Game1.options.gamepadControls = false;
            Game1.activeClickableMenu = null;
            machine.checkForAction(player);
            var menu = Game1.activeClickableMenu as MachineMenu
                ?? throw new InvalidOperationException("Native interaction did not create the production menu.");
            var session = helper.Reflection.GetField<MenuActionSession>(menu, "Session").GetValue();
            bool GuideOpen() => helper.Reflection.GetField<bool>(menu, "GuideOpen").GetValue();
            int GuidePage() => helper.Reflection.GetField<int>(menu, "GuidePage").GetValue();
            ClickableComponent Focus() => menu.currentlySnappedComponent
                ?? throw new InvalidOperationException("No current navigation focus.");
            void PadMove(Buttons button) => menu.receiveKeyPress(Utility.mapGamePadButtonToKey(button));
            void PadConfirm()
            {
                // Game1.updateActiveMenu maps A to the click at the snapped cursor, then sends its mapped key.
                // Match the native snap point without reading the desktop pointer from an inactive window.
                menu.receiveGamePadButton(Buttons.A);
                Check(!menu.areGamePadControlsImplemented(), "The regression must exercise the native cursor-click route.");
                ClickableComponent selected = Focus();
                Rectangle bounds = selected.bounds;
                Check(ReferenceEquals(menu.getComponentWithID(selected.myID), selected)
                    && new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height).Contains(bounds),
                    "Gamepad focus is not registered in the current viewport.");
                Point target = new(bounds.Right - bounds.Width / 4, bounds.Bottom - bounds.Height / 4);
                menu.receiveLeftClick(target.X, target.Y);
                PadMove(Buttons.A);
            }

            menu.receiveKeyPress(Keys.Enter);
            Check(GuideOpen() && GuidePage() == 0, "Keyboard confirmation did not open the guide.");
            menu.receiveKeyPress(Keys.Down);
            menu.receiveKeyPress(Keys.Enter);
            Check(GuidePage() == 1, "Keyboard navigation did not reach the next guide page.");
            int selected = Focus().myID;
            var smaller = viewport;
            smaller.Width = 540;
            smaller.Height = 300;
            Game1.uiViewport = smaller;
            var smallBounds = new Rectangle(0, 0, smaller.Width, smaller.Height);
            menu.gameWindowSizeChanged(new Rectangle(0, 0, viewport.Width, viewport.Height), smallBounds);
            Check(GuidePage() == 1 && Focus().myID == selected && smallBounds.Contains(Focus().bounds),
                "Viewport change lost the guide page or its focus.");
            menu.receiveKeyPress(Keys.Left);
            menu.receiveKeyPress(Keys.Enter);
            Check(GuidePage() == 0, "Keyboard navigation did not return to the previous guide page.");
            menu.receiveKeyPress(Keys.Escape);
            Check(!GuideOpen() && ReferenceEquals(Game1.activeClickableMenu, menu) && Raw() == before
                && player.Items.SequenceEqual(inventory)
                && helper.Reflection.GetField<Request?>(session, "Pending").GetValue() is null,
                "Guide navigation changed the operation or left the machine menu.");

            Game1.options.snappyMenus = true;
            Game1.options.gamepadControls = true;
            Game1.options.actionButton = new[] { new InputButton(Keys.Enter) };
            menu.snapToDefaultClickableComponent();
            PadMove(Buttons.DPadDown);
            PadConfirm();
            string started = Raw();
            Check(started != before && session.IsComplete && player.Items.CountId(soy) == 3,
                "Gamepad confirmation failed to start exactly one batch.");
            PadMove(Buttons.DPadUp);
            PadConfirm();
            Check(GuideOpen(), "The completed menu lost gamepad access to the guide.");
            PadMove(Buttons.B);
            Check(!GuideOpen() && ReferenceEquals(Game1.activeClickableMenu, menu),
                "Gamepad cancel closed the machine instead of returning from the guide.");
            Game1.timeOfDay += 200;
            PadMove(Buttons.DPadDown);
            PadConfirm();
            Check(Raw() == started && player.Items.CountId(soy) == 3 && session.IsComplete,
                "Gamepad navigation unlocked a second operation in the completed menu.");
            PadMove(Buttons.B);
            Check(Game1.activeClickableMenu is null, "Gamepad cancel did not close the machine menu.");
        }
        finally
        {
            Game1.uiViewport = viewport;
            Game1.options.snappyMenus = snappy;
            Game1.options.gamepadControls = gamepad;
            Game1.options.actionButton = actionButtons;
            Game1.timeOfDay = clock;
            Game1.setMousePositionRaw(cursor.X, cursor.Y);
            Game1.activeClickableMenu = previousMenu;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
