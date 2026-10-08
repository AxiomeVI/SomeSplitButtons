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
    private static HiddenEnd hiddenEnd = HiddenEnd.None;
    // Null until an ending is seen. Taken from the settings on its first frame and kept for the rest
    // of it: switched off mid-ending, the freeze would lift and SpeedrunTool record the run's end at
    // the switch, after a cutscene its timer had been running through.
    private static bool? endingFrozen;

    /// <summary>Why SpeedrunTool is not being told the chapter ended, if it is not.</summary>
    // A list that may carry the run on is open, or may still open. Told, SpeedrunTool ends the run: short of
    // NumberOfRooms it writes the end under key NumberOfRooms, shows the comparison, and a pick would carry on
    // a run it has already finished.
    internal enum HiddenEnd { None, Ending, Heart }

    internal static void Reset() {
        SpeedrunToolRecords.Forget();
        freezeLevelCompleted = true;
        endingSplitRecorded = false;
        showCompletedToSpeedrunTool = false;
        hiddenEnd = HiddenEnd.None;
        endingFrozen = null;
    }

    internal static object Snapshot()
        => (freezeLevelCompleted, endingSplitRecorded, showCompletedToSpeedrunTool, hiddenEnd, endingFrozen);

    internal static void Restore(object snapshot)
        => (freezeLevelCompleted, endingSplitRecorded, showCompletedToSpeedrunTool, hiddenEnd, endingFrozen)
            = ((bool, bool, bool, HiddenEnd, bool?)) snapshot;

    /// <summary>
    ///     Splits the Skip Cutscene mark as an ordinary room and keeps the chapter end from SpeedrunTool
    ///     while the list is open.
    /// </summary>
    // Through the freeze's own-button path, which hides the completion and advances the room. The freeze
    // stays: Release is what would show SpeedrunTool the completion.
    internal static void SplitMarkAsRoom() {
        hiddenEnd = HiddenEnd.Ending;
        Split();
    }

    /// <summary>
    ///     Tells SpeedrunTool the chapter ended at the mark after all, as the list closes without a pick.
    /// </summary>
    // The mark's room advance is undone first, so SpeedrunTool records the same key again, with the same
    // time since the list held the clock, as the run's end: no room of no length.
    internal static void RevealMark(Level level) {
        if (hiddenEnd != HiddenEnd.Ending) return;
        hiddenEnd = HiddenEnd.None;
        SpeedrunToolRecords.UndoAdvance(level);
        Release();
        if (!level.Completed) SplitAsCompleted();
    }

    /// <summary>Keeps a heart's chapter end from SpeedrunTool, from its bank.</summary>
    internal static void HideHeartEnd() => hiddenEnd = HiddenEnd.Heart;

    /// <summary>Tells SpeedrunTool a heart ended the chapter, once the player leaves or the list is off.</summary>
    // A wipe out is every normal way of leaving: the poem's confirm, a real Return to Map, a split's fade.
    internal static void UpdateHiddenHeart(Level level) {
        if (hiddenEnd == HiddenEnd.Heart && (level.Wipe != null || !Reentry.ReturnToMap.Alive())) {
            hiddenEnd = HiddenEnd.None;
        }
    }

    /// <summary>Splits SpeedrunTool's room timer on behalf of one of this mod's own buttons.</summary>
    // The Save and Quit and Return to Map timers must call this and not RoomTimerManager directly,
    // or the freeze below swallows their split inside an ending cutscene. The finally is what makes
    // the flag safe: a throw out of SpeedrunTool would leave every later split going through.
    internal static void Split() {
        // After a heart split, the Return to Map split finds no time since: the heart stopped the clock.
        // A room of no length is not a room.
        if (hiddenEnd == HiddenEnd.Heart && SpeedrunToolRecords.NoTimeSinceLastRecord()) return;
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
    private static bool CompletedAsShown(Level level) =>
        hiddenEnd == HiddenEnd.None && (level.Completed || showCompletedToSpeedrunTool);

    /// <summary>Whether SpeedrunTool should currently be kept from seeing a completed level.</summary>
    // The press as well as the settings: a split pressed and then switched off still lands at the
    // mark, and lifting the freeze early would let SpeedrunTool split on the switch-off frame.
    private static bool ShouldFreezeLevelCompleted(Level level) {
        if (level == null || !level.endingChapterAfterCutscene || !freezeLevelCompleted) return false;
        endingFrozen ??= SomeSplitButtonsModule.Settings.Enabled && SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton;
        return endingFrozen.Value || SkipCutsceneTimer.Armed;
    }

    /// <summary>
    ///     Keeps SpeedrunTool's manager from calling UpdateTimerState on every frame of the freeze once
    ///     the ending's split is recorded.
    /// </summary>
    // Not left to OnUpdateTimerState to swallow: a mod detouring UpdateTimerState from outside this one
    // would still see a call every frame. Before the ending's split is recorded, the manager must see
    // the completion, or that split never happens.
    public static void OnManagerTiming(Action<Level> orig, Level level) {
        if (!(endingSplitRecorded && ShouldFreezeLevelCompleted(level)) && hiddenEnd == HiddenEnd.None) {
            orig(level);
            return;
        }
        WithCompleted(level, false, () => orig(level));
    }

    /// <summary>
    ///     Keeps SpeedrunTool's room timer running through the ending, then puts the flag back.
    /// </summary>
    // The restore is in a finally for WithCompleted's reason. Also where a ClockHold stops the room timer, by giving it no time to add. Showing it a stopped
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
    // Exactly one call gets through per freeze, so the runner gets the ending's split and one at the
    // button: level.Completed stays true for the whole cutscene, and every later call would overwrite
    // the first split with a larger time.
    //
    // ⚠️ Hiding level.Completed is what makes that call land as its own room. SpeedrunTool keys every
    // record on `TimeKeyPrefix + roomNumber` and only advances roomNumber under `if
    // (!level.Completed)`, so without it both splits share a key and the second overwrites the
    // first. It also makes SpeedrunTool break before the second write to ThisRunTimes[pbTimeKey], so
    // the call still produces exactly one record.
    public static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (Engine.Scene is not Level level) {
            SpeedrunToolRecords.Around(() => orig(endPoint));
            return;
        }
        bool freeze = ShouldFreezeLevelCompleted(level);
        // The mod's own buttons go through on the ending split's terms rather than being counted
        // against its one-call budget: level.Completed is hidden for them too, so SpeedrunTool
        // advances roomNumber and records a new room instead of overwriting the ending's time.
        if (freeze && !splittingOnOurOwnButton) {
            if (endingSplitRecorded) return;
            endingSplitRecorded = true;
        }
        WithCompleted(level, !freeze && CompletedAsShown(level), () => SpeedrunToolRecords.Around(() => orig(endPoint)));
    }

    /// <summary>Runs <paramref name="call"/> with level.Completed reading <paramref name="shown"/>.</summary>
    // The restore is in a finally because the flag is vanilla's: a throw out of SpeedrunTool would
    // otherwise leave the level marked incomplete for good, which is worse than the throw.
    private static void WithCompleted(Level level, bool shown, Action call) {
        bool real = level.Completed;
        level.Completed = shown;
        try {
            call();
        } finally {
            level.Completed = real;
        }
    }
}
