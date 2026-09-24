namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     Suppresses the room-change split SpeedrunTool fires shortly after a checkpoint load.
/// </summary>
// ⚠️ Nothing in SplitFeatures.ResetAll may reach this. Everest.Events.LevelLoader.OnLoadingThread
// fires during the load and before the new Level's first Update, and its handler calls ResetAll —
// so a flag reachable from any feature's Reset is wiped during the load it exists to cover. That is
// why this is its own class and not a field on ReturnToMapTimer or SkipCutsceneRoomTimer.
//
// ⚠️ Engine.FrameCounter is not comparable across the reload. Under a TAS it is CelesteTAS's own
// in-update counter, and a real LevelLoader reload resets it — measured 2026-09-24, `armed at frame
// 179` then the new Level's first Update reads frame 0. A raw `frame != armedAtFrame` comparison
// reads that as "many updates have passed" on the very first one, so this counts updates instead of
// comparing frame numbers; `frame` stays a parameter on both methods only because they are each
// called from exactly one call site and changing the signature buys nothing.
internal static class ArrivalSplitSwallow {
    private static bool armed;

    // ⚠️ Not one update: measured 2026-09-24 against a real LevelLoader reload, with the budget
    // opened wide to observe rather than enforce it. Two ClearIfFrameDiffers calls run with nothing
    // to consume — the arming update itself (the old level, still finishing its own Level_OnUpdate),
    // then the new level's first Update — before SpeedrunTool's own detection calls UpdateTimerState
    // on the new level's *second* Update. 2 is therefore the bare minimum that still catches it; 3
    // ships instead, one update of margin for a checkpoint whose spawn-in settles a frame slower.
    // That margin stays well under the 5 real updates swallow_does_not_outlive_its_frame waits
    // before checking that an unrelated, never-consumed arm has cleared — a wider budget here would
    // make that file start failing.
    //
    // ⚠️ If SpeedrunTool's detection ever lands later than this budget — a heavier checkpoint room,
    // a future SpeedrunTool change — the failure is a visible extra split, not corruption: Consume
    // returns false because ClearIfFrameDiffers has already disarmed, so the arrival's own
    // UpdateTimerState call goes through unswallowed and roomNumber advances one further than
    // intended, exactly the bug this swallow exists to prevent. Nothing silently mismeasures a
    // legitimate split, because the two calls this budget skips both fire before the reloaded level
    // hands control back — Everest's own spawn-in (fade, then the player's first controllable
    // update) has no path to move Madeline into a different room inside a handful of Updates, so
    // there is no genuine room-change split for this window to eat by mistake.
    internal const int GraceUpdates = 3;
    private static int updatesRemaining;

    internal static bool Armed => armed;

    internal static void Arm(ulong frame) {
        armed = true;
        updatesRemaining = GraceUpdates;
    }

    /// <summary>Swallows one split, and returns whether it did.</summary>
    internal static bool Consume() {
        if (!armed) return false;
        armed = false;
        return true;
    }

    /// <summary>
    ///     Disarms once <see cref="GraceUpdates"/> updates have passed since the arm, whether or not
    ///     it was used.
    /// </summary>
    // On use, not on a budget, is the tempting version and it is wrong: picking the checkpoint for
    // the room you already stand in leaves previousRoom == Session.Level, so no room-change split
    // fires, nothing consumes the flag, and it eats the next genuine transition instead.
    internal static void ClearIfFrameDiffers(ulong frame) {
        if (!armed) return;
        if (updatesRemaining > 0) {
            updatesRemaining--;
            return;
        }
        armed = false;
    }

    internal static void Reset() {
        armed = false;
        updatesRemaining = 0;
    }
}
