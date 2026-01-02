using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool.RoomTimer;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;

public static class StaticSkipCutsceneSplitManager
{
    public static bool pressed = false;

    public static void HandleButtonPressed() {
        pressed = true;
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {			
        pressed = false;
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        pressed = false;
    }

    public static void OnClearState() {
        pressed = false;
    }
}

public static class SkipCutsceneTimer {
    private static int frameCounter = 0;
    private static bool pressed = false;
    private static bool inPrologue = false;
    private const int END_CS_FADEOUT_FRAMES = 18;
    private const int PROLOGUE_END_CS_FADEOUT_FRAMES = 232;

    public static void HandleTimerButtonPressed(int index) {
        pressed = true;
        inPrologue = index == -1; // Prologue chapter index is -1
        frameCounter = 0;
    }

    public static void Update() {
        if (pressed) {
            frameCounter++;
            if (frameCounter > (inPrologue ? PROLOGUE_END_CS_FADEOUT_FRAMES : END_CS_FADEOUT_FRAMES)) {
                pressed = false;
                frameCounter = 0;
                inPrologue = false;
                RoomTimerManager.UpdateTimerState();
            }
        }
        else {
            frameCounter = 0;
        }
    }
}