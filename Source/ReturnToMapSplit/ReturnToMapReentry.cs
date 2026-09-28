using System.Collections.Generic;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.Splits;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     What happens after the Return to Map split when the checkpoint picker is enabled: the clock
///     is held, the picker opens, and picking reloads the level at the chosen checkpoint.
/// </summary>
internal static class ReturnToMapReentry {
    // A view of ClockHold, named as the expectation files have always read it.
    private static bool holding => ClockHold.IsHeldBy(ClockHold.Holder.ReturnToMap);

    /// <summary>Whether this manager currently holds the clock. Read by the test probe.</summary>
    internal static bool Holding => holding;

    /// <summary>Holds the clock, pauses the level and opens the picker.</summary>
    internal static void Begin(Level level, List<(string Key, string Label)> rows) {
        ClockHold.Take(ClockHold.Holder.ReturnToMap);
        level.Paused = true;

        ReturnToMapCheckpointMenu menu = new(rows, Load, Cancel);
        level.Add(menu);
        // The picker is added from the mod's post-orig hook, so without this it first updates on the
        // following frame. Same nudge ReturnToMapTimer.Press gives the confirm prompt.
        level.OnEndOfFrame += () => level.Entities.UpdateLists();
    }

    /// <summary>Lets the picker and its hold go once the feature behind them is switched off.</summary>
    // ⚠️ Called from outside every settings gate: a hold nobody releases freezes the chapter clock,
    // Session.Time and SaveData.AddTime for the rest of the Level.
    internal static void UpdateHold(Level level) {
        if (!holding) return;

        if (!SomeSplitButtonsModule.Settings.Enabled
            || !SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton
            || !SomeSplitButtonsModule.Settings.ReturnToMapCheckpointMenu) {
            Reset();
        }
    }

    /// <summary>Tears the picker down and releases the clock.</summary>
    // ⚠️ Reachable from Level_OnLoadingThread, which runs on the loader's background thread. Guard
    // on the scene being a Level rather than relying on it: there is no Level to touch at that
    // moment, and the guard is what says so out loud.
    internal static void Reset() {
        if (holding && Engine.Scene is Level level) {
            foreach (ReturnToMapCheckpointMenu menu in level.Entities.FindAll<ReturnToMapCheckpointMenu>()) {
                menu.RemoveSelf();
            }
            level.Paused = false;
        }
        ClockHold.Release(ClockHold.Holder.ReturnToMap);
    }

    /// <summary>Builds the session for the chosen checkpoint and reloads the level into it.</summary>
    private static void Load(string checkpointKey) {
        if (Engine.Scene is not Level level) return;

        Session outgoing = level.Session;
        // The B-side carries nothing: it is another chapter, and SaveData.RegisterCompletion writes a
        // StartedFromBeginning session's Time as that chapter's best. SpeedrunTool's room timer
        // carries the chain on its own.
        Session next = checkpointKey == CassetteWindow.BSideKey
            ? new Session(new AreaKey(outgoing.Area.ID, AreaMode.BSide))
            : BuildSession(outgoing, checkpointKey);

        // ⚠️ The hold stays until the load's Level_OnLoadingThread lets go of it. SpeedrunTool's room
        // timer runs after this callback on the outgoing level, and released here it added this
        // frame to the next room. The picker's removal cannot release it early: Choose has finished
        // the picker, so its Cancel does nothing.
        CloseMenu(level);

        // A one-shot reset, not a sustained modification: LoadLevel does not put TimeRate back, so
        // splitting during a seeker or Oshiro slowdown would start the new level at reduced speed.
        // Vanilla's own Return to Map confirm does the same before leaving.
#pragma warning disable CS0618
        Engine.TimeRate = 1f;
#pragma warning restore CS0618

        // Audio.SetMusic returns early when the requested track is already playing, so without this
        // the load's Session.Audio.Apply leaves the music running instead of restarting it. A
        // checkpoint in the same chapter usually carries the same event, so this is the common case
        // and not the corner.
        Audio.SetMusic(null);
        Audio.BusStopAll(Buses.GAMEPLAY, immediate: true);

        CheckpointArrival.Expect();
        if (checkpointKey == CassetteWindow.BSideKey) SceneSwitchClear.Suspend(next);

        // The way a checkpoint picked from the chapter panel is entered, raising Everest's
        // Level.Enter. A checkpoint session is not StartedFromBeginning, so no postcard shows. The
        // B-side's is, so its title card shows, as it does after a real Return to Map.
        LevelEnter.Go(next, fromSaveData: false);
    }

    /// <summary>
    ///     A checkpoint start with the attempt carried onto it: the constructor settles what the
    ///     checkpoint owns, and the fields below are what one attempt keeps across the load.
    /// </summary>
    // ⚠️ Copies, not references. These are HashSets and a bool[]; assigning them would leave the new
    // session sharing mutable state with the abandoned one.
    // Everything else starts fresh, as after a real Return to Map: collected berries, hearts and
    // cassettes come back as ghosts (SaveData already has them), keys return to their spot and
    // opened doors close, because all of that is read from the session, not the save.
    private static Session BuildSession(Session outgoing, string checkpointKey) {
        return new Session(outgoing.Area, CheckpointList.StripAreaPrefix(checkpointKey)) {
            Time = outgoing.Time,
            Deaths = outgoing.Deaths,
            Dashes = outgoing.Dashes,
            // Only drives the C-side postcard, which vanilla shows on the way back to the overworld.
            // The re-entry never goes there, so dropping this would lose the postcard for good.
            UnlockedCSide = outgoing.UnlockedCSide,
        };
    }

    // ⚠️ Not Reset() — Reset never sets unpauseTimer, so a cancel through it lets the Back press
    // (bound to Dash by default) bleed into a dash the next frame. CloseMenu is the exit path that
    // already guards this, same as ReturnToMapSplitConfirmMenu.LeaveThePause.
    //
    // Nothing when nothing is held: the picker also reaches here from its own removal, after a
    // Reset or a Load already closed it, and a second CloseMenu plays the unpause sound again.
    private static void Cancel() {
        if (!holding) return;
        ClockHold.Release(ClockHold.Holder.ReturnToMap);
        if (Engine.Scene is not Level level) return;
        CloseMenu(level);
    }

    /// <summary>
    ///     Closes the picker and the pause under it, and hands the level back to the player.
    /// </summary>
    // ⚠️ Not level.Unpause(). That hands the first TextMenu in the scene — which is this picker — to
    // CloseAndRun(Everest.SaveSettings()), so it stays in the scene until a settings file has been
    // written. ReturnToMapSplitConfirmMenu.LeaveThePause documents the same hazard for the confirm
    // prompt, and vanilla's own Return to Map prompt hand-rolls these lines for the same reason.
    private static void CloseMenu(Level level) {
        foreach (ReturnToMapCheckpointMenu menu in level.Entities.FindAll<ReturnToMapCheckpointMenu>()) {
            menu.Close();
            menu.RemoveSelf();
        }
        level.PauseMainMenuOpen = false;
        level.Paused = false;
        Audio.Play(SFX.ui_game_unpause);
        level.unpauseTimer = 0.15f;
    }
}
