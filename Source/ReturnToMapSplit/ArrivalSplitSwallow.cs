namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     Suppresses the room-change split SpeedrunTool fires shortly after a checkpoint load.
/// </summary>
// ⚠️ Nothing in SplitFeatures.ResetAll may reach this. Everest.Events.LevelLoader.OnLoadingThread
// fires during the load and before the new Level's first Update, and its handler calls ResetAll —
// so a flag reachable from any feature's Reset is wiped during the load it exists to cover. That is
// why this is its own class and not a field on ReturnToMapTimer or SkipCutsceneRoomTimer.
internal static class ArrivalSplitSwallow {
    private static bool armed;

    // Measured minimum against a real LevelLoader reload: 2 TickGraceBudget calls run with nothing
    // to consume (the arming update's own call, then the new level's first Update) before
    // SpeedrunTool's own detection lands, on the new level's second Update. GraceUpdates ships at 6
    // for margin — the real ceiling is "before the reloaded level hands control back" (fade-in, then
    // the player's first controllable update), tens of updates, not the 2 measured here.
    //
    // If SpeedrunTool's detection ever lands later than this budget, the failure is a visible extra
    // split, not corruption: TickGraceBudget will have already disarmed, so the arrival's own
    // UpdateTimerState call goes through unswallowed and roomNumber advances one further than
    // intended.
    internal const int GraceUpdates = 6;
    private static int updatesRemaining;

    internal static bool Armed => armed;

    internal static void Arm() {
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
    internal static void TickGraceBudget() {
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
