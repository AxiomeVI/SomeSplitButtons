using System;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>
///     The two handlers behind <see cref="SpeedrunToolHooks"/>, and the freeze state they share.
/// </summary>
// Reasons about things this mod does not own — RoomTimerData's record keys, when roomNumber
// advances, which branch of UpdateTimerState writes what. Everything that breaks when SpeedrunTool
// changes lives in Integration/.
internal static class SkipCutsceneRoomTimer {
    private static bool freezeLevelCompleted = true;
    private static bool endingSplitRecorded = false;
    private static bool splittingOnOurOwnButton = false;

    internal static void Reset() {
        freezeLevelCompleted = true;
        endingSplitRecorded = false;
    }

    /// <summary>Splits SpeedrunTool's room timer on behalf of one of this mod's own buttons.</summary>
    // The Save and Quit and Return to Map timers must call this and not RoomTimerManager directly,
    // or the freeze below swallows their split inside an ending cutscene. The finally is what makes
    // the flag safe: a throw out of SpeedrunTool would leave every later split going through.
    internal static void Split() {
        splittingOnOurOwnButton = true;
        try {
            RoomTimerManager.UpdateTimerState();
        }
        finally {
            splittingOnOurOwnButton = false;
        }
    }

    /// <summary>
    ///     Called when the split fires: from here on SpeedrunTool sees the real
    ///     <c>level.Completed</c> again.
    /// </summary>
    internal static void Release() => freezeLevelCompleted = false;

    /// <summary>Whether SpeedrunTool should currently be kept from seeing a completed level.</summary>
    private static bool ShouldFreezeLevelCompleted(Level level) =>
        level != null &&
        SomeSplitButtonsModule.Settings.Enabled &&
        SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton &&
        level.endingChapterAfterCutscene &&
        freezeLevelCompleted;

    /// <summary>
    ///     Keeps SpeedrunTool's room timer running through the ending, then puts the flag back.
    /// </summary>
    // The restore is in a finally because the flag is vanilla's: an exception out of SpeedrunTool
    // would otherwise leave the level permanently marked incomplete, which is worse than the throw.
    public static void OnTiming(Action<object, Level> orig, object self, Level level) {
        bool wasCompleted = level.Completed;
        if (ShouldFreezeLevelCompleted(level)) {
            level.Completed = false;
        }
        try {
            orig(self, level);
        } finally {
            level.Completed = wasCompleted;
        }
    }

    /// <summary>
    ///     Lets the split that opens the ending through, then swallows SpeedrunTool's timer-state
    ///     transitions for the rest of the freeze.
    /// </summary>
    // Without this mod, SpeedrunTool splits the moment the ending cutscene triggers and then locks.
    // The runner wants that first split *and* one at the button, so exactly one call gets through
    // per freeze — not "stop swallowing", because both `case Timing:` and `case Completed:` write
    // ThisRunTimes[key] = Time and level.Completed stays true for the whole cutscene, so every later
    // call would overwrite the first split with a larger time.
    //
    // ⚠️ Hiding level.Completed is what makes that call land as its own room. SpeedrunTool keys every
    // record on `TimeKeyPrefix + roomNumber` and only advances roomNumber under `if
    // (!level.Completed)`, so without it both splits share a key and the second overwrites the
    // first. It also makes SpeedrunTool break before the second write to ThisRunTimes[pbTimeKey], so
    // the call still produces exactly one record.
    public static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (!ShouldFreezeLevelCompleted(Engine.Scene as Level)) {
            orig(endPoint);
            return;
        }
        // The mod's own buttons go through on the ending split's terms rather than being counted
        // against its one-call budget: level.Completed is hidden for them too, so SpeedrunTool
        // advances roomNumber and records a new room instead of overwriting the ending's time.
        if (!splittingOnOurOwnButton) {
            if (endingSplitRecorded) return;
            endingSplitRecorded = true;
        }

        Level level = (Level) Engine.Scene;
        bool wasCompleted = level.Completed;
        level.Completed = false;
        try {
            orig(endPoint);
        } finally {
            level.Completed = wasCompleted;
        }
    }
}
