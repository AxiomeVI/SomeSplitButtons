using System;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitButton;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplitButton;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplitManager;
using Celeste.Mod.SomeSplitButtons.Menu;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.ModInterop;
using static Celeste.TextMenuExt;
using FMOD.Studio;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace Celeste.Mod.SomeSplitButtons;

public class SomeSplitButtonsModule : EverestModule {
    public static SomeSplitButtonsModule Instance { get; private set; }

    public override Type SettingsType => typeof(SomeSplitButtonsModuleSettings);
    public static SomeSplitButtonsModuleSettings Settings => (SomeSplitButtonsModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(SomeSplitButtonsModuleSession);
    public static SomeSplitButtonsModuleSession Session => (SomeSplitButtonsModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(SomeSplitButtonsModuleSaveData);
    public static SomeSplitButtonsModuleSaveData SaveData => (SomeSplitButtonsModuleSaveData) Instance._SaveData;
    private object SaveLoadInstance = null;
    private static Hook _timingHook;
    private static Hook _updateTimerStateHook;

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
        // ⚠️ Load order is load-bearing here. This hook must sit *outside* SpeedrunTool's own
        // Level.Update hook (RoomTimerManager.Timing), so that SRT accumulates the frame before the
        // split timers call UpdateTimerState. It does because the everest.yaml dependency forces SRT
        // to load first, which makes this hook the outermost. Do not "tidy" the dependency away.
        On.Celeste.Level.Update += Level_OnUpdate;
        Everest.Events.LevelLoader.OnLoadingThread += Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
        typeof(SaveLoadIntegration).ModInterop();
        SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
            OnSaveState,
            OnLoadState,
            OnClearState,
            OnBeforeSaveState,
            null,
            null
        );
        // Both hooks are resolved by reflection against SpeedrunTool, so a rename on its side turns
        // them into no-ops. Say so in the log: without this the Skip Cutscene split just quietly
        // stops splitting, which reads as a mod bug rather than a version mismatch.
        var updateTimerStateMethod = typeof(RoomTimerManager).GetMethod("UpdateTimerState", BindingFlags.Public | BindingFlags.Static);
        if (updateTimerStateMethod != null) {
            _updateTimerStateHook = new Hook(
                updateTimerStateMethod,
                typeof(SkipCutsceneTimer).GetMethod("OnUpdateTimerState", BindingFlags.Public | BindingFlags.Static)
            );
        }
        else {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool RoomTimerManager.UpdateTimerState not found — the Skip Cutscene split will not hold back the room timer.");
        }

        var assembly = typeof(RoomTimerManager).Assembly;
        var roomTimerDataType = assembly.GetType("Celeste.Mod.SpeedrunTool.RoomTimer.RoomTimerData");
        var timingMethod = roomTimerDataType?.GetMethod("Timing", BindingFlags.Public | BindingFlags.Instance);
        if (timingMethod != null) {
            _timingHook = new Hook(
                timingMethod,
                typeof(SkipCutsceneTimer).GetMethod("OnTiming", BindingFlags.Public | BindingFlags.Static)
            );
        }
        else {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                "SpeedrunTool RoomTimerData.Timing not found — the room timer will stop at chapter completion instead of at the Skip Cutscene mark.");
        }
    }

    /// <summary>
    ///     Moves the pause-menu handler to the end of the event's invocation list.
    /// </summary>
    public override void Initialize() {
        base.Initialize();
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
    }

    public override void Unload() {
        On.Celeste.Level.Update -= Level_OnUpdate;
        Everest.Events.LevelLoader.OnLoadingThread -= Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        SaveLoadIntegration.Unregister(SaveLoadInstance);
        SaveAndQuitTimer.Reset();
        SkipCutsceneTimer.Reset();
        ReturnToMapTimer.Reset();
        Everest.Events.Level.OnExit -= Level_OnLevelExit;
        _timingHook?.Dispose();
        _timingHook = null;
        _updateTimerStateHook?.Dispose();
        _updateTimerStateHook = null;
    }

    /// <summary>
    ///     Disarms every split timer on level load, whether or not its feature is enabled.
    /// </summary>
    public static void Level_OnLoadingThread(Level level)
    {
        SkipCutsceneTimer.Reset();
        SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex);
        SaveAndQuitTimer.Reset();
        ReturnToMapTimer.Reset();
    }

    private static void Level_OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow)
    {
        SkipCutsceneTimer.Reset();
        SaveAndQuitTimer.Reset();
        ReturnToMapTimer.Reset();
    }

    // The three save-state callbacks below only disarm the mod's own timers, so they run
    // unconditionally for the same reason as Level_OnLoadingThread.
    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        SkipCutsceneTimer.OnSaveState();
        SaveAndQuitTimer.OnSaveState();
        ReturnToMapTimer.OnSaveState();
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        SkipCutsceneTimer.OnLoadState();
        SaveAndQuitTimer.OnLoadState();
        ReturnToMapTimer.OnLoadState();
    }

    public static void OnBeforeSaveState(Level level) {
        if (!Settings.Enabled) return;
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnBeforeSaveState(level);
    }

    public static void OnClearState() {
        SkipCutsceneTimer.OnClearState();
        SaveAndQuitTimer.OnClearState();
        ReturnToMapTimer.OnClearState();
    }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
    {
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);
        ModMenuOptions.CreateMenu(menu);
        CreateModMenuSectionKeyBindings(menu, inGame, pauseSnapshot);
    }

    /// <summary>
    ///     Index of the vanilla pause-menu button carrying <paramref name="dialogId"/>, or -1 when
    ///     this menu does not have it.
    /// </summary>
    private static readonly HashSet<string> warnedMissingAnchors = new();

    private static int VanillaButtonIndex(TextMenu menu, string dialogId, bool warnIfMissing) {
        string label = Dialog.Clean(dialogId);
        int index = menu.Items.FindIndex(item => item is TextMenu.Button button && button.Label == label);

        // Once per anchor per session: the menu is rebuilt on every pause, and this would otherwise
        // fill the log. Without it the only symptom is a button that quietly never appears, which
        // reads as a forgotten setting rather than as a conflict with another mod.
        if (index < 0 && warnIfMissing && warnedMissingAnchors.Add(dialogId)) {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                $"no '{dialogId}' button in the pause menu — the split button anchored on it is being skipped. " +
                "Another mod has probably replaced or removed it.");
        }
        return index;
    }

    private void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
        if (!Settings.Enabled) return;

        // Directly below Options — index 4 in a default chapter, which is what the old literal
        // Insert(4) hit. Anchoring on Options is what keeps it there once Assist Mode or Variant
        // Mode pushes everything down.
        // Gated on vanilla Save and Quit being present as well: it is the button this one mirrors,
        // and a minimal menu has neither.
        int optionsIndex = VanillaButtonIndex(menu, "menu_pause_options", warnIfMissing: false);
        if (Settings.ShowSaveAndQuitSplitButton
            && optionsIndex >= 0
            && VanillaButtonIndex(menu, "menu_pause_savequit", warnIfMissing: !minimal) >= 0) {

            MainSaveAndQuitSplitButton sq_button = new(Dialog.Clean(DialogIds.SaveAndQuitSplitButtonId));
            sq_button.Pressed(() => {
                MainSaveAndQuitSplitButton.PressedHandler(level);
            });
            EaseInSubHeaderExt descriptionText = new(Settings.SaveAndQuitAndRetry ? Dialog.Clean(DialogIds.SQButtonRetryDesc) : Dialog.Clean(DialogIds.SQButtonDesc), false, menu, null)
            {
                HeightExtra = 0f
            };
            menu.Insert(optionsIndex + 1, descriptionText);
            menu.Insert(optionsIndex + 1, sq_button);
            sq_button.OnEnter = () => descriptionText.FadeVisible = true;
            sq_button.OnLeave = () => descriptionText.FadeVisible = false;
        }

        if (Settings.ShowSkipCutsceneSplitButton
            && level.endingChapterAfterCutscene
            && !SkipCutsceneTimer.Hidden) {

            MainSkipCutsceneSplitButton sc_button = new(Dialog.Clean(DialogIds.SkipCutsceneSplitButtonId));
            sc_button.Pressed(() => {
                    MainSkipCutsceneSplitButton.PressedHandler(level);
            });
            EaseInSubHeaderExt descriptionText = new(level.Session.Area.ChapterIndex == -1 ? Dialog.Clean(DialogIds.SCSPrologueButtonDesc) : Dialog.Clean(DialogIds.SCSButtonDesc), false, menu, null)
            {
                HeightExtra = 0f
            };
            // Into vanilla Skip Cutscene's own slot, pushing it down — index 2 during an ending
            // cutscene, which is what the old literal Insert(2) hit. The vanilla button is always
            // there when this one is, since both need InCutscene.
            int skipIndex = VanillaButtonIndex(menu, "menu_pause_skip_cutscene", warnIfMissing: true);
            if (skipIndex >= 0) {
                menu.Insert(skipIndex, descriptionText);
                menu.Insert(skipIndex, sc_button);
                sc_button.OnEnter = () => descriptionText.FadeVisible = true;
                sc_button.OnLeave = () => descriptionText.FadeVisible = false;
            }
        }

        if (Settings.ShowReturnToMapSplitButton) {
            MainReturnToMapSplitButton rtm_button = new(Dialog.Clean(DialogIds.ReturnToMapSplitButtonId));
            rtm_button.Pressed(() => {
                MainReturnToMapSplitButton.PressedHandler(level, menu);
            });
            EaseInSubHeaderExt descriptionText = new(Dialog.Clean(DialogIds.RTMButtonDesc), false, menu, null)
            {
                HeightExtra = 0f
            };
            // Below vanilla Return to Map, which is the last entry of a default chapter's menu —
            // where the old menu.Add() put it. Anchoring keeps it beside its counterpart even if
            // another mod appends entries after it.
            int returnIndex = VanillaButtonIndex(menu, "menu_pause_return", warnIfMissing: !minimal);
            if (returnIndex >= 0) {
                menu.Insert(returnIndex + 1, descriptionText);
                menu.Insert(returnIndex + 1, rtm_button);
                rtm_button.OnEnter = () => descriptionText.FadeVisible = true;
                rtm_button.OnLeave = () => descriptionText.FadeVisible = false;
            }
        }
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void Level_OnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);
        if (!Settings.Enabled) return;

        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.Update(self);
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.Update(self);
        if (Settings.ShowReturnToMapSplitButton) ReturnToMapTimer.Update();

        if (Settings.ButtonToggleSaveQuit.Pressed) {
            Settings.ShowSaveAndQuitSplitButton = !Settings.ShowSaveAndQuitSplitButton;
            SaveAndQuitTimer.Reset();
            Instance.SaveSettings();
        }

        if (Settings.ButtonToggleSkipCutscene.Pressed) {
            Settings.ShowSkipCutsceneSplitButton = !Settings.ShowSkipCutsceneSplitButton;
            SkipCutsceneTimer.Reset();
            if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.PrologueCheck(self.Session.Area.ChapterIndex);
            Instance.SaveSettings();
        }

        if (Settings.ButtonToggleReturnToMap.Pressed) {
            Settings.ShowReturnToMapSplitButton = !Settings.ShowReturnToMapSplitButton;
            ReturnToMapTimer.Reset();
            Instance.SaveSettings();
        }
    }
}