using System;
using System.Collections.Generic;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     Who opened the list: the clock holder it takes, the settings that keep it alive, and its title.
/// </summary>
// A class, not a record: openers are compared by identity, and a record's == compares values.
internal sealed class ReentryOpener {
    internal ClockHold.Holder Holder { get; }
    internal Func<bool> Alive { get; }
    internal string TitleId { get; }
    internal ReentryOpener(ClockHold.Holder holder, Func<bool> alive, string titleId) {
        Holder = holder;
        Alive = alive;
        TitleId = titleId;
    }
}

/// <summary>
///     The list both the Return to Map and the Skip Cutscene splits open, and the load behind it: the
///     clock is held, the list opens, and a pick reloads the level or loads a destination.
/// </summary>
internal static class Reentry {
    private static SomeSplitButtonsModuleSettings Settings => SomeSplitButtonsModule.Settings;

    internal static readonly ReentryOpener ReturnToMap = new(ClockHold.Holder.ReturnToMap,
        () => Settings.Enabled && Settings.ShowReturnToMapSplitButton && Settings.ReturnToMapCheckpointMenu,
        DialogIds.CheckpointMenuHeaderId);

    internal static readonly ReentryOpener SkipCutscene = new(ClockHold.Holder.SkipCutscene,
        () => Settings.Enabled && Settings.ShowSkipCutsceneSplitButton && Settings.SkipCutsceneLoadMenu,
        DialogIds.ChapterMenuHeaderId);

    // Null while no list is open. One list at a time.
    private static ReentryOpener opener;

    // A view of ClockHold, named as the expectation files have always read it.
    private static bool holding => opener != null && ClockHold.IsHeldBy(opener.Holder);

    /// <summary>Whether the open list holds the clock. Read by the test probe.</summary>
    internal static bool Holding => holding;

    /// <summary>Holds the clock, pauses the level and opens the list, unless one is already open.</summary>
    internal static bool Begin(Level level, List<(string Key, string Label)> rows, ReentryOpener who) {
        if (opener != null) return false;
        opener = who;
        ClockHold.Take(who.Holder);
        level.Paused = true;
        StartPause(level);

        ReturnToMapCheckpointMenu menu = new(rows, Load, Cancel, who.TitleId);
        level.Add(menu);
        // The list is added from the mod's post-orig hook, so without this it first updates on the
        // following frame. Same nudge ReturnToMapTimer.Press gives the confirm prompt.
        level.OnEndOfFrame += () => level.Entities.UpdateLists();
        return true;
    }

    // Vanilla's pause effects, private on Level (the publicizer reaches them). Only when none run: the
    // Return to Map list opens after its confirmation prompt, and StartPauseEffects plays a sound.
    private static void StartPause(Level level) {
        if (Level.PauseSnapshot == null) level.StartPauseEffects();
    }

    /// <summary>Lets the list and its hold go once the feature that opened them is switched off.</summary>
    // ⚠️ Called from outside every settings gate: a hold nobody releases freezes the chapter clock,
    // Session.Time and SaveData.AddTime for the rest of the Level. ClockHold is static, so a hold left
    // standing also outlives a load.
    internal static void UpdateHold(Level level) {
        // Read once: a loader-thread Reset can null the field between two reads.
        ReentryOpener current = opener;
        if (current != null && ClockHold.IsHeldBy(current.Holder) && !current.Alive()) Reset(current);
    }

    /// <summary>Tears the list down and releases the clock, if <paramref name="who"/> opened it.</summary>
    // ⚠️ Reachable from Level_OnLoadingThread, on the loader's background thread. A LevelLoader built
    // while a Level is still the scene (`console load`) runs it with that Level current.
    internal static void Reset(ReentryOpener who) {
        if (opener != who) return;
        if (holding && Engine.Scene is Level level) {
            foreach (ReturnToMapCheckpointMenu menu in level.Entities.FindAll<ReturnToMapCheckpointMenu>()) {
                menu.RemoveSelf();
            }
            level.Paused = false;
            level.EndPauseEffects();
        }
        ClockHold.Release(who.Holder);
        opener = null;
    }

    /// <summary>Builds the session for the chosen row, a checkpoint or a destination, and loads it.</summary>
    private static void Load(string checkpointKey) {
        if (Engine.Scene is not Level level) return;

        Session outgoing = level.Session;
        // A destination carries nothing but the C-side postcard's flag: it is another area, and
        // SaveData.RegisterCompletion writes a StartedFromBeginning session's Time as that area's best.
        // SpeedrunTool's room timer carries the chain on its own.
        Session next = Destinations.TryParseKey(checkpointKey, out int id, out AreaMode mode)
            ? new Session(new AreaKey(id, mode)) { UnlockedCSide = outgoing.UnlockedCSide }
            : BuildSession(outgoing, checkpointKey);

        // ⚠️ The hold stays until the load's Level_OnLoadingThread lets go of it. SpeedrunTool's room
        // timer runs after this callback on the outgoing level, and released here it added this
        // frame to the next room. The list's removal cannot release it early: Choose has finished
        // the list, so its Cancel does nothing.
        CloseMenu(level);

        // The Prologue registers its completion at the very end of its ending, which the pick leaves
        // before; chapters 1 to 7 already did as theirs began. Keyed on the ID, not on Completed, which
        // is false wherever a split fires outside an ending.
        if (opener == SkipCutscene && level.Session.Area.ID == 0) level.RegisterAreaComplete();

        // Vanilla's Return to Map runs these; a heart's collect sound stops through one.
        foreach (LevelEndingHook hook in level.Tracker.GetComponents<LevelEndingHook>()) hook.OnEnd?.Invoke();

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

        // The pick carries the run on: SpeedrunTool's end-of-run record, written if the level was shown
        // completed, is taken back. Its arrival split is then let through only where the last record is a
        // segment of its own, as a chapter's end recorded on a completed level is, which splits without
        // advancing the room. Otherwise the split already advanced it, or the record is a repeat the next
        // room may overwrite, and the arrival would add a room of no length.
        if (!SpeedrunToolRecords.KeepRunGoing()) CheckpointArrival.Expect();
        LoadChain.Start(next);

        // The way the chapter panel enters, raising Everest's Level.Enter: a checkpoint session is not
        // StartedFromBeginning, so nothing shows before it; a destination's is, so its postcard, title
        // card or vignette shows, as after a real Return to Map.
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
        return new Session(outgoing.Area, checkpointKey) {
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
        ClockHold.Release(opener.Holder);
        opener = null;
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
        level.EndPauseEffects();
        Audio.Play(SFX.ui_game_unpause);
        level.unpauseTimer = 0.15f;
    }
}
