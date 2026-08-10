using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>
///     The two SpeedrunTool methods this mod detours, resolved by reflection so a rename on its side
///     costs a warning instead of a crash.
/// </summary>
// Gathered here so that every way this mod can break when SpeedrunTool changes lives in one
// directory, and so that Load() reads as a list of what the mod installs rather than as the
// mechanics of installing it.
//
// Reflection rather than an `On.` hook because neither method is vanilla — MonoMod can only generate
// those for the game's own assembly. The cost is that a rename compiles fine and fails at runtime,
// which is exactly what the warnings below exist to make legible: without them the Skip Cutscene
// split simply stops splitting, and that reads as a bug in this mod rather than a version mismatch.
internal static class SpeedrunToolHooks {
    private static Hook _timingHook;
    private static Hook _updateTimerStateHook;

    internal static void Install() {
        MethodInfo updateTimerState = typeof(RoomTimerManager)
            .GetMethod("UpdateTimerState", BindingFlags.Public | BindingFlags.Static);
        if (updateTimerState != null) {
            _updateTimerStateHook = new Hook(
                updateTimerState,
                typeof(SkipCutsceneTimer).GetMethod("OnUpdateTimerState", BindingFlags.Public | BindingFlags.Static)
            );
        }
        else {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool RoomTimerManager.UpdateTimerState not found — the Skip Cutscene split will not hold back the room timer.");
        }

        // RoomTimerData is internal to SpeedrunTool, so it has to come from the assembly by name
        // rather than from a typeof. The null-conditional carries a missing *type* as well as a
        // missing method: both end in the same warning, because to a player they are the same fault.
        System.Type roomTimerData = typeof(RoomTimerManager).Assembly
            .GetType("Celeste.Mod.SpeedrunTool.RoomTimer.RoomTimerData");
        MethodInfo timing = roomTimerData?.GetMethod("Timing", BindingFlags.Public | BindingFlags.Instance);
        if (timing != null) {
            _timingHook = new Hook(
                timing,
                typeof(SkipCutsceneTimer).GetMethod("OnTiming", BindingFlags.Public | BindingFlags.Static)
            );
        }
        else {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool RoomTimerData.Timing not found — the room timer will stop at chapter completion instead of at the Skip Cutscene mark.");
        }
    }

    internal static void Uninstall() {
        _timingHook?.Dispose();
        _timingHook = null;
        _updateTimerStateHook?.Dispose();
        _updateTimerStateHook = null;
    }
}
