using System.Reflection;
using Celeste.Mod.SpeedrunTool;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>
///     Keeps SpeedrunTool's save states, and with them its room timer, across the checkpoint list's
///     load into the B-side.
/// </summary>
// SpeedrunTool clears every state saved in another chapter or side when a scene begins, and a clear
// resets its room timer (RoomTimerManager.ClearPbTimes). The B-side load changes side, so it would
// cost the player the A-side state and the timer the list exists to carry. The setting behind it,
// AutoClearStateOnSceneSwitch, is held off for that one load and put back afterwards.
//
// Its check runs after orig(Scene.Begin), for the LevelLoader and again for the Level, so the value
// goes back on the B-side level's first update, after both.
//
// ⚠️ Never written to disk: nothing saves settings between here and that first update. Level.Unpause
// does, and neither LevelEnter nor LevelLoader can be paused.
//
// Hidden from SpeedrunTool's menu and absent from older releases, so it is found by reflection. A
// miss leaves SpeedrunTool's clear in place.
internal static class SceneSwitchClear {
    private static PropertyInfo setting;
    private static Session target;
    private static bool playerValue;

    // Hook: Level.OnBeforeUpdate puts the setting back on the target level's first update.
    internal static void Install() {
        setting = typeof(SpeedrunToolSettings).GetProperty("AutoClearStateOnSceneSwitch",
            BindingFlags.Public | BindingFlags.Instance);
        if (setting?.PropertyType != typeof(bool) || !setting.CanWrite) {
            setting = null;
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool AutoClearStateOnSceneSwitch not found — loading the B-side from the checkpoint list will clear SpeedrunTool's save state and room timer.");
        }
        Everest.Events.Level.OnBeforeUpdate += Level_OnBeforeUpdate;
    }

    internal static void Uninstall() {
        Everest.Events.Level.OnBeforeUpdate -= Level_OnBeforeUpdate;
        Resume();
    }

    /// <summary>Holds the clear off until the level built from <paramref name="next"/> updates.</summary>
    internal static void Suspend(Session next) {
        if (setting == null || target != null || SpeedrunToolSettings.Instance is not { } settings) return;
        playerValue = (bool) setting.GetValue(settings);
        setting.SetValue(settings, false);
        target = next;
    }

    private static void Resume() {
        if (target == null) return;
        target = null;
        if (SpeedrunToolSettings.Instance is { } settings) setting.SetValue(settings, playerValue);
    }

    // The outgoing level still updates after the pick, so the session tells the two apart.
    private static void Level_OnBeforeUpdate(Level level) {
        if (level.Session == target) Resume();
    }
}
