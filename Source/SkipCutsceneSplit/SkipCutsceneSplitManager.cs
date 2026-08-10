using System;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;

public static class SkipCutsceneTimer {
    private const int END_CS_FADEOUT_FRAMES = 18;
    private const int PROLOGUE_END_CS_FADEOUT_FRAMES = 232;

    private static int frameCounter = 0;
    private static bool pressed = false;
    private static bool inPrologue = false;
    private static bool hidden = false; // Hide the button after the first press
    public static bool Hidden => hidden;
    private static bool freezeLevelCompleted = true;

    public static void HandleButtonPressed() {
        hidden = true;
        pressed = true;
        frameCounter = 0;
    }

    public static void OnSaveState() => Reset();
    public static void OnLoadState() => Reset();
    public static void OnClearState() => Reset();

    public static void Reset()
    {
        hidden = false;
        frameCounter = 0;
        pressed = false;
        freezeLevelCompleted = true;
    }

    public static void PrologueCheck(int chapterIndex)
    {
        inPrologue = chapterIndex == -1; // Prologue chapter index is -1
    }

    public static void Update(Level level) {
        if (pressed) {
            frameCounter++;
            if (frameCounter > (inPrologue ? PROLOGUE_END_CS_FADEOUT_FRAMES : END_CS_FADEOUT_FRAMES)) {
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
    ///     Swallows SpeedrunTool's timer-state transitions for the duration of the freeze.
    /// </summary>
    public static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (ShouldFreezeLevelCompleted(Engine.Scene as Level)) return;
        orig(endPoint);
    }
}