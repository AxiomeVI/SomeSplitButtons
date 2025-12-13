using System;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;

public static class StaticSkipCutsceneSplitManager
{
    public static int counter = 0;

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {			
        counter = 0;
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        counter = 0;
    }

    public static void OnClearState()
    {
        counter = 0;
    }

    public static void OnBeforeSaveState(Level level)
    {
        counter = 0;
    }

    public static void OnBeforeLoadState(Level level)
    {
        counter = 0;
    }
}