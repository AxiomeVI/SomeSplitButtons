using System;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;

public static class SkipCutsceneTimer {
    private const int END_CS_FADEOUT_FRAMES = 18;
    private const int PROLOGUE_END_CS_FADEOUT_FRAMES = 232;

    private static int frameCounter = 0;
    private static bool pressed = false;
    private static bool inPrologue = false;
    public static bool hidden = false; // Hide the button after the first press

    public static void HandleButtonPressed() {
        hidden = true;
        pressed = true;
        frameCounter = 0;
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {			
        Reset();
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        Reset();
    }

    public static void OnClearState() {
        Reset();
    }

    public static void Reset()
    {
        hidden = false;
        frameCounter = 0;
        pressed = false;
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
                level.Completed = true; // This stops srt timer completely
            }
        }
    }
}