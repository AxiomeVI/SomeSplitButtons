using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     What happens after the Return to Map split when the checkpoint picker is enabled: the clock
///     is held, the picker opens, and picking reloads the level at the chosen checkpoint.
/// </summary>
internal static class ReturnToMapReentry {
    private static bool holding;

    /// <summary>Whether this manager currently holds the clock. Read by the test probe.</summary>
    internal static bool Holding => holding;

    /// <summary>Holds the clock, pauses the level and opens the picker.</summary>
    internal static void Begin(Level level) {
        holding = true;
        level.TimerStopped = true;
        level.Paused = true;

        ReturnToMapCheckpointMenu menu = new(level, Load, Cancel);
        level.Add(menu);
        // The picker is added from the mod's post-orig hook, so without this it first updates on the
        // following frame. Same nudge ReturnToMapTimer.Press gives the confirm prompt.
        level.OnEndOfFrame += () => level.Entities.UpdateLists();
    }

    /// <summary>
    ///     Maintains the hold frame by frame, so nothing else can clear it while the picker is open.
    /// </summary>
    // ⚠️ Called from outside every settings gate, because TimerStopped is vanilla's flag and not the
    // mod's. ssb_set_show writes a Show… setting directly, bypassing SplitFeature.Toggle and the
    // Reset() it calls — so a feature switched off mid-hold is put back here instead. Left unhandled,
    // it would freeze the chapter clock, Session.Time and SaveData.AddTime for the rest of the Level.
    internal static void UpdateHold(Level level) {
        if (!holding) return;

        if (!SomeSplitButtonsModule.Settings.Enabled
            || !SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton
            || !SomeSplitButtonsModule.Settings.ReturnToMapCheckpointMenu) {
            Reset();
            return;
        }

        level.TimerStopped = true;
    }

    /// <summary>Releases the clock just before SpeedrunTool clones the level.</summary>
    // Keyed on ownership and run with every setting off, for UpdateHold's reason. The manager's own
    // flag is deliberately left set, so UpdateHold re-asserts on the live level the same frame —
    // the shape SaveAndQuitTimer.ReleaseHoldForSaveState already uses.
    internal static void ReleaseHoldForSaveState(Level level) {
        if (holding) level.TimerStopped = false;
    }

    /// <summary>Tears the picker down and releases the clock.</summary>
    // ⚠️ Reachable from Level_OnLoadingThread, which runs on the loader's background thread. Guard
    // on the scene being a Level rather than relying on it: there is no Level to touch at that
    // moment, and the guard is what says so out loud.
    internal static void Reset() {
        if (holding && Engine.Scene is Level level) {
            level.TimerStopped = false;
            foreach (ReturnToMapCheckpointMenu menu in level.Entities.FindAll<ReturnToMapCheckpointMenu>()) {
                menu.RemoveSelf();
            }
            level.Paused = false;
        }
        holding = false;
    }

    private static void Load(string checkpointKey) {
        // Task 7.
    }

    /// <summary>Backing out of the picker is just letting go of the hold — no load to stage.</summary>
    private static void Cancel() {
        Reset();
    }
}
