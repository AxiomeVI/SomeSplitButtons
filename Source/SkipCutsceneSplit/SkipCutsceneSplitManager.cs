using System;
using Celeste.Mod.SomeSplitButtons.Splits;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;

public static class SkipCutsceneTimer {
    private static int frameCounter = 0;
    private static bool pressed = false;
    private static bool inPrologue = false;
    private static bool hidden = false; // Hide the button after the first press
    public static bool Hidden => hidden;
    private static bool freezeLevelCompleted = true;
    private static bool endingSplitRecorded = false;

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
        freezeLevelCompleted = true;
        endingSplitRecorded = false;
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
                freezeLevelCompleted = false;
                level.Completed = true;
            }
        }
    }

    /// <summary>
    ///     Whether SpeedrunTool should currently be kept from seeing a completed level.
    /// </summary>
    private static bool ShouldFreezeLevelCompleted(Level level) =>
        level != null &&
        SomeSplitButtonsModule.Settings.Enabled &&
        SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton &&
        level.endingChapterAfterCutscene &&
        freezeLevelCompleted;

    /// <summary>
    ///     Keeps SpeedrunTool's room timer running through the ending, then puts the flag back.
    /// </summary>
    // The restore is in a finally because the flag is vanilla's, not ours: letting an exception out
    // of SpeedrunTool would leave the level permanently marked incomplete, which is a worse failure
    // than whatever threw — the chapter would never register as finished for anything that reads it.
    public static void OnTiming(Action<object, Level> orig, object self, Level level) {
        bool wasCompleted = level.Completed;
        if (ShouldFreezeLevelCompleted(level)) {
            level.Completed = false;
        }
        try {
            orig(self, level);
        } finally {
            level.Completed = wasCompleted;
        }
    }

    /// <summary>
    ///     Lets the split that opens the ending through, then swallows SpeedrunTool's timer-state
    ///     transitions for the rest of the freeze.
    /// </summary>
    // Without this mod, SpeedrunTool splits the moment the ending cutscene triggers and then locks:
    // nothing can split again. The runner wants to keep that first split *and* still split at the
    // button, so exactly one call gets through per freeze.
    //
    // Not "stop swallowing": both `case Timing:` and `case Completed:` of RoomTimerData.
    // UpdateTimerState write ThisRunTimes[key] = Time, and level.Completed stays true for every
    // frame of the cutscene — so every later call would overwrite that first split with a larger
    // time, and the split would drift until the button was pressed.
    //
    // That one call is also made to look like an ordinary room split, by hiding level.Completed for
    // its duration. SpeedrunTool keys every record on `TimeKeyPrefix + roomNumber` and only advances
    // roomNumber under `if (!level.Completed)` — so without this both splits land on the same key
    // and the button's overwrites the ending's instead of joining it. Hiding the flag also makes
    // SpeedrunTool take its `break` before the second write to `ThisRunTimes[pbTimeKey]`, so the
    // call still produces exactly one record.
    public static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (ShouldFreezeLevelCompleted(Engine.Scene as Level) is false) {
            orig(endPoint);
            return;
        }
        if (endingSplitRecorded) return;
        endingSplitRecorded = true;

        Level level = (Level) Engine.Scene;
        bool wasCompleted = level.Completed;
        level.Completed = false;
        try {
            orig(endPoint);
        } finally {
            level.Completed = wasCompleted;
        }
    }
}