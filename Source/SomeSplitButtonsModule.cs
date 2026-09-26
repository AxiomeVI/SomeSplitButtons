using System;
using Celeste.Mod.CelesteHotkeys;
using Celeste.Mod.SpeedrunTool.Message;
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
                OnBeforeSaveState,
                null,
                null
            );
        }
        else {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool.SaveLoad ModInterop not found — the split timers will not be disarmed around save states.");
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
        On.Monocle.Engine.Update -= Engine_OnUpdate;
        On.Celeste.Level.Update -= Level_OnUpdate;
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
    // It writes the same statics the main thread's hotkey loop can write through Toggle → Reset.
    // Both write whole bools and ints, the window is a few milliseconds, and the worst outcome is a
    // split disarmed that was about to be disarmed anyway — but it is a race, and a known one.
    public static void Level_OnLoadingThread(Level level) {
        SplitFeatures.ResetAll();
        SplitFeatures.RefreshAll(level);
    }

    private static void Level_OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow)
        => SplitFeatures.ResetAll();

    // The three save-state callbacks below only disarm the mod's own timers, so they run
    // unconditionally for the same reason as Level_OnLoadingThread.
    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
        => SplitFeatures.ResetAll();

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
        => SplitFeatures.ResetAll();

    // No settings gate, unlike the rest of this file. What these release are vanilla flags the mod
    // borrowed; gating the release on a setting cleared flags the mod never set, and stranded ones it
    // did. See SaveAndQuitTimer.ReleaseHoldForSaveState.
    public static void OnBeforeSaveState(Level level) => SplitFeatures.BeforeSaveStateAll(level);

    public static void OnClearState() => SplitFeatures.ResetAll();

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot) {
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);
        ModMenuOptions.CreateMenu(menu);
    }

    private static void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal)
        => PauseMenuButtons.Create(level, menu, minimal);

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    /// <summary>Announces on screen which split button a hotkey just toggled, and to which state.</summary>
    // The hotkeys fire from gameplay, where nothing else reflects the new state: the mod menu is
    // closed and the split button only appears once paused.
    private static void AnnounceToggle(string buttonNameId, bool enabled) {
        PopupMessage(string.Format(
            Dialog.Get(enabled ? DialogIds.ButtonEnabledId : DialogIds.ButtonDisabledId),
            Dialog.Clean(buttonNameId)));
    }

    private static void Level_OnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // Holds first, and above every settings gate. They are vanilla flags the mod borrowed, so
        // switching a button off — or the whole mod off — must not strand one half-held. See
        // SaveAndQuitTimer.UpdateHold.
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.UpdateHold?.Invoke(self);
        }

        // Above the gate for a related reason: the heart protection is the one refusal a player
        // cannot switch off, so what it counts on must not be counted by something they can.
        HeartCheck.Update(self);

        // Also above the gate: a flag armed and then the mod disabled must still clear. SpeedrunTool's
        // timing runs inside orig, so a clear placed after it and before the feature loop still sees
        // the frame the split just landed on.
        ArrivalSplitSwallow.TickGraceBudget();

        if (!Settings.Enabled) return;
        foreach (SplitFeature feature in SplitFeatures.All) {
            if (feature.Enabled()) feature.Update(self);
        }
    }

    private static void Engine_OnUpdate(On.Monocle.Engine.orig_Update orig, Monocle.Engine self, Microsoft.Xna.Framework.GameTime gameTime) {
        orig(self, gameTime);

        // Polled even while the mod is off, which counts as a pause: a combo held while the mod is
        // switched back on must not read as a fresh press.
        Hotkeys.Set.Update(Settings.Enabled);
        if (!Settings.Enabled) return;

        foreach (SplitFeature feature in SplitFeatures.All) {
            if (!Hotkeys.Set.Pressed(feature.Keybind)) continue;

            bool enabled = !feature.Enabled();
            feature.Toggle(enabled);
            // The mod menu leaves this to Everest, which saves when the menu closes. Nothing closes
            // on a hotkey's behalf.
            Instance.SaveSettings();
            AnnounceToggle(feature.ButtonLabelId, enabled);
        }
    }
}
