using System.Reflection;
using Celeste.Mod.SpeedrunTool;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>
///     Keeps SpeedrunTool's save states, and with them its room timer, across the checkpoint list's
///     load into another chapter or side.
/// </summary>
// SpeedrunTool clears every state saved in another chapter or side when a scene begins, and a clear
// resets its room timer (RoomTimerManager.ClearPbTimes). Every destination changes chapter or side, and
// a checkpoint picked after one still loads away from the kept state's area, so either would cost the
// player their state and the timer the list exists to carry. The setting behind it,
// AutoClearStateOnSceneSwitch, is held off for every pick's load and put back afterwards.
//
// ⚠️ Never written to disk while held: LoadChain puts it back on the destination level's first update,
// or as soon as the scene leaves the load, before the next scene begins. The Core vignette can be paused
// and quit, so "nothing can save settings on the way" does not hold on its own.
//
// Hidden from SpeedrunTool's menu and absent from older releases, so it is found by reflection. A
// miss leaves SpeedrunTool's clear in place.
internal static class SceneSwitchClear {
    private static PropertyInfo setting;
    private static bool suspended;
    private static bool playerValue;

    internal static void Install() {
        setting = typeof(SpeedrunToolSettings).GetProperty("AutoClearStateOnSceneSwitch",
            BindingFlags.Public | BindingFlags.Instance);
        if (setting?.PropertyType != typeof(bool) || !setting.CanWrite) {
            setting = null;
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool AutoClearStateOnSceneSwitch not found — loading another chapter or side from the checkpoint list will clear SpeedrunTool's save state and room timer.");
        }
    }

    internal static void Uninstall() {
        Resume();
    }

    /// <summary>Holds the clear off. <see cref="ReturnToMapSplit.LoadChain"/> decides when it comes back.</summary>
    internal static void Suspend() {
        if (setting == null || suspended || SpeedrunToolSettings.Instance is not { } settings) return;
        playerValue = (bool) setting.GetValue(settings);
        setting.SetValue(settings, false);
        suspended = true;
    }

    internal static void Resume() {
        if (!suspended) return;
        suspended = false;
        if (SpeedrunToolSettings.Instance is { } settings) setting.SetValue(settings, playerValue);
    }
}
