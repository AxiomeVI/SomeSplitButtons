using System;
using System.Collections.Generic;
using Monocle;
using MonoMod.ModInterop;

namespace Celeste.Mod.SomeSplitButtons.Interop;

/// <summary>
///     Lets another mod watch the split buttons being used, without referencing this assembly.
/// </summary>
/// <remarks>
///     <c>observer(action, stage, frame)</c>, with <c>frame</c> being <see cref="Engine.FrameCounter"/>.
///     Every <c>Pressed</c> is followed by exactly one terminal stage — <c>Confirmed</c>,
///     <c>Cancelled</c> or <c>Refused</c> — on every path. <c>Opened</c> terminates nothing.
/// </remarks>
// Only BCL types cross the boundary: ModInterop binds by signature, and an enum would need an
// assembly both sides reference. Bump InteropVersion whenever a string or a signature changes.
[ModExportName("SomeSplitButtons")]
public static class SplitButtonsInterop {
    public static void AddSplitObserver(Action<string, string, ulong> observer) => SplitEvents.Add(observer);

    public static void RemoveSplitObserver(Action<string, string, ulong> observer) => SplitEvents.Remove(observer);

    public static int InteropVersion() => 1;
}

public static class SplitActions {
    public const string SkipCutscene = "SkipCutscene";
    public const string SaveAndQuit = "SaveAndQuit";
    public const string ReturnToMap = "ReturnToMap";
}

public static class SplitStages {
    public const string Pressed = "Pressed";
    public const string Opened = "Opened";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public const string Refused = "Refused";
}

/// <summary>
///     The observer list behind <see cref="SplitButtonsInterop"/>. Kept out of the export class, since
///     ModInterop exports every public static method it finds there.
/// </summary>
internal static class SplitEvents {
    private static readonly List<Action<string, string, ulong>> observers = new();

    /// <summary>The observers already reported as throwing, so each is reported once.</summary>
    private static readonly HashSet<Action<string, string, ulong>> warnedObservers = new();

    internal static void Add(Action<string, string, ulong> observer) {
        if (observer != null) observers.Add(observer);
    }

    internal static void Remove(Action<string, string, ulong> observer) {
        if (observer == null) return;
        observers.Remove(observer);
        // Forgotten on the way out, so a mod that removes a broken observer and adds a fixed one
        // gets told if the new one throws too.
        warnedObservers.Remove(observer);
    }

    /// <summary>Reports an observer's exception, once per observer.</summary>
    // An observer that throws usually throws every time, and this runs on every button press — one
    // broken mod would otherwise repeat the same stack trace into the log for a whole session. The
    // first throw carries the exception; after that there is nothing new to say.
    private static void Warn(Action<string, string, ulong> observer, string action, string stage, Exception e) {
        if (!warnedObservers.Add(observer)) return;
        Logger.Warn(nameof(SomeSplitButtonsModule),
            $"A split observer threw on {action} {stage} and will not be reported again: {e}");
    }

    internal static void Emit(string action, string stage) {
        if (observers.Count == 0) return;
        ulong frame = Engine.FrameCounter;
        // A copy, so an observer may remove itself; a try per observer, so another mod's exception
        // can neither break a split nor starve the observers after it.
        foreach (Action<string, string, ulong> observer in observers.ToArray()) {
            try {
                observer(action, stage, frame);
            }
            catch (Exception e) {
                Warn(observer, action, stage, e);
            }
        }
    }
}
