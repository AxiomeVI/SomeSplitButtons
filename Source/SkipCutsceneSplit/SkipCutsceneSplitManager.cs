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

    private static bool ShouldFreezeLevelCompleted() =>
        SomeSplitButtonsModule.Settings.Enabled &&
        SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton &&
        freezeLevelCompleted;

    public static void OnTiming(Action<object, Level> orig, object self, Level level) {
        if (ShouldFreezeLevelCompleted()) {
            level.Completed = false;
        }
        orig(self, level);
    }

    public static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (ShouldFreezeLevelCompleted() && Engine.Scene is Level level) {
            level.Completed = false;
        }
        orig(endPoint);
    }
}