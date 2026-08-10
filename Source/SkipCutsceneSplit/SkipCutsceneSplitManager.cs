using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.Splits;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;

public static class SkipCutsceneTimer {
    private static int frameCounter = 0;
    private static bool pressed = false;
    private static bool inPrologue = false;
    private static bool hidden = false; // Hide the button after the first press
    public static bool Hidden => hidden;

    public static void HandleButtonPressed() {
        hidden = true;
        pressed = true;
        frameCounter = 0;
    }

    public static void Reset()
    {
        hidden = false;
        frameCounter = 0;
        pressed = false;
        SkipCutsceneRoomTimer.Reset();
    }

    public static void PrologueCheck(int chapterIndex)
    {
        inPrologue = chapterIndex == -1; // Prologue chapter index is -1
    }

    /// <summary>
    ///     How long this split will wait, for the chapter it was last refreshed for.
    /// </summary>
    // Exposed so the pause-menu description can quote the wait rather than decide it a second time.
    // The button used to say "232 frames" by re-testing ChapterIndex == -1 itself, which is the same
    // question asked of a different source — the live session instead of the flag set on level load.
    public static int FadeoutFrames =>
        inPrologue ? SplitTimings.PROLOGUE_END_CS_FADEOUT_FRAMES : SplitTimings.END_CS_FADEOUT_FRAMES;

    public static bool InPrologue => inPrologue;

    public static void Update(Level level) {
        if (pressed) {
            frameCounter++;
            if (frameCounter > FadeoutFrames) {
                pressed = false;
                frameCounter = 0;
                SkipCutsceneRoomTimer.Release();
                level.Completed = true;
            }
        }
    }
}
