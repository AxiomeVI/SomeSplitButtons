#nullable enable
using System;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>
/// A crystal heart is written to the save at the end of its collect routine, not when it is touched.
/// Vanilla's Save and Quit leaves the Level paused for the whole fade-out, so a press before that
/// freezes the routine where it stands and the heart is lost; this mod's fade-out runs live, so the
/// routine finishes and the heart is kept. The split has to refuse until the heart is banked, or it
/// rewards a press the run would have punished.
/// </summary>
// Unlike the berry check this one cannot be switched off. The berry setting exists because a runner
// practising a segment where the berry is not the point has a reason to split anyway; there is no
// such reason here, since a split before the bank is not a faster version of the strat but a
// different one that the game does not allow.
public static class HeartCheck {
    /// <summary>
    /// The update <c>RegisterAsCollected</c> lands on, counting from the first one that sees a heart
    /// collecting.
    /// </summary>
    // Measured 2026-08-29 by test/Heart/banks_on_the_expected_update, and deliberately not the 135
    // that reading HeartGem.CollectRoutine gives: it opens on Celeste.Freeze(0.2f), and a freeze
    // stops the scene outright, so 12 of those frames update neither the routine nor this count. The
    // two units agree from any press onwards, the freeze being long over by the time a pause menu
    // can be reached.
    //
    // ⚠️ Display only. What refuses the split is the pair of vanilla flags below, so a drift in this
    // figure costs a wrong number in a message and never a wrong decision.
    private const int COLLECT_UPDATES = 124;

    private static int updates;

    /// <summary>
    /// True while a heart is collecting and has not been written yet.
    /// </summary>
    // The two flags are vanilla's own and bracket exactly that window: HeartGem.collected is set on
    // the frame of the touch, and level.Frozen on the line beside RegisterAsCollected. Neither is a
    // count, so this cannot drift with a game update the way COLLECT_UPDATES can.
    //
    // Session.HeartGem would be the obvious third test and is the wrong one: on a B or C side the
    // routine also completes the chapter, and a session that already carries the heart from an
    // earlier attempt would read as safe while the completion is still unwritten.
    private static bool Collecting(Level level) =>
        !level.Frozen && level.Tracker.GetEntity<HeartGem>() is {IsFake: false, collected: true};

    /// <summary>
    /// Counts the routine out, so the refusal can say how long the wait still is.
    /// </summary>
    // Called every frame from Level_OnUpdate, above the settings gate: a protection that cannot be
    // turned off must not be counted by something that can. It needs no Reset — the count is zero on
    // any frame without a collecting heart, including the first frame of a new Level.
    internal static void Update(Level level) {
        updates = Collecting(level) ? updates + 1 : 0;
    }

    /// <summary>
    /// Why the caller must not split, or null when no heart blocks it.
    /// </summary>
    // Returns the message rather than showing it, for the reason BerryCheck.BlockedMessage gives.
    public static string? BlockedMessage() {
        if (Engine.Scene is not Level level || !Collecting(level)) return null;

        // Never zero: the count outruns the constant if the routine is ever longer than it was
        // measured to be, and "0 more frames" beside a refusal reads as a bug in the mod.
        int remaining = Math.Max(COLLECT_UPDATES - updates, 1);
        return string.Format(Dialog.Get(DialogIds.HeartBlocksSplitId), remaining);
    }
}
