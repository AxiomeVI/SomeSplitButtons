using System;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>
///     The two handlers behind <see cref="SpeedrunToolHooks"/>, and the freeze state they share.
/// </summary>
// Split out of SkipCutsceneTimer, which held a twenty-line state machine and these sixty lines of
// SpeedrunTool internals in one class. They are the most delicate code in the repo and they reason
// about things this mod does not own — RoomTimerData's record keys, when roomNumber advances, which
// branch of UpdateTimerState writes what — so they belong next to the hooks that install them and
// under the same rule: everything that breaks when SpeedrunTool changes lives in Integration/.
//
// The freeze flags live here rather than with the timer because they exist for nothing else. The
// timer owns "how many frames until the split"; this owns "what SpeedrunTool is allowed to see
// while that runs".
internal static class SkipCutsceneRoomTimer {
    private static bool freezeLevelCompleted = true;
    private static bool endingSplitRecorded = false;

    internal static void Reset() {
        freezeLevelCompleted = true;
        endingSplitRecorded = false;
    }

    /// <summary>
    ///     Called when the split fires: from here on SpeedrunTool sees the real
    ///     <c>level.Completed</c> again.
    /// </summary>
    internal static void Release() => freezeLevelCompleted = false;

    /// <summary>
    ///     Whether SpeedrunTool should currently be kept from seeing a completed level.
    /// </summary>
    private static bool ShouldFreezeLevelCompleted(Level level) =>
        level != null &&
        SomeSplitButtonsModule.Settings.Enabled &&
        SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton &&
        level.endingChapterAfterCutscene &&
        freezeLevelCompleted;

    /// <summary>
    ///     Keeps SpeedrunTool's room timer running through the ending, then puts the flag back.
    /// </summary>
    // The restore is in a finally because the flag is vanilla's, not ours: letting an exception out
    // of SpeedrunTool would leave the level permanently marked incomplete, which is a worse failure
    // than whatever threw — the chapter would never register as finished for anything that reads it.
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
    // Without this mod, SpeedrunTool splits the moment the ending cutscene triggers and then locks:
    // nothing can split again. The runner wants to keep that first split *and* still split at the
    // button, so exactly one call gets through per freeze.
    //
    // Not "stop swallowing": both `case Timing:` and `case Completed:` of RoomTimerData.
    // UpdateTimerState write ThisRunTimes[key] = Time, and level.Completed stays true for every
    // frame of the cutscene — so every later call would overwrite that first split with a larger
    // time, and the split would drift until the button was pressed.
    //
    // That one call is also made to look like an ordinary room split, by hiding level.Completed for
    // its duration. SpeedrunTool keys every record on `TimeKeyPrefix + roomNumber` and only advances
    // roomNumber under `if (!level.Completed)` — so without this both splits land on the same key
    // and the button's overwrites the ending's instead of joining it. Hiding the flag also makes
    // SpeedrunTool take its `break` before the second write to `ThisRunTimes[pbTimeKey]`, so the
    // call still produces exactly one record.
    public static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (ShouldFreezeLevelCompleted(Engine.Scene as Level) is false) {
            orig(endPoint);
            return;
        }
        if (endingSplitRecorded) return;
        endingSplitRecorded = true;

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
