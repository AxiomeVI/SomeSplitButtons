using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using Celeste.Mod.SpeedrunTool;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>
///     Keeps SpeedrunTool's save states, and with them its room timer, across the checkpoint list's
///     load into another chapter or side.
/// </summary>
// Before 3.29, SpeedrunTool clears every state saved in another chapter or side when a scene begins,
// and the clear resets its room timer. AutoClearStateOnSceneSwitch is held off for every pick's load
// and put back afterwards. From 3.29 on, SpeedrunTool keeps states across chapters by default and shows
// the option in its menu, so nothing is held and the player's choice stands.
//
// ⚠️ SpeedrunTool can save its settings while the value is held: its Toggle Hotkeys hotkey saves from
// any scene, and its level hotkeys can save on the target level's first frame. The SaveSettings hook
// writes the player's value instead. Without it the held false reaches disk, and every later boot
// keeps it.
//
// Absent from older releases, so it is found by reflection. A miss leaves SpeedrunTool's clear in place.
internal static class SceneSwitchClear {
    private static PropertyInfo setting;
    private static Hook saveSettingsHook;
    private static bool suspended;
    private static bool playerValue;

    /// <summary>Whether a pick holds the clear off: false from SpeedrunTool 3.29 on.</summary>
    internal static bool holds;

    internal static void Install() {
        setting = typeof(SpeedrunToolSettings).GetProperty("AutoClearStateOnSceneSwitch",
            BindingFlags.Public | BindingFlags.Instance);
        if (setting?.PropertyType != typeof(bool) || !setting.CanWrite) {
            setting = null;
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool AutoClearStateOnSceneSwitch not found — loading another chapter or side from the checkpoint list will clear SpeedrunTool's save state and room timer.");
        }

        // The marker of 3.29's migration of this setting, not a version string: it is what changed.
        holds = typeof(SpeedrunToolSettings).GetProperty("SceneSwitchDefaultMigrated",
            BindingFlags.Public | BindingFlags.Instance) == null;

        try {
            saveSettingsHook = new Hook(
                typeof(EverestModule).GetMethod(nameof(EverestModule.SaveSettings), Type.EmptyTypes),
                OnSaveSettings);
        }
        catch (Exception e) {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                $"EverestModule.SaveSettings could not be hooked — a SpeedrunTool settings save during a load from the checkpoint list may store AutoClearStateOnSceneSwitch off. ({e.GetType().Name}: {e.Message})");
        }
    }

    internal static void Uninstall() {
        Resume();
        saveSettingsHook?.Dispose();
        saveSettingsHook = null;
    }

    /// <summary>Holds the clear off. <see cref="ReturnToMapSplit.LoadChain"/> decides when it comes back.</summary>
    internal static void Suspend() {
        if (!holds || setting == null || suspended || SpeedrunToolSettings.Instance is not { } settings) return;
        playerValue = (bool) setting.GetValue(settings);
        setting.SetValue(settings, false);
        suspended = true;
    }

    internal static void Resume() {
        if (!suspended) return;
        suspended = false;
        if (SpeedrunToolSettings.Instance is { } settings) setting.SetValue(settings, playerValue);
    }

    // Hook: a save of SpeedrunTool's settings while the clear is held writes the player's value.
    private static void OnSaveSettings(Action<EverestModule> orig, EverestModule module) {
        if (!suspended || module is not SpeedrunToolModule || SpeedrunToolSettings.Instance is not { } settings) {
            orig(module);
            return;
        }
        setting.SetValue(settings, playerValue);
        try {
            orig(module);
        }
        finally {
            setting.SetValue(settings, false);
        }
    }
}
