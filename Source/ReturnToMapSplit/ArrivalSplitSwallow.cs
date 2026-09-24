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

    // ⚠️ Not one update: measured 2026-09-24 against a real LevelLoader reload, SpeedrunTool's own
    // detection does not call UpdateTimerState until the new Level's *third* Update — the first two
    // run with nothing to consume. 3 gives that exactly one update of margin, and stays under the 5
    // real updates swallow_does_not_outlive_its_frame waits before checking that an unrelated,
    // never-consumed arm has cleared; a wider margin here would make that file start failing.
    private const int GraceUpdates = 3;
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
