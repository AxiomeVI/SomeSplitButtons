using System;

namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     The one owner of the chapter-clock holds: Save and Quit's after its split, Return to Map's
///     while the checkpoint picker is open.
/// </summary>
// A hold stops the clock without touching TimerStopped. Level.UpdateTime — the only reader of that
// flag in the game — is skipped while any hold stands, and SpeedrunTool's room timer, the other
// reader, is given no time to add (SkipCutsceneRoomTimer.OnTiming). The flag itself is the game's
// alone: it sets it on its own, for a completing heart or a Farewell cutscene, and a hold that wrote
// it could not tell that stop from its own when letting go.
internal static class ClockHold {
    [Flags]
    internal enum Holder {
        None = 0,
        SaveAndQuit = 1,
        ReturnToMap = 2,
    }

    private static Holder holders = Holder.None;

    internal static bool Held => holders != Holder.None;

    internal static bool IsHeldBy(Holder holder) => (holders & holder) != 0;

    internal static void Take(Holder holder) => holders |= holder;

    internal static void Release(Holder holder) => holders &= ~holder;

    /// <summary>Puts a hold back as a save state recorded it.</summary>
    internal static void Restore(Holder holder, bool held) {
        if (held) Take(holder);
        else Release(holder);
    }

    // Hook: stop the chapter clock, file time included, the way TimerStopped does — UpdateTime
    // returns before touching either when the flag is set, so skipping it whole is the same stop.
    internal static void Level_OnUpdateTime(On.Celeste.Level.orig_UpdateTime orig, Level self) {
        if (Held) return;
        orig(self);
    }
}
