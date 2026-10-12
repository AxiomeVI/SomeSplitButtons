using System;
using System.Collections.Generic;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.Interop;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     Who opened the list: the clock holder it takes, the settings that keep it alive, its title, and
///     the interop action of its split.
/// </summary>
// A class, not a record: openers are compared by identity, and a record's == compares values.
internal sealed class ReentryOpener(ClockHold.Holder holder, Func<bool> alive, string titleId, string action,
    Action<Level> abandoned = null) {
    internal ClockHold.Holder Holder => holder;
    internal Func<bool> Alive => alive;
    internal string TitleId => titleId;
    /// <summary>The <see cref="SplitActions"/> string of the split that opens this list.</summary>
    internal string InteropAction => action;
    /// <summary>Called when the list closes without a pick: Cancel, or its setting switched off.</summary>
    internal Action<Level> Abandoned => abandoned;
}

/// <summary>
///     The list both the Return to Map and the Skip Cutscene splits open, and the load behind it: the
///     clock is held, the list opens, and a pick reloads the level or loads a destination.
/// </summary>
internal static class Reentry {
    private static SomeSplitButtonsModuleSettings Settings => SomeSplitButtonsModule.Settings;

    internal static readonly ReentryOpener ReturnToMap = new(ClockHold.Holder.ReturnToMap,
        () => Settings.Enabled && Settings.ShowReturnToMapSplitButton && Settings.ReturnToMapCheckpointMenu,
        DialogIds.CheckpointMenuHeaderId, SplitActions.ReturnToMap);

    internal static readonly ReentryOpener SkipCutscene = new(ClockHold.Holder.SkipCutscene,
        () => Settings.Enabled && Settings.ShowSkipCutsceneSplitButton && Settings.SkipCutsceneLoadMenu,
        DialogIds.ChapterMenuHeaderId, SplitActions.SkipCutscene, SkipCutsceneRoomTimer.RevealMark);

    // Null while no list is open. One list at a time.
    private static ReentryOpener opener;

    // Set by a pick, whose hold stands until the load's loader thread resets it. The list is no longer
    // there to abandon: without this, a setting switched off on the pick frame reveals the chapter end
    // and says the player stayed, after the load has started.
    private static bool picked;

    // ⚠️ Read by name by the test fixtures.
    private static bool holding => opener != null && ClockHold.IsHeldBy(opener.Holder);

    /// <summary>Whether the open list holds the clock. Read by the test probe.</summary>
    internal static bool Holding => holding;

    /// <summary>
    ///     Holds the clock, pauses the level and opens the list, unless one is already open. False when
    ///     it opened none.
    /// </summary>
    internal static bool Begin(Level level, List<(string Key, string Label)> rows, ReentryOpener who) {
        if (opener != null) return false;
        opener = who;
        ClockHold.Take(who.Holder);
        level.Paused = true;
        // Vanilla's pause effects, only when none run: the Return to Map list opens after its
        // confirmation prompt, and StartPauseEffects plays a sound.
        if (Level.PauseSnapshot == null) level.StartPauseEffects();

        ReturnToMapCheckpointMenu menu = new(rows, Load, Cancel, who.TitleId);
        level.Add(menu);
        // The list is added from the mod's post-orig hook, so without this it first updates on the
        // following frame. Same nudge ReturnToMapTimer.Press gives the confirm prompt.
        level.OnEndOfFrame += () => level.Entities.UpdateLists();
        return true;
    }

    /// <summary>Lets the list and its hold go once the feature that opened them is switched off.</summary>
    // ⚠️ Called from outside every settings gate: a hold nobody releases freezes the chapter clock,
    // Session.Time and SaveData.AddTime for the rest of the Level. ClockHold is static, so a hold left
    // standing also outlives a load.
    internal static void UpdateHold(Level level) {
        // Read once: a loader-thread Reset can null the field between two reads.
        ReentryOpener current = opener;
        if (current != null && !picked && ClockHold.IsHeldBy(current.Holder) && !current.Alive()) {
            current.Abandoned?.Invoke(level);
            Reset(current);
            SplitEvents.Emit(current.InteropAction, SplitStages.Stayed);
        }
    }

    /// <summary>Tears the list down and releases the clock, if <paramref name="who"/> opened it.</summary>
    // ⚠️ Reachable from Level_OnLoadingThread, on the loader's background thread. A LevelLoader built
    // while a Level is still the scene (`console load`) runs it with that Level current. So it emits
    // nothing, which is also right: a level load or a state load is its own outcome for the split
    // behind the list. The opener goes before the list is removed, so that a removal the main thread
    // processes at once finds nothing held and says nothing either.
    internal static void Reset(ReentryOpener who) {
        if (opener != who) return;
        bool wasHolding = holding;
        ClockHold.Release(who.Holder);
        opener = null;
        picked = false;
        if (wasHolding && Engine.Scene is Level level) {
            foreach (ReturnToMapCheckpointMenu menu in level.Entities.FindAll<ReturnToMapCheckpointMenu>()) {
                menu.RemoveSelf();
            }
            level.Paused = false;
            level.EndPauseEffects();
        }
    }

    /// <summary>Tears down whichever list is open, emitting nothing: a state load is about to replace the level.</summary>
    internal static void Abort() {
        ReentryOpener current = opener;
        if (current != null) Reset(current);
    }

    /// <summary>Builds the session for the chosen row, a checkpoint or a destination, and loads it.</summary>
    private static void Load(string checkpointKey) {
        if (Engine.Scene is not Level level) return;
        // Read once: a loader-thread Reset can null the field.
        ReentryOpener loading = opener;
        picked = true;

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
        if (loading == SkipCutscene && level.Session.Area.ID == SkipCutsceneTimer.PROLOGUE_AREA_ID) level.RegisterAreaComplete();

        // Vanilla's Return to Map runs these; a heart's collect sound stops through one.
        foreach (LevelEndingHook hook in level.Tracker.GetComponents<LevelEndingHook>()) hook.OnEnd?.Invoke();
        PausedWait.ResetForExit();

        // False: the split already advanced the room, and the arrival must not add one of no length.
        if (!SpeedrunToolRecords.KeepRunGoing()) CheckpointArrival.Expect();
        LoadChain.Start(next);
        if (loading != null) SplitEvents.Emit(loading.InteropAction, SplitStages.Loading);

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
            // The constructor sets FirstLevel, so without this Level.Reload reads the checkpoint room
            // as a chapter's untouched first room: a death there would zero the Time and Deaths
            // carried above and restart the game's timer. Its only reader in the game is that check.
            HitCheckpoint = true,
        };
    }

    // ⚠️ Not Reset() — Reset never sets unpauseTimer, so a cancel through it lets the Back press
    // (bound to Dash by default) bleed into a dash the next frame. CloseMenu is the exit path that
    // already guards this, same as ReturnToMapSplitConfirmMenu.LeaveThePause.
    //
    // Nothing when nothing is held: the picker also reaches here from its own removal, after a
    // Reset or a Load already closed it, and a second CloseMenu plays the unpause sound again.
    //
    // levelGoesOn is false when the scene is ending under the list. That level load is the split's
    // outcome, and the loader's own Reset may have run first or not: no Stayed either way.
    private static void Cancel(bool levelGoesOn) {
        if (!holding) return;
        ReentryOpener closing = opener;
        ClockHold.Release(closing.Holder);
        opener = null;
        if (Engine.Scene is not Level level) return;
        closing.Abandoned?.Invoke(level);
        CloseMenu(level);
        if (levelGoesOn) SplitEvents.Emit(closing.InteropAction, SplitStages.Stayed);
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
