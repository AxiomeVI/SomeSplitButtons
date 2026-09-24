using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.Splits;


namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;

internal static class SkipCutsceneTimer {
    private static readonly SplitCountdown countdown = new(() => FadeoutFrames);
    private static bool inPrologue = false;
    private static bool hidden = false; // Hide the button after the first press
    internal static bool Hidden => hidden;

    // Arm, not TryArm: this split stays in the level, so there is no collectible for it to lose and
    // nothing for a collect check to refuse.
    internal static void HandleButtonPressed() {
        hidden = true;
        countdown.Arm();
    }

    /// <summary>What the pause-menu button does: leave the pause and arm the split.</summary>
    // This split never refuses — there is no collectible to lose on a cutscene skip — so it has no
    // return value and its interop sequence ends on the press.
    internal static void Press(Level level) {
        HandleButtonPressed();
        level.Unpause();
    }

    internal static void Reset() {
        hidden = false;
        countdown.Reset();
        SkipCutsceneRoomTimer.Reset();
    }

    internal static void PrologueCheck(int chapterIndex) {
        inPrologue = chapterIndex == -1; // Prologue chapter index is -1
    }

    /// <summary>How long this split will wait, for the chapter it was last refreshed for.</summary>
    // Exposed so the pause-menu description quotes the wait rather than deciding it a second time
    // from a different source — the live session instead of the flag set on level load.
    internal static int FadeoutFrames =>
        inPrologue ? SplitTimings.PROLOGUE_END_CS_FADEOUT_FRAMES : SplitTimings.END_CS_FADEOUT_FRAMES;

    internal static bool InPrologue => inPrologue;

    internal static void Update(Level level) {
        if (!countdown.Tick()) return;

        SkipCutsceneRoomTimer.Release();
        level.Completed = true;
    }
}
