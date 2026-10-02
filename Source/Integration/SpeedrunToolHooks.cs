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
// Reflection, not `On.` hooks: MonoMod generates those for the game's assembly only. Every lookup,
// bind and detour degrades to a warning, since a throw in Load() makes Everest refuse the mod, and a
// direct call compiled against SpeedrunTool throws MissingMethodException mid-split.
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

        // RoomTimerData is internal to SpeedrunTool, so it comes from the assembly by name.
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

    /// <summary>Installs one detour onto a handler in SkipCutsceneRoomTimer, or warns and returns null.</summary>
    private static Hook TryHook(Func<MethodInfo> target, string handlerName, string warning) => Try(() => {
        MethodInfo from = target();
        MethodInfo to = typeof(SkipCutsceneRoomTimer).GetMethod(handlerName, BindingFlags.Public | BindingFlags.Static);
        return from != null && to != null ? new Hook(from, to) : null;
    }, warning);

    /// <summary>Binds a delegate to one method, or warns and returns null.</summary>
    private static T TryBind<T>(Func<MethodInfo> target, string warning) where T : Delegate
        => Try(() => target()?.CreateDelegate<T>(), warning);

    // The lookup runs inside the try, so a throw from it (an ambiguous overload, a changed signature
    // in CreateDelegate) lands in the same catch as one from `new Hook`.
    private static T Try<T>(Func<T> make, string warning) where T : class {
        try {
            if (make() is T made) return made;
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
