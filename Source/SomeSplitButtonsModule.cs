using System;
using Celeste.Mod.CelesteHotkeys;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Celeste.Mod.SomeSplitButtons.Interop;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.UI;
using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.Utils;
using MonoMod.ModInterop;
using MonoMod.RuntimeDetour;
using static Celeste.TextMenuExt;
using FMOD.Studio;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons;

public class SomeSplitButtonsModule : EverestModule {
    public static SomeSplitButtonsModule Instance { get; private set; }

    public override Type SettingsType => typeof(SomeSplitButtonsModuleSettings);
    public static SomeSplitButtonsModuleSettings Settings => (SomeSplitButtonsModuleSettings) Instance._Settings;

    // No SessionType or SaveDataType: registering either with an empty class behind it makes Everest
    // serialise an empty object into every save file and every session. Declare them when there is a
    // field to put in them, not before.
    private object saveLoadInstance = null;

    public SomeSplitButtonsModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(SomeSplitButtonsModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(SomeSplitButtonsModule), LogLevel.Info);
#endif
    }

    public override void Load() {
        Everest.Events.Level.OnExit += Level_OnLevelExit;
        // ⚠️ Ordering is load-bearing. This hook must sit *outside* SpeedrunTool's own Level.Update
        // hook (RoomTimerManager.Timing), so SRT has accumulated the frame by the time the split
        // timers call UpdateTimerState after orig.
        //
        // Said out loud rather than inherited from the everest.yaml dependency making SpeedrunTool
        // load first — which would rest the requirement on something written for another reason, and
        // be silently wrong if SRT were hot-reloaded. The id is the mod name Everest gives a detour,
        // so it must match everest.yaml's Name on both sides.
        using (new DetourConfigContext(
                   new DetourConfig("SomeSplitButtons").WithBefore("SpeedrunTool")).Use()) {
            On.Celeste.Level.Update += Level_OnUpdate;
        }
        Everest.Events.Level.OnBeforeUpdate += Level_OnBeforeUpdate;
        On.Celeste.Level.UpdateTime += ClockHold.Level_OnUpdateTime;
        Everest.Events.LevelLoader.OnLoadingThread += Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
        // ModInterop leaves the delegate fields null when SpeedrunTool does not export
        // SpeedrunTool.SaveLoad, and calling through unchecked throws inside Load() — which makes
        // Everest refuse the whole mod over one missing integration.
        typeof(SplitButtonsInterop).ModInterop();
        typeof(SaveLoadIntegration).ModInterop();
        if (SaveLoadIntegration.RegisterSaveLoadAction != null) {
            saveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
                OnSaveState,
                OnLoadState,
                OnClearState,
                null,
                null,
                null
            );
        }
        else {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool.SaveLoad ModInterop not found — a load state will not put the split timers back as they were saved.");
        }
        // Settings written by older builds can carry Keys.None, which fires on every unmappable key.
        Bindable.Sanitize(Settings);
        // Engine.Update, not Level.Update: the hotkeys arm split buttons for a run that has not
        // started yet, so they have to answer on the overworld and the chapter card too.
        On.Monocle.Engine.Update += Engine_OnUpdate;

        SpeedrunToolHooks.Install();
    }

    /// <summary>Moves the pause-menu handler to the end of the event's invocation list.</summary>
    public override void Initialize() {
        base.Initialize();
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
    }

    public override void Unload() {
        if (settingsUnsaved) SaveSettings();
        settingsUnsaved = false;
        On.Monocle.Engine.Update -= Engine_OnUpdate;
        On.Celeste.Level.Update -= Level_OnUpdate;
        Everest.Events.Level.OnBeforeUpdate -= Level_OnBeforeUpdate;
        On.Celeste.Level.UpdateTime -= ClockHold.Level_OnUpdateTime;
        Everest.Events.LevelLoader.OnLoadingThread -= Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        // Null whenever the registration above was skipped or refused; Unregister is null in exactly
        // the same case, since both come from the same import.
        if (saveLoadInstance != null) {
            SaveLoadIntegration.Unregister?.Invoke(saveLoadInstance);
            saveLoadInstance = null;
        }
        SplitFeatures.ResetAll();
        Everest.Events.Level.OnExit -= Level_OnLevelExit;
        SpeedrunToolHooks.Uninstall();
    }

    /// <summary>Disarms every split timer on level load, whether or not its feature is enabled.</summary>
    // Runs on the level loader's background thread, and it is the only event that fires once per
    // level load and before the Level's first Update — OnLoadLevel fires on every room transition
    // and would disarm a split mid-chapter, OnEnter misses a `console load`.
    //
    // It writes statics the main thread also writes. Nothing on the main thread writes them while a
    // level loads — a hotkey toggle no longer resets — so keep it that way: a reset added to a path
    // that runs during a load would race this one.
    public static void Level_OnLoadingThread(Level level) {
        SplitFeatures.ResetAll();
        SplitFeatures.RefreshAll(level);
    }

    private static void Level_OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow)
        => SplitFeatures.ResetAll();

    // Records what the features are doing and disarms nothing: a save state leaves the live level
    // running, so an armed split must still fire.
    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
        => dictionary[typeof(SomeSplitButtonsModule)] = new Dictionary<string, object> {
            [nameof(SplitFeatures)] = SplitFeatures.SnapshotAll(),
        };

    // A load puts the level back as it was saved, and SpeedrunTool restarts its room timer from there,
    // so the features go back to what they were doing at the save: a split counting down then counts
    // down again. Unconditional, for the same reason as Level_OnLoadingThread.
    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        SplitFeatures.ResetAll();
        // The state may come from another chapter, when SpeedrunTool keeps states across scene
        // switches: re-derive what depends on the chapter before putting the countdowns back.
        SplitFeatures.RefreshAll(level);
        if (dictionary.TryGetValue(typeof(SomeSplitButtonsModule), out Dictionary<string, object> saved)
            && saved.TryGetValue(nameof(SplitFeatures), out object snapshots)) {
            SplitFeatures.RestoreAll((object[]) snapshots);
        }
    }

    // Deliberately empty. Clearing a state leaves the live level running — and SpeedrunTool also
    // clears the existing state before every save over it, so a reset here cancelled a split in
    // flight on every save but a player's first.
    public static void OnClearState() { }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot) {
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);
        ModMenuOptions.CreateMenu(menu);
    }

    private static void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal)
        => PauseMenuButtons.Create(level, menu, minimal);

    public static void PopupMessage(string message) {
        SpeedrunToolHooks.ShowPopup(message);
    }

    /// <summary>Announces on screen which split button a hotkey just toggled, and to which state.</summary>
    // The hotkeys fire from gameplay, where nothing else reflects the new state: the mod menu is
    // closed and the split button only appears once paused.
    private static void AnnounceToggle(string buttonNameId, bool enabled) {
        PopupMessage(string.Format(
            Dialog.Get(enabled ? DialogIds.ButtonEnabledId : DialogIds.ButtonDisabledId),
            Dialog.Clean(buttonNameId)));
    }

    /// <summary>Set when Level.Update's own body runs, which is where Everest raises this event.</summary>
    private static bool levelBodyRan;

    private static void Level_OnBeforeUpdate(Level level) => levelBodyRan = true;

    private static void Level_OnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        levelBodyRan = false;
        orig(self);

        // Holds first, and above every settings gate: a hold stops the chapter clock until its
        // feature lets go, so switching a button off — or the whole mod off — must not strand one.
        // See SaveAndQuitTimer.UpdateHold.
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.UpdateHold?.Invoke(self);
        }

        // Everything below counts frames, and SpeedrunTool skips orig while it freezes the game after
        // a save or a load: the level did not advance, so neither may they. A split must wait 31
        // frames of play, not of freeze.
        if (!levelBodyRan) return;

        // Above the gate for a related reason: the heart protection is the one refusal a player
        // cannot switch off, so what it counts on must not be counted by something they can.
        HeartCheck.Update(self);

        // Also above the gate: a flag armed and then the mod disabled must still clear. SpeedrunTool's
        // timing runs inside orig, so a clear placed after it and before the feature loop still sees
        // the frame the split just landed on.
        ArrivalSplitSwallow.TickGraceBudget();

        // An armed feature runs whatever the settings say, the master one included: a press already
        // accepted completes.
        foreach (SplitFeature feature in SplitFeatures.All) {
            if ((Settings.Enabled && feature.Enabled()) || feature.Armed()) feature.Update(self);
        }
    }

    private static void Engine_OnUpdate(On.Monocle.Engine.orig_Update orig, Monocle.Engine self, Microsoft.Xna.Framework.GameTime gameTime) {
        orig(self, gameTime);

        // Polled even while the mod is off, which counts as a pause: a combo held while the mod is
        // switched back on must not read as a fresh press.
        Hotkeys.Set.Update(Settings.Enabled);
        SaveSettingsOutsideGameplay();
        if (!Settings.Enabled) return;

        foreach (SplitFeature feature in SplitFeatures.All) {
            if (Hotkeys.Set.Pressed(feature.Keybind)) ToggleFromHotkey(feature);
        }
    }

    /// <summary>Settings a hotkey changed that have not been written to disk yet.</summary>
    private static bool settingsUnsaved;

    /// <summary>What a split button's hotkey does: flip it, and say so on screen.</summary>
    internal static void ToggleFromHotkey(SplitFeature feature) {
        bool enabled = !feature.Enabled();
        feature.Toggle(enabled);
        // The mod menu leaves the save to Everest, which writes when the menu closes; nothing closes
        // on a hotkey's behalf. Deferred rather than written here, because here is mid-run.
        settingsUnsaved = true;
        AnnounceToggle(feature.ButtonLabelId, enabled);
    }

    /// <summary>Writes what a hotkey changed at the next moment the player is not playing.</summary>
    // A synchronous file write, so not on a gameplay frame: paused, or outside a level, is where a
    // hitch costs nothing. Unload saves whatever is still pending.
    private static void SaveSettingsOutsideGameplay() {
        if (!settingsUnsaved || Monocle.Engine.Scene is Level { Paused: false }) return;
        Instance.SaveSettings();
        settingsUnsaved = false;
    }
}
