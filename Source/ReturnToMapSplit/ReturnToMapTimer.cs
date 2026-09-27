using System.Collections.Generic;
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

    internal static bool Armed => countdown.Armed;

    // The countdown only. Its hold stands while the picker — a pause — is open, and a player cannot
    // take a save state while paused.
    internal static object Snapshot() => countdown.State;

    internal static void Restore(object snapshot) => countdown.State = ((bool, int)) snapshot;

    /// <summary>
    ///     Arms the split and pauses the level for its wait, unless a collectible the player would
    ///     lose refuses it.
    /// </summary>
    internal static bool HandleButtonPressed() {
        if (!countdown.TryArm()) return false;
        // TryArm arms nothing outside a Level.
        PausedWait.Begin((Level) Engine.Scene);
        return true;
    }

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

        Logger.Info(nameof(SomeSplitButtonsModule), $"ReturnToMap split in {level.Session.Level} on frame {Engine.FrameCounter}");
        SkipCutsceneRoomTimer.Split();

        // Nothing to pick when the save has reached no checkpoint here, so the player carries on,
        // clock running.
        List<(string Key, string Label)> rows = SomeSplitButtonsModule.Settings.ReturnToMapCheckpointMenu
            ? CheckpointList.ForArea(level.Session.Area)
            : null;
        if (rows == null || rows.Count == 0) {
            PausedWait.End(level);
            return;
        }

        // The picker is a pause too, so the wait's pause runs straight into it.
        ReturnToMapReentry.Begin(level, rows);
    }
}
