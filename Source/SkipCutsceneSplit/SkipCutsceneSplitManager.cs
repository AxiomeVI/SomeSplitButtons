using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool.RoomTimer;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;

public static class StaticSkipCutsceneSplitManager
{
    public static int cutsceneSkippedCounter = 0;
    public static bool pressed = false;

    public static void HandleButtonPressed() {
        cutsceneSkippedCounter = 0;
        pressed = true;
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {			
        cutsceneSkippedCounter = 0;
        pressed = false;
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        cutsceneSkippedCounter = 0;
        pressed = false;
    }

    public static void OnClearState() {
        cutsceneSkippedCounter = 0;
        pressed = false;
    }
}

public static class SkipCutsceneTimer {
    private static int frameCounter = 0;
    private static bool pressed = false;
    private const int END_CS_FADEOUT_FRAMES = 18;
    private const int PROLOGUE_END_CS_FADEOUT_FRAMES = 232;

    public static void HandleTimerButtonPressed() {
        pressed = true;
        frameCounter = 0;
    }

    public static void Update(bool isPrologueCutscene) {
        if (pressed) {
            frameCounter++;
            if (frameCounter > (isPrologueCutscene ? PROLOGUE_END_CS_FADEOUT_FRAMES : END_CS_FADEOUT_FRAMES)) {
                pressed = false;
                frameCounter = 0;
                RoomTimerManager.UpdateTimerState();
            }
        }
        else {
            frameCounter = 0;
        }
    }
}