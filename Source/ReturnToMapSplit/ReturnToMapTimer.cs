using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.Utils;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

internal static class ReturnToMapTimer {
    private static readonly SplitCountdown countdown = new(() => SplitTimings.WIPE_FADEOUT_FRAMES);

    internal static void Reset() {
        ReturnToMapReentry.Reset();
        countdown.Reset();
    }

    /// <summary>Arms the split, unless a collectible the player would lose refuses it.</summary>
    internal static bool HandleButtonPressed() => countdown.TryArm();

    /// <summary>
    ///     What the pause-menu button does: open the confirmation prompt over the pause menu.
    /// </summary>
    // Vanilla Return to Map asks before leaving, so the split button does too. Nothing is armed
    // here — the prompt's Yes calls HandleButtonPressed, and the prompt owns the interop sequence's
    // terminal stage from the moment it opens.
    internal static void Press(Level level, TextMenu pauseMenu) {
        ReturnToMapSplitConfirmMenu confirmMenu = new(level, pauseMenu);
        // Vanilla's Return to Map button clears this before calling GiveUp; the confirmation menu
        // puts it back when it closes. It gates the journal-button HUD hide in Level.Update.
        level.PauseMainMenuOpen = false;
        pauseMenu.Focused = false;
        pauseMenu.Alpha = 0f;
        level.Add(confirmMenu);
        level.OnEndOfFrame += () => level.Entities.UpdateLists();
    }

    internal static void Update(Level level) {
        if (!countdown.Tick()) return;

        SkipCutsceneRoomTimer.Split();
        if (!SomeSplitButtonsModule.Settings.ReturnToMapCheckpointMenu) return;

        // The collect protection ran at arm time, and 31 frames of live gameplay have passed since.
        // The split alone never left the level, so a collectible picked up in that window cost
        // nothing; the reload destroys it. Re-ask before opening, and keep the split either way — it
        // has already fired and cannot be unfired.
        if (CollectCheck.BlockedMessage() is string blocked) {
            SomeSplitButtonsModule.PopupMessage(blocked);
            return;
        }

        ReturnToMapReentry.Begin(level);
    }
}
