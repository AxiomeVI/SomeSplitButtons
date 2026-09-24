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

    /// <summary>Builds the session for the chosen checkpoint and reloads the level into it.</summary>
    private static void Load(string checkpointKey) {
        if (Engine.Scene is not Level level) return;

        Session outgoing = level.Session;
        Session next = BuildSession(outgoing, checkpointKey);

        CloseMenu(level);
        holding = false;

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

        // ⚠️ Armed from inside an update, not a console command — ArrivalSplitSwallow counts
        // Level_OnUpdate calls from here, and a command runs between updates, off that count.
        ArrivalSplitSwallow.Arm();

        Engine.Scene = new LevelLoader(next);
    }

    /// <summary>
    ///     A checkpoint start with the attempt carried onto it: the constructor settles what the
    ///     checkpoint owns, and the fields below are what one attempt keeps across the load.
    /// </summary>
    // ⚠️ Copies, not references. These are HashSets and a bool[]; assigning them would leave the new
    // session sharing mutable state with the abandoned one.
    private static Session BuildSession(Session outgoing, string checkpointKey) {
        Session next = new(outgoing.Area, CheckpointList.StripAreaPrefix(checkpointKey)) {
            Time = outgoing.Time,
            Deaths = outgoing.Deaths,
            Dashes = outgoing.Dashes,
            Cassette = outgoing.Cassette,
            HeartGem = outgoing.HeartGem,
            GrabbedGolden = outgoing.GrabbedGolden,
            UnlockedCSide = outgoing.UnlockedCSide,
            Strawberries = new(outgoing.Strawberries),
            DoNotLoad = new(outgoing.DoNotLoad),
            Keys = new(outgoing.Keys),
        };
        // Both sides guarded: the constructor always sizes next.SummitGems today, but nothing here
        // may lean on that staying true, and outgoing.SummitGems is null whenever that save has
        // never touched a summit chapter.
        if (outgoing.SummitGems != null && next.SummitGems != null) {
            outgoing.SummitGems.CopyTo(next.SummitGems, 0);
        }
        return next;
    }

    // ⚠️ Not Reset() — Reset never sets unpauseTimer, so a cancel through it lets the Back press
    // (bound to Dash by default) bleed into a dash the next frame. CloseMenu is the exit path that
    // already guards this, same as ReturnToMapSplitConfirmMenu.LeaveThePause.
    private static void Cancel() {
        if (Engine.Scene is not Level level) return;
        level.TimerStopped = false;
        holding = false;
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
