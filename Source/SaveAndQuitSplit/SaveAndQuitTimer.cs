using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.Utils;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
internal static class SaveAndQuitTimer {
    private static readonly SplitCountdown countdown = new(() => SplitTimings.WIPE_FADEOUT_FRAMES);
    private static bool keepTimerStopped = false;

    /// <summary>
    ///     Releases the chapter clock just before SpeedrunTool clones the level, so no saved state
    ///     carries a hold this manager would have let go of.
    /// </summary>
    // Keyed on ownership and run with every setting off, for UpdateHold's reason.
    internal static void ReleaseHoldForSaveState(Level level) {
        if (keepTimerStopped) level.TimerStopped = false;
    }

    /// <summary>
    ///     Disarms the timer, releasing the chapter clock first if this manager is what is holding
    ///     it stopped.
    /// </summary>
    // As well as UpdateHold, not instead of it: this releases at the moment of the reset, and on a
    // level exit there is no next frame for UpdateHold to use. Abandoning the flag outside a Level
    // is safe — the only way to leave one is to replace it, and the next starts with it false.
    internal static void Reset() {
        if (keepTimerStopped && Engine.Scene is Level level) {
            level.TimerStopped = false;
        }
        keepTimerStopped = false;
        countdown.Reset();
    }

    /// <summary>
    ///     Vanilla's own test for "the chapter clock may run again", copied from the
    ///     <c>TimerStarted</c> latch in <c>Level.UpdateTime</c>.
    /// </summary>
    private static bool ClockWouldRestart(Level level) {
        if (level.InCutscene) return false;
        Player player = level.Tracker.GetEntity<Player>();
        return player != null && !player.TimePaused;
    }

    /// <summary>Arms the split, unless a collectible the player would lose refuses it.</summary>
    internal static bool HandleButtonPressed() => countdown.TryArm();

    /// <summary>
    ///     Maintains the chapter-clock hold, and releases it once vanilla would have restarted the
    ///     clock on its own.
    /// </summary>
    // ⚠️ Called from outside every settings gate, because TimerStopped is vanilla's flag and not the
    // mod's. While this holds it, the mod is the only thing that will ever put it back — a button
    // switched off mid-hold would freeze the chapter clock, Session.Time and SaveData.AddTime for
    // the rest of the Level. Only the split-only path arms it; re-entry replaces the Level instead.
    internal static void UpdateHold(Level level) {
        if (!keepTimerStopped) return;

        level.TimerStopped = true;
        if (ClockWouldRestart(level)) {
            keepTimerStopped = false;
            level.TimerStopped = false;
        }
    }

    internal static void Update(Level level) {
        if (!countdown.Tick()) return;

        SkipCutsceneRoomTimer.Split();
        if (SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter) {
            Reenter(level);
        }
        else {
            level.TimerStopped = true;
            keepTimerStopped = true;
        }
    }

    /// <summary>
    ///     What the pause-menu button does: leave the pause, arm the split, and start the fade-out.
    ///     False when the split was refused.
    /// </summary>
    internal static bool Press(Level level) {
        bool armed = HandleButtonPressed();
        // Unpause even when refused: what the refusal counts down only advances while the game
        // runs, so staying paused would stop the wait the message asks the player to wait out.
        level.Unpause();
        // Never fade out unarmed: nothing would ever end the wipe. See HandleButtonPressed.
        if (armed && SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter) BeginFadeOut(level);
        return armed;
    }

    /// <summary>
    ///     What vanilla's menu_pause_savequit handler settles before its wipe, minus the exit. It
    ///     also raises the death counters and fires LevelEndingHook; this does not, since the
    ///     chapter is not ending.
    /// </summary>
    private static void BeginFadeOut(Level level) {
        // A one-shot reset, not the sustained modification TimeRateModifier arbitrates: the
        // fade-out should run at normal speed and not a seeker's.
#pragma warning disable CS0618
        Engine.TimeRate = 1f;
#pragma warning restore CS0618
        // Audio.SetMusic returns early when the requested track is already playing, so without
        // this the re-entry's Session.Audio.Apply leaves the music running instead of restarting it.
        Audio.SetMusic(null);
        Audio.BusStopAll(Buses.GAMEPLAY, immediate: true);
        // No OnComplete: this class owns the frame the scene changes on. See Reenter.
        level.DoScreenWipe(wipeIn: false);
    }

    /// <summary>
    ///     Re-enters the room the way a chapter resumed after a Save and Quit does: through
    ///     <c>LevelLoader</c>, which rebuilds the level and respawns at <c>Session.RespawnPoint</c>.
    /// </summary>
    // Must stay on the same frame as the split above, and after it: RoomTimerManager reads the
    // Level it is told about, and this scene is gone by the next frame. No hold needed — a Level
    // built by LevelLoader starts with TimerStarted false.
    private static void Reenter(Level level) {
        Engine.Scene = new LevelLoader(level.Session);
    }
}
