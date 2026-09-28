using System;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.Utils;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
internal static class SaveAndQuitTimer {
    private static readonly SplitCountdown countdown = new(() => SplitTimings.WIPE_FADEOUT_FRAMES);
    private static bool fadingOut = false;

    /// <summary>The fade-out this timer started, to tell it from any other wipe.</summary>
    private static ScreenWipe fadeOut;
    // A view of ClockHold, named as the expectation files have always read it.
    private static bool keepTimerStopped => ClockHold.IsHeldBy(ClockHold.Holder.SaveAndQuit);

    /// <summary>Disarms the timer and lets go of the chapter clock.</summary>
    // As well as UpdateHold, not instead of it: on a level exit there is no next frame for
    // UpdateHold to use, and a hold left standing would stop the next level's clock.
    internal static void Reset() {
        ClockHold.Release(ClockHold.Holder.SaveAndQuit);
        countdown.Reset();
        fadingOut = false;
        fadeOut = null;
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

    internal static bool Armed => countdown.Armed;

    private sealed record Saved((bool Armed, int Counter) Countdown, bool Held, bool FadingOut);

    internal static object Snapshot() => new Saved(countdown.State, keepTimerStopped, fadingOut);

    internal static void Restore(object snapshot) {
        Saved saved = (Saved) snapshot;
        countdown.State = saved.Countdown;
        ClockHold.Restore(ClockHold.Holder.SaveAndQuit, saved.Held);
        fadingOut = saved.FadingOut;
    }

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

    /// <summary>Releases the chapter-clock hold once vanilla would have restarted the clock.</summary>
    // ⚠️ Called from outside every settings gate: only this releases the hold, and a button switched
    // off mid-hold would otherwise freeze the chapter clock, Session.Time and SaveData.AddTime for the
    // rest of the Level. Re-entry takes it for the split frame only: the load replaces the Level, and
    // Reset lets go of it there.
    internal static void UpdateHold(Level level) {
        if (keepTimerStopped && ClockWouldRestart(level)) ClockHold.Release(ClockHold.Holder.SaveAndQuit);
    }

    internal static void Update(Level level) {
        if (fadingOut) KeepFadeOutInStep(level);

        if (!countdown.Tick()) return;

        fadingOut = false;
        Logger.Info(nameof(SomeSplitButtonsModule), $"SaveAndQuit split in {level.Session.Level} on frame {Engine.FrameCounter}");
        SkipCutsceneRoomTimer.Split();
        // On both paths, and on this frame: SpeedrunTool's room timer runs after this on the same
        // frame, and would add it to the next room.
        ClockHold.Take(ClockHold.Holder.SaveAndQuit);
        if (SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter) {
            Reenter(level);
        }
        else {
            PausedWait.End(level);
        }
    }

    /// <summary>
    ///     What the pause-menu button does: arm the split, close the menu with the level still
    ///     paused, and start the fade-out. False when the split was refused.
    /// </summary>
    // A refusal unpauses: what it counts down only advances while the game runs, so staying paused
    // would stop the wait the message asks the player to wait out. It never fades out either,
    // since nothing would ever end the wipe (see HandleButtonPressed).
    internal static bool Press(Level level, TextMenu pauseMenu) {
        if (!HandleButtonPressed()) {
            level.Unpause();
            return false;
        }
        // Not level.Unpause(): the level stays paused through the wait (PausedWait).
        pauseMenu?.RemoveSelf();
        if (SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter) BeginFadeOut(level);
        return true;
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
        fadeOut = level.Wipe;
        fadingOut = true;
    }

    /// <summary>Sets the fade-out to where the countdown is, and puts it back if it is gone.</summary>
    // Derived from the countdown rather than left to run on its own, because two things move it
    // off vanilla's pace: SpeedrunTool's freeze after a save, which updates the wipe while the
    // countdown waits, so the fade finishes and removes itself early; and a load, which replaces it
    // with SpeedrunTool's own wipe-in. Called only on frames the level ran, when SpeedrunTool is done
    // with any wipe of its own. Another wipe — a death's — is left alone.
    private static void KeepFadeOutInStep(Level level) {
        if (level.Wipe == null) {
            level.DoScreenWipe(wipeIn: false);
            fadeOut = level.Wipe;
        }
        if (fadeOut == null || level.Wipe != fadeOut) return;
        // ScreenWipe.Update's own step, once per counted frame: the wipe is black one update before
        // the split, as vanilla's is one before its OnComplete.
        fadeOut.Percent = Math.Min(1f, countdown.State.Counter * Engine.RawDeltaTime / fadeOut.Duration);
    }

    /// <summary>
    ///     Re-enters the room the way a chapter resumed after a Save and Quit does: through
    ///     <c>LevelEnter.Go(session, fromSaveData: true)</c>, which raises Everest's Level.Enter and
    ///     then rebuilds the level, respawning at <c>Session.RespawnPoint</c>.
    /// </summary>
    // Must stay on the same frame as the split above, and after it: RoomTimerManager reads the
    // Level it is told about, and this scene is gone by the next frame. The hold taken with the
    // split covers the rest of this frame, and the load's Level_OnLoadingThread lets go of it; the
    // Level LevelLoader builds starts with TimerStarted false. fromSaveData keeps LevelEnter's
    // postcards and remix card away, as for a real resume.
    //
    // Through LoadChain, as a pick from the list is: a save state kept from another chapter or side by
    // an earlier pick would otherwise be cleared by SpeedrunTool on this scene switch, its room timer
    // with it.
    private static void Reenter(Level level) {
        LoadChain.Start(level.Session);
        LevelEnter.Go(level.Session, fromSaveData: true);
    }
}
