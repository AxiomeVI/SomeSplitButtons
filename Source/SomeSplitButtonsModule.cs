using System;
using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitButton;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.ModInterop;
using Monocle;

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
        Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
        On.Celeste.Level.Update += OnLevelUpdate;
        // TODO: understand what it does
        typeof(SaveLoadIntegration).ModInterop();
        SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
            StaticSkipCutsceneSplitManager.OnSaveState, 
            StaticSkipCutsceneSplitManager.OnLoadState, 
            StaticSkipCutsceneSplitManager.OnClearState, 
            StaticSkipCutsceneSplitManager.OnBeforeSaveState,
            StaticSkipCutsceneSplitManager.OnBeforeLoadState, 
            null
        );
    }

    public override void Unload() {
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        On.Celeste.Level.Update -= OnLevelUpdate;
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

        if (Settings.ShowSkipCutsceneSplitButton && StaticSkipCutsceneSplitManager.counter >= Settings.CutscenesRequired){
            MainSkipCutsceneSplitButton sc_button = new(Dialog.Get(DialogIds.SkipCutsceneSplitButtonId));
            sc_button.Pressed(() => {
                    sc_button.PressedHandler(level);
                    StaticSkipCutsceneSplitManager.counter = 0;
            });

            // https://github.com/EverestAPI/Everest/blob/d7bc4c2716b747d243fa4347b98422766b3d8b5a/Celeste.Mod.mm/Mod/Core/CoreModule.cs#L234
            List<TextMenu.Item> items = menu.Items;
            int index;
            // Find the skip cutscene button and place our button above it.
            string cleanedOptions = Dialog.Clean("menu_pause_skip_cutscene");
            index = items.FindIndex(_ => {
                TextMenu.Button other = (_ as TextMenu.Button);
                if (other == null)
                    return false;
                return other.Label == cleanedOptions;
            });
            if (index != -1) menu.Insert(index++, sc_button);
        }
    }

    private static void OnLevelUpdate(On.Celeste.Level.orig_Update orig, Level level) {
        if (!Settings.ShowSkipCutsceneSplitButton) {
            orig(level); 
            return;
        }
        // TODO: find a better way to do this, maybe with level.onCutsceneSkip
        if (level.SkippingCutscene && StaticSkipCutsceneSplitManager.cutsceneTimer == 0) StaticSkipCutsceneSplitManager.counter++;
        if (level.SkippingCutscene) StaticSkipCutsceneSplitManager.cutsceneTimer++; else StaticSkipCutsceneSplitManager.cutsceneTimer = 0;
        orig(level);
        // TODO: find a way to keep level completion counting as a room for the timer
        level.Completed = false;
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }
}

public static class Timer {

    // CelesteTAS info hud function https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L546
    internal static int ToCeilingFrames(this float timer) {
        if (timer <= 0.0f) {
            return 0;
        }

        float frames = MathF.Ceiling(timer / Engine.DeltaTime);
        return float.IsInfinity(frames) || float.IsNaN(frames) ? int.MaxValue : (int) frames;
    }
    
    public static void HandleTimerButtonPressed(bool berryCheck = true) {
        if (berryCheck) {
            Player player = (Engine.Scene as Level).Tracker.GetEntity<Player>();

            // CelesteTAS info hud format https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L307
            Follower? firstRedBerryFollower = player.Leader.Followers.Find(follower => follower.Entity is Strawberry {Golden: false});
            if (firstRedBerryFollower?.Entity is Strawberry firstRedBerry) {
                float collectTimer = firstRedBerry.collectTimer;
                if (collectTimer <= 0.15f) {
                    int collectFrames = (0.15f - collectTimer).ToCeilingFrames();
                    if (collectTimer >= 0f) {
                        SomeSplitButtonsModule.PopupMessage($"Berry({collectFrames}) ");
                    } else {
                        int additionalFrames = Math.Abs(collectTimer).ToCeilingFrames();
                        SomeSplitButtonsModule.PopupMessage($"Berry({collectFrames - additionalFrames}+{additionalFrames}) ");
                    }
                }
                return;
            }
        }
        RoomTimerManager.UpdateTimerState();
    }
}