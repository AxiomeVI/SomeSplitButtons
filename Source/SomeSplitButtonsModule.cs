using System;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitButton;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SomeSplitButtons.Menu;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.ModInterop;
using static Celeste.TextMenuExt;
using FMOD.Studio;
using System.Collections.Generic;

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
        On.Celeste.Level.Update += Level_OnUpdate;
        Everest.Events.LevelLoader.OnLoadingThread += Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
        Everest.Events.Level.OnComplete += Level_OnLevelComplete;
        typeof(SaveLoadIntegration).ModInterop();
        SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
            OnSaveState, 
            OnLoadState, 
            OnClearState, 
            null,
            null,
            null
        );
    }

    public override void Unload() {
        On.Celeste.Level.Update -= Level_OnUpdate;
        Everest.Events.LevelLoader.OnLoadingThread -= Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        Everest.Events.Level.OnComplete -= Level_OnLevelComplete;
        SaveLoadIntegration.Unregister(SaveLoadInstance);
    }

    public void Level_OnLoadingThread(Level level)
    {
        if (!Settings.Enabled) return;
        if (Settings.ShowSkipCutsceneSplitButton) 
        {
            SkipCutsceneTimer.Reset();
            SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex);
        }
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.Reset();
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        if (!Settings.Enabled) return;		
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.OnSaveState(dictionary, level);
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnSaveState(dictionary, level);
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        if (!Settings.Enabled) return;
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.OnLoadState(dictionary, level);
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnLoadState(dictionary, level);
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
        CreateModMenuSectionKeyBindings(menu, inGame, pauseSnapshot);
    }

    private void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
        if (!Settings.Enabled) return;

        if (Settings.ShowSaveAndQuitSplitButton) {
            MainSaveAndQuitSplitButton sq_button = new(Dialog.Clean(DialogIds.SaveAndQuitSplitButtonId));
            sq_button.Pressed(() => {
                sq_button.PressedHandler(level);
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
            && !SkipCutsceneTimer.hidden) {

            MainSkipCutsceneSplitButton sc_button = new(Dialog.Clean(DialogIds.SkipCutsceneSplitButtonId));
            sc_button.Pressed(() => {
                    sc_button.PressedHandler(level);
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

    private static void Level_OnLevelComplete(Level level) {
        if (!Settings.Enabled || !Settings.ShowSkipCutsceneSplitButton) return;
        level.Completed = false;
        RoomTimerManager.UpdateTimerState();
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void Level_OnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);
        if (!Settings.Enabled) return;

        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.Update(self);
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.Update(self);

        if (Settings.ButtonToggleSaveQuit.Pressed) {
            Settings.ShowSaveAndQuitSplitButton = !Settings.ShowSaveAndQuitSplitButton;
            SkipCutsceneTimer.Reset();
            if (Settings.ShowSaveAndQuitSplitButton) SkipCutsceneTimer.PrologueCheck(self.Session.Area.ChapterIndex);
            Instance.SaveSettings();
        }

        if (Settings.ButtonToggleSkipCutscene.Pressed) {
            Settings.ShowSkipCutsceneSplitButton = !Settings.ShowSkipCutsceneSplitButton;;
            SkipCutsceneTimer.Reset();
            Instance.SaveSettings();
        }
    }
}