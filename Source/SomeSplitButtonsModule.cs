using System;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitButton;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.ModInterop;
using static Celeste.TextMenuExt;
using Microsoft.Xna.Framework;

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
        On.Celeste.Level.Update += LevelOnUpdate;
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
        Everest.Events.Level.OnComplete += OnLevelComplete;
        typeof(SaveLoadIntegration).ModInterop();
        SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
            StaticSkipCutsceneSplitManager.OnSaveState, 
            StaticSkipCutsceneSplitManager.OnLoadState, 
            StaticSkipCutsceneSplitManager.OnClearState, 
            null,
            null,
            null
        );
    }

    public override void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        Everest.Events.Level.OnComplete -= OnLevelComplete;
        SaveLoadIntegration.Unregister(SaveLoadInstance);
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
            && !StaticSkipCutsceneSplitManager.pressed) {

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

    private static void OnLevelComplete(Level level) {
        if (!Settings.Enabled || !Settings.ShowSkipCutsceneSplitButton) return;
        level.Completed = false;
        RoomTimerManager.UpdateTimerState();
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);
        if (!Settings.Enabled) return;

        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.Update();
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.Update();

        if (Settings.ButtonToggleSaveQuit.Pressed) {
            Settings.ShowSaveAndQuitSplitButton = !Settings.ShowSaveAndQuitSplitButton;
            Instance.SaveSettings();
        }

        if (Settings.ButtonToggleSkipCutscene.Pressed) {
            Settings.ShowSkipCutsceneSplitButton = !Settings.ShowSkipCutsceneSplitButton;
            Instance.SaveSettings();
        }
    }
}