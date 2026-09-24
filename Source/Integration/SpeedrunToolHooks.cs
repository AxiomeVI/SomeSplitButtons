using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.RuntimeDetour;
using System;
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
// Reflection rather than an `On.` hook because neither method is vanilla, and MonoMod can only
// generate those for the game's own assembly. The cost is that a rename compiles fine and fails at
// runtime, which is what the warnings exist to make legible — without them the Skip Cutscene split
// simply stops splitting, and that reads as a bug here rather than a version mismatch.
//
// Everything degrades to a warning, including the three faults that used to throw out of Load() and
// make Everest refuse the whole mod: a changed signature, a new overload making GetMethod
// ambiguous, and a renamed handler on this side. Hence the explicit parameter types and nameof.
internal static class SpeedrunToolHooks {
    private static Hook timingHook;
    private static Hook updateTimerStateHook;

    internal static void Install() {
        updateTimerStateHook = TryHook(
            () => typeof(RoomTimerManager).GetMethod(
                nameof(RoomTimerManager.UpdateTimerState),
                BindingFlags.Public | BindingFlags.Static,
                null, new[] {typeof(bool)}, null),
            nameof(SkipCutsceneRoomTimer.OnUpdateTimerState),
            "SpeedrunTool RoomTimerManager.UpdateTimerState not found — the Skip Cutscene split will not hold back the room timer.");

        // RoomTimerData is internal to SpeedrunTool, so it has to come from the assembly by name
        // rather than from a typeof. A missing *type* carries the same warning as a missing method,
        // because to a player they are the same fault.
        timingHook = TryHook(
            () => typeof(RoomTimerManager).Assembly
                .GetType("Celeste.Mod.SpeedrunTool.RoomTimer.RoomTimerData")
                ?.GetMethod("Timing",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] {typeof(Level)}, null),
            nameof(SkipCutsceneRoomTimer.OnTiming),
            "SpeedrunTool RoomTimerData.Timing not found — the room timer will stop at chapter completion instead of at the Skip Cutscene mark.");
    }

    /// <summary>Installs one detour, or logs <paramref name="warning"/> and returns null.</summary>
    // The target is a delegate rather than a MethodInfo so a throw from the *lookup* lands in the
    // same catch as a throw from `new Hook`. Neither may take the mod down with it.
    private static Hook TryHook(Func<MethodInfo> target, string handlerName, string warning) {
        try {
            MethodInfo from = target();
            MethodInfo to = typeof(SkipCutsceneRoomTimer)
                .GetMethod(handlerName, BindingFlags.Public | BindingFlags.Static);
            if (from != null && to != null) return new Hook(from, to);
            Logger.Warn(nameof(SomeSplitButtonsModule), warning);
        }
        catch (Exception e) {
            Logger.Warn(nameof(SomeSplitButtonsModule), $"{warning} ({e.GetType().Name}: {e.Message})");
        }
        return null;
    }

    internal static void Uninstall() {
        timingHook?.Dispose();
        timingHook = null;
        updateTimerStateHook?.Dispose();
        updateTimerStateHook = null;
    }
}
