using System;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitButton;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SomeSplitButtons.Menu;
using Celeste.Mod.SomeSplitButtons.UI;
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
    private static ComboHotkey _saveQuitHotkey;
    private static ComboHotkey _skipCutsceneHotkey;

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
        _saveQuitHotkey = new ComboHotkey(Settings.ButtonToggleSaveQuit);
        _skipCutsceneHotkey = new ComboHotkey(Settings.ButtonToggleSkipCutscene);

        var updateTimerStateMethod = typeof(RoomTimerManager).GetMethod("UpdateTimerState", BindingFlags.Public | BindingFlags.Static);
        if (updateTimerStateMethod != null) {
            _updateTimerStateHook = new Hook(
                updateTimerStateMethod,
                typeof(SkipCutsceneTimer).GetMethod("OnUpdateTimerState", BindingFlags.Public | BindingFlags.Static)
            );
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
    }

    public override void Unload() {
        On.Celeste.Level.Update -= Level_OnUpdate;
        Everest.Events.LevelLoader.OnLoadingThread -= Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        SaveLoadIntegration.Unregister(SaveLoadInstance);
        SaveAndQuitTimer.Reset();
        SkipCutsceneTimer.Reset();
        Everest.Events.Level.OnExit -= Level_OnLevelExit;
        _timingHook?.Dispose();
        _timingHook = null;
        _updateTimerStateHook?.Dispose();
        _updateTimerStateHook = null;
    }

    public static void Level_OnLoadingThread(Level level)
    {
        if (!Settings.Enabled) return;
        if (Settings.ShowSkipCutsceneSplitButton) 
        {
            SkipCutsceneTimer.Reset();
            SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex);
        }
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.Reset();
    }

    private static void Level_OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) 
    {
        if (!Settings.Enabled) return;
        SaveAndQuitTimer.Reset();
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        if (!Settings.Enabled) return;		
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.OnSaveState();
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnSaveState();
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        if (!Settings.Enabled) return;
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.OnLoadState();
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnLoadState();
    }

    public static void OnBeforeSaveState(Level level) {
        if (!Settings.Enabled) return;
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnBeforeSaveState(level);
    }

    public static void OnClearState() {
        if (!Settings.Enabled) return;
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.OnClearState();
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnClearState();
    }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
    {
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);
        ModMenuOptions.CreateMenu(menu);
    }

    private void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
        if (!Settings.Enabled) return;

        if (Settings.ShowSaveAndQuitSplitButton) {
            MainSaveAndQuitSplitButton sq_button = new(Dialog.Clean(DialogIds.SaveAndQuitSplitButtonId));
            sq_button.Pressed(() => {
                MainSaveAndQuitSplitButton.PressedHandler(level);
            });
            EaseInSubHeaderExt descriptionText = new(Settings.SaveAndQuitAndRetry ? Dialog.Clean(DialogIds.SQButtonRetryDesc) : Dialog.Clean(DialogIds.SQButtonDesc), false, menu, null)
            {
                HeightExtra = 0f
            };
            menu.Insert(4, descriptionText);
            menu.Insert(4, sq_button);
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
            menu.Insert(2, descriptionText);
            menu.Insert(2, sc_button);
            sc_button.OnEnter = () => descriptionText.FadeVisible = true;
            sc_button.OnLeave = () => descriptionText.FadeVisible = false;
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

        ComboHotkey.UpdateStates();
        _saveQuitHotkey.Update();
        _skipCutsceneHotkey.Update();

        if (_saveQuitHotkey.Pressed) {
            Settings.ShowSaveAndQuitSplitButton = !Settings.ShowSaveAndQuitSplitButton;
            SaveAndQuitTimer.Reset();
            Instance.SaveSettings();
        }

        if (_skipCutsceneHotkey.Pressed) {
            Settings.ShowSkipCutsceneSplitButton = !Settings.ShowSkipCutsceneSplitButton;
            SkipCutsceneTimer.Reset();
            if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.PrologueCheck(self.Session.Area.ChapterIndex);
            Instance.SaveSettings();
        }
    }
}