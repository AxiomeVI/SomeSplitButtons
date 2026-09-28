using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>
///     The SpeedrunTool methods this mod detours and calls, and the field it clears, resolved by
///     reflection so a rename on its side costs a warning instead of a crash.
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
//
// The calls too, not only the detours: a direct call compiles against SpeedrunTool and throws
// MissingMethodException inside Level.Update at the moment of a split.
internal static class SpeedrunToolHooks {
    private static Hook timingHook;
    private static Hook managerTimingHook;
    private static Hook updateTimerStateHook;
    private static Action<bool> updateTimerState;
    private static Action<string, string> showPopup;
    private static Func<bool> endPointExists;
    private static FieldInfo previousRoom;

    /// <summary>Splits SpeedrunTool's room timer. Does nothing when the method was not found.</summary>
    // Through the detour above, like any other caller: it patches the method, not a call site.
    internal static void UpdateTimerState() => updateTimerState?.Invoke(false);

    /// <summary>Shows a SpeedrunTool popup. Does nothing when the method was not found.</summary>
    internal static void ShowPopup(string message) => showPopup?.Invoke(message, null);

    /// <summary>
    ///     Whether an end point is set. While one is, SpeedrunTool records a split only at the end
    ///     point or on a completed level, so Save and Quit and Return to Map splits register nothing.
    /// </summary>
    internal static bool EndPointExists => endPointExists?.Invoke() ?? false;

    /// <summary>
    ///     Forgets the room SpeedrunTool last timed, as its own reset does, so the next level it
    ///     times raises no room-change split. Does nothing when the field was not found.
    /// </summary>
    internal static void ForgetPreviousRoom() => previousRoom?.SetValue(null, null);

    internal static void Install() {
        Func<MethodInfo> updateTimerStateMethod = () => typeof(RoomTimerManager).GetMethod(
            nameof(RoomTimerManager.UpdateTimerState),
            BindingFlags.Public | BindingFlags.Static,
            null, new[] {typeof(bool)}, null);
        updateTimerStateHook = TryHook(
            updateTimerStateMethod,
            nameof(SkipCutsceneRoomTimer.OnUpdateTimerState),
            "SpeedrunTool RoomTimerManager.UpdateTimerState not found — the Skip Cutscene split will not hold back the room timer.");
        updateTimerState = TryBind<Action<bool>>(
            updateTimerStateMethod,
            "SpeedrunTool RoomTimerManager.UpdateTimerState not found — the split buttons will not split the room timer.");
        showPopup = TryBind<Action<string, string>>(
            () => typeof(PopupMessageUtils).GetMethod(
                nameof(PopupMessageUtils.Show),
                BindingFlags.Public | BindingFlags.Static,
                null, new[] {typeof(string), typeof(string)}, null),
            "SpeedrunTool PopupMessageUtils.Show not found — hotkey toggles and refused splits will not show a message.");
        endPointExists = TryBind<Func<bool>>(
            () => typeof(RoomTimerManager).Assembly
                .GetType("Celeste.Mod.SpeedrunTool.RoomTimer.EndPoint")
                ?.GetProperty("IsExist", BindingFlags.Public | BindingFlags.Static)
                ?.GetMethod,
            "SpeedrunTool EndPoint.IsExist not found — a split an end point ignores will not say so.");

        previousRoom = typeof(RoomTimerManager).GetField("previousRoom", BindingFlags.NonPublic | BindingFlags.Static);
        if (previousRoom?.FieldType != typeof(string)) {
            previousRoom = null;
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool RoomTimerManager.previousRoom not found — a checkpoint picked after the Return to Map split will split the room timer again on arrival.");
        }

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
        managerTimingHook = TryHook(
            () => typeof(RoomTimerManager).GetMethod("Timing",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] {typeof(Level)}, null),
            nameof(SkipCutsceneRoomTimer.OnManagerTiming),
            "SpeedrunTool RoomTimerManager.Timing not found — during an ending, a mod watching SpeedrunTool's splits may see one every frame.");
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

    /// <summary>Binds a delegate to one method, or logs <paramref name="warning"/> and returns null.</summary>
    // CreateDelegate throws on a changed signature, which lands in the same catch as the lookup.
    private static T TryBind<T>(Func<MethodInfo> target, string warning) where T : Delegate {
        try {
            MethodInfo method = target();
            if (method != null) return method.CreateDelegate<T>();
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
        managerTimingHook?.Dispose();
        managerTimingHook = null;
        updateTimerStateHook?.Dispose();
        updateTimerStateHook = null;
        updateTimerState = null;
        showPopup = null;
        endPointExists = null;
        previousRoom = null;
    }
}
