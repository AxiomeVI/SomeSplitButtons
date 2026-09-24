#nullable enable
using System;
using System.Collections.Generic;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>
///     Refuses a split while a crystal heart is still being written to the save.
/// </summary>
// A heart is banked at the end of its collect routine, not when it is touched, and this mod's
// fade-out runs live — so a split during the routine leaves with the heart unwritten and loses it.
// Cannot be switched off, unlike the berry check: a split before the bank is not a faster strat but
// one the game does not allow.
internal static class HeartCheck {
    /// <summary>The update <c>RegisterAsCollected</c> lands on, from the first collecting frame.</summary>
    // Measured in game, and not the 135 that reading HeartGem.CollectRoutine gives: it opens on
    // Celeste.Freeze(0.2f), and a freeze stops the scene, so 12 of those frames update nothing.
    //
    // Display only — the refusal is decided by the flags below, so a drift here costs a wrong number
    // in a message and never a wrong decision.
    private const int COLLECT_UPDATES = 124;

    private static int updates;

    /// <summary>True while a heart is collecting and has not been written yet.</summary>
    // HeartGem.collected is set on the frame of the touch and level.Frozen on the line beside
    // RegisterAsCollected, so the pair brackets exactly that window and neither can drift.
    // Session.HeartGem is the wrong third test: on a B or C side the routine also completes the
    // chapter, and a session already carrying the heart would read as safe.
    //
    // Every heart in the room, because Tracker.GetEntity<HeartGem>() returns the first *tracked*
    // instance and not the first matching one. Heart entities that do not derive from HeartGem,
    // such as CollabUtils mini hearts, are invisible here.
    private static bool Collecting(Level level) {
        if (level.Frozen) return false;

        List<Entity> hearts = level.Tracker.GetEntities<HeartGem>();
        for (int i = 0; i < hearts.Count; i++) {
            if (hearts[i] is HeartGem {IsFake: false, collected: true}) return true;
        }
        return false;
    }

    /// <summary>Counts the routine out, so the refusal can say how long the wait still is.</summary>
    // Called above the settings gate: a protection that cannot be turned off must not be counted by
    // something that can. Needs no Reset, since the count is zero on any frame without a heart.
    internal static void Update(Level level) {
        updates = Collecting(level) ? updates + 1 : 0;
    }

    /// <summary>Why the caller must not split, or null when no heart blocks it.</summary>
    internal static string? BlockedMessage() {
        if (Engine.Scene is not Level level || !Collecting(level)) return null;

        // Never zero: the count outruns the constant if the routine is ever longer than it was
        // measured to be, and "0 more frames" beside a refusal reads as a bug.
        int remaining = Math.Max(COLLECT_UPDATES - updates, 1);
        return string.Format(Dialog.Get(DialogIds.HeartBlocksSplitId), remaining);
    }
}
