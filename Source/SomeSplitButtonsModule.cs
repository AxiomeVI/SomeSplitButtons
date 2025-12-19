using System;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitButton;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.ModInterop;
using Monocle;
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
        On.Monocle.Engine.Update += Engine_Update;
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
        On.Celeste.Level.SkipCutscene += OnSkipCutscene;
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
        On.Monocle.Engine.Update -= Engine_Update;
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        On.Celeste.Level.SkipCutscene -= OnSkipCutscene;
        Everest.Events.Level.OnComplete -= OnLevelComplete;
        SaveLoadIntegration.Unregister(SaveLoadInstance);
    }

    private void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
        if (Settings.ShowSaveAndQuitSplitButton) {
            MainSaveAndQuitSplitButton sq_button = new(Dialog.Get(DialogIds.SaveAndQuitSplitButtonId));
            sq_button.Pressed(() => {
                sq_button.PressedHandler(level);
            });
            menu.Insert(4, sq_button);
        }

        if (level.InCutscene
            && Settings.ShowSkipCutsceneSplitButton 
            && StaticSkipCutsceneSplitManager.cutsceneSkippedCounter >= Settings.CutscenesRequired
            && !StaticSkipCutsceneSplitManager.pressed) {

            MainSkipCutsceneSplitButton sc_button = new(Dialog.Get(DialogIds.SkipCutsceneSplitButtonId));
            sc_button.Pressed(() => {
                    sc_button.PressedHandler(level);
            });
            menu.Insert(2, sc_button);
        }
    }

    private static void OnSkipCutscene(On.Celeste.Level.orig_SkipCutscene orig, Level level) {
        if (Settings.ShowSkipCutsceneSplitButton) StaticSkipCutsceneSplitManager.cutsceneSkippedCounter++;
        orig(level);
    }

    private static void OnLevelComplete(Level level) {
        if (!Settings.ShowSkipCutsceneSplitButton) return;
        level.Completed = false;
        RoomTimerManager.UpdateTimerState();
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void Engine_Update(On.Monocle.Engine.orig_Update orig, Engine self, GameTime gameTime) {
        orig(self, gameTime);
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.Update();
        if (Settings.ShowSkipCutsceneSplitButton) SkipCutsceneTimer.Update(Settings.Prologue);
    }
}