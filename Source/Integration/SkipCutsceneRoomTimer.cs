using System;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;
using Celeste.Mod.SomeSplitButtons.Splits;
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
    private static bool showCompletedToSpeedrunTool = false;

    internal static void Reset() {
        SpeedrunToolRecords.Forget();
        freezeLevelCompleted = true;
        endingSplitRecorded = false;
        showCompletedToSpeedrunTool = false;
    }

    internal static object Snapshot() => (freezeLevelCompleted, endingSplitRecorded, showCompletedToSpeedrunTool);

    internal static void Restore(object snapshot)
        => (freezeLevelCompleted, endingSplitRecorded, showCompletedToSpeedrunTool) = ((bool, bool, bool)) snapshot;

    /// <summary>Splits SpeedrunTool's room timer on behalf of one of this mod's own buttons.</summary>
    // The Save and Quit and Return to Map timers must call this and not RoomTimerManager directly,
    // or the freeze below swallows their split inside an ending cutscene. The finally is what makes
    // the flag safe: a throw out of SpeedrunTool would leave every later split going through.
    internal static void Split() {
        splittingOnOurOwnButton = true;
        try {
            SpeedrunToolHooks.UpdateTimerState();
        }
        finally {
            splittingOnOurOwnButton = false;
        }
    }

    /// <summary>
    ///     Called when the split fires: from here on SpeedrunTool is no longer shown an incomplete
    ///     level.
    /// </summary>
    internal static void Release() => freezeLevelCompleted = false;

    /// <summary>
    ///     Splits on the Skip Cutscene mark in a level the game has not completed yet, and shows
    ///     SpeedrunTool a completed level from then on. The game's own flag is never written.
    /// </summary>
    // For the rest of the level, not only for this call: RoomTimerData.Timing puts timerState back to
    // Timing on any frame it reads the level incomplete, which would restart the room timer.
    internal static void SplitAsCompleted() {
        showCompletedToSpeedrunTool = true;
        Split();
    }

    /// <summary>What SpeedrunTool is shown for <c>level.Completed</c> outside the freeze.</summary>
    private static bool CompletedAsShown(Level level) => level.Completed || showCompletedToSpeedrunTool;

    /// <summary>Whether SpeedrunTool should currently be kept from seeing a completed level.</summary>
    // The press as well as the setting: a split pressed and then switched off still lands at the
    // mark, and lifting the freeze early would let SpeedrunTool split on the switch-off frame.
    private static bool ShouldFreezeLevelCompleted(Level level) =>
        level != null &&
        ((SomeSplitButtonsModule.Settings.Enabled && SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton)
         || SkipCutsceneTimer.Armed) &&
        level.endingChapterAfterCutscene &&
        freezeLevelCompleted;

    /// <summary>
    ///     Keeps SpeedrunTool's manager from calling UpdateTimerState on every frame of the freeze once
    ///     the ending's split is recorded.
    /// </summary>
    // The manager reads the real level.Completed, true through the whole ending, and calls every frame.
    // Swallowing those calls in OnUpdateTimerState is not enough: a mod detouring UpdateTimerState from
    // outside this one still sees each, with a frame more on a timer the freeze keeps running, and
    // SpeebrunConsistencyTracker recorded a one-frame room on every frame of the ending. Before the
    // ending's split is recorded, the manager must see the completion, or that split never happens.
    public static void OnManagerTiming(Action<Level> orig, Level level) {
        if (!(endingSplitRecorded && ShouldFreezeLevelCompleted(level))) {
            orig(level);
            return;
        }
        bool wasCompleted = level.Completed;
        level.Completed = false;
        try {
            orig(level);
        } finally {
            level.Completed = wasCompleted;
        }
    }

    /// <summary>
    ///     Keeps SpeedrunTool's room timer running through the ending, then puts the flag back.
    /// </summary>
    // The restore is in a finally because the flag is vanilla's: an exception out of SpeedrunTool
    // would otherwise leave the level permanently marked incomplete, which is worse than the throw.
    // Also where a ClockHold stops the room timer, by giving it no time to add. Showing it a stopped
    // level would skip Timing whole, and with it the line that ends the Completed state a split
    // leaves when rooms remain: the timer would show a finished room at 0.000 through the hold.
    public static void OnTiming(Action<object, Level> orig, object self, Level level) {
        bool wasCompleted = level.Completed;
        float rawDeltaTime = Engine.RawDeltaTime;
        level.Completed = !ShouldFreezeLevelCompleted(level) && CompletedAsShown(level);
        if (ClockHold.Held) Engine.RawDeltaTime = 0f;
        try {
            orig(self, level);
        } finally {
            level.Completed = wasCompleted;
            Engine.RawDeltaTime = rawDeltaTime;
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
        Level current = Engine.Scene as Level;
        if (!ShouldFreezeLevelCompleted(current)) {
            if (current == null) {
                SpeedrunToolRecords.Around(() => orig(endPoint));
                return;
            }
            bool completed = current.Completed;
            current.Completed = CompletedAsShown(current);
            try {
                SpeedrunToolRecords.Around(() => orig(endPoint));
            } finally {
                current.Completed = completed;
            }
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
            SpeedrunToolRecords.Around(() => orig(endPoint));
        } finally {
            level.Completed = wasCompleted;
        }
    }
}
