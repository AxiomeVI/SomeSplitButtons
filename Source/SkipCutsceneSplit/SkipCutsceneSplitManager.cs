using System;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;

public static class StaticSkipCutsceneSplitManager
{
    public static int counter = 0;
    public static int cutsceneTimer = 0;

    public static void Reset() {
        counter = 0;
        cutsceneTimer = 0;
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {			
        Reset();
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        Reset();
    }

    public static void OnClearState()
    {
        Reset();
    }

    public static void OnBeforeSaveState(Level level)
    {
        Reset();
    }

    public static void OnBeforeLoadState(Level level)
    {
        Reset();
    }
}