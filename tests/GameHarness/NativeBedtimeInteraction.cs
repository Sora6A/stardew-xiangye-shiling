using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace GameHarness;

/// <summary>Positions the disposable player, then lets the native location update open the actual bed question.</summary>
internal sealed class NativeBedtimeInteraction
{
    private readonly Vector2 BedTile;
    private int ApproachTicks;
    private DialogueBox? Prompt;

    internal int StartTotalDays { get; }
    internal bool AnswerSent { get; private set; }

    internal NativeBedtimeInteraction()
    {
        if (Game1.currentLocation is not FarmHouse house || Game1.activeClickableMenu is not null || Game1.eventUp)
            throw new InvalidOperationException("The disposable host must be free to interact inside its farmhouse.");
        var bed = house.GetPlayerBed() ?? throw new InvalidOperationException("The disposable farmhouse has no player bed.");
        // GetPlayerBedSpot points at the wake-up tile. Sleep is one row below the furniture origin.
        var origin = bed.TileLocation;
        var candidates = Enumerable.Range((int)origin.X, bed.getTilesWide())
            .Select(x => new Vector2(x, origin.Y + 1))
            .Where(tile => house.doesTileHaveProperty((int)tile.X, (int)tile.Y, "TouchAction", "Back") == "Sleep").ToArray();
        if (candidates.Length != 1)
            throw new InvalidOperationException("The player bed must expose exactly one native Sleep touch tile.");
        this.BedTile = candidates[0];
        Vector2 approach = this.BedTile + Vector2.UnitY;
        if (house.doesTileHaveProperty((int)approach.X, (int)approach.Y, "TouchAction", "Back") is not null)
            throw new InvalidOperationException("The adjacent bed approach tile has another touch action.");
        this.StartTotalDays = Game1.Date.TotalDays;
        Game1.player.setTileLocation(approach);
        Game1.player.behaviorOnMovement(0);
    }

    internal void Tick(Action<string> record)
    {
        if (this.AnswerSent || ++this.ApproachTicks < 3) return;
        if (this.ApproachTicks == 3)
        {
            Game1.player.setTileLocation(this.BedTile);
            return;
        }
        if (Game1.activeClickableMenu is not DialogueBox prompt || Game1.currentLocation.lastQuestionKey != "Sleep") return;
        if (this.Prompt is null)
        {
            this.Prompt = prompt;
            record("HostOpenedNativeSleepQuestion");
        }
        if (!ReferenceEquals(this.Prompt, prompt))
            throw new InvalidOperationException("The native sleep prompt changed before confirmation.");
        if (prompt.transitioning || prompt.safetyTimer > 0 || prompt.characterIndexInDialogue < prompt.getCurrentString().Length)
            return;
        int yes = Array.FindIndex(prompt.responses, response => response.responseKey == "Yes");
        if (yes < 0) throw new InvalidOperationException("The native sleep prompt has no Yes response.");
        prompt.selectedResponse = yes;
        this.AnswerSent = true;
        record("HostSelectedNativeSleepYes");
        prompt.receiveLeftClick(0, 0);
    }
}
