using System;
using Celeste.Mod.SomeSplitButtons.Utils;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     The frame countdown a split button runs between its press and the room timer being told.
/// </summary>
// A Func<int> rather than an int, because Skip Cutscene's total depends on the chapter.
internal sealed class SplitCountdown(Func<int> frames) {
    private bool armed;
    private int counter;

    /// <summary>Whether a press is waiting to become a split.</summary>
    internal bool Armed => armed;

    internal void Arm() {
        armed = true;
        counter = 0;
    }

    internal void Reset() {
        armed = false;
        counter = 0;
    }

    /// <summary>
    ///     Arms the countdown unless a collectible the player would lose says otherwise. False when
    ///     nothing was armed, which is the caller's signal not to start a fade-out that has no
    ///     completion of its own and would leave the screen black.
    /// </summary>
    internal bool TryArm() {
        if (Engine.Scene is not Level) return false;
        if (CollectCheck.BlockedMessage() is string blocked) {
            SomeSplitButtonsModule.PopupMessage(blocked);
            return false;
        }

        Arm();
        return true;
    }

    /// <summary>Advances one update, and returns true on the single update the split fires.</summary>
    // Called after orig(self) in Level.Update, and fires where the count *passes* the total rather
    // than reaches it: press on N, split on N + frames. Both the call site and the comparison are
    // measured — moving either shifts every split by a frame.
    //
    // It does not stop for a pause: Level.Update runs while paused and this is called after orig, so
    // re-pausing inside the wait keeps it going. A button that promises 31 frames promises 31.
    internal bool Tick() {
        if (!armed) return false;

        counter++;
        if (counter <= frames()) return false;

        Reset();
        return true;
    }
}
