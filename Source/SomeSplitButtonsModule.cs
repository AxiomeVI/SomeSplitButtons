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
using Celeste.Mod.SomeSplitButtons.Splits;
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
        // ModInterop leaves the delegate fields null when SpeedrunTool does not export
        // SpeedrunTool.SaveLoad. Calling through unchecked throws inside Load(), and Everest then
        // refuses the whole mod over one missing integration — so degrade like the reflection hooks
        // below and say what stops working.
        typeof(SaveLoadIntegration).ModInterop();
        if (SaveLoadIntegration.RegisterSaveLoadAction != null) {
            SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
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
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.Hotkey = new ComboHotkey(feature.Binding());
        }
        // Engine.Update, not Level.Update: the hotkeys arm split buttons for a run that has not
        // started yet, so they have to answer on the overworld and the chapter card too. The
        // instances above must exist before the hook goes up.
        On.Monocle.Engine.Update += Engine_OnUpdate;

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
        On.Monocle.Engine.Update -= Engine_OnUpdate;
        On.Celeste.Level.Update -= Level_OnUpdate;
        Everest.Events.LevelLoader.OnLoadingThread -= Level_OnLoadingThread;
        Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
        // Null whenever the registration above was skipped or refused; Unregister is null in exactly
        // the same case, since both come from the same import.
        if (SaveLoadInstance != null) {
            SaveLoadIntegration.Unregister?.Invoke(SaveLoadInstance);
            SaveLoadInstance = null;
        }
        SplitFeatures.ResetAll();
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
        SplitFeatures.ResetAll();
        SplitFeatures.RefreshAll(level);
    }

    private static void Level_OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow)
        => SplitFeatures.ResetAll();

    // The three save-state callbacks below only disarm the mod's own timers, so they run
    // unconditionally for the same reason as Level_OnLoadingThread. Disarming is all any of them
    // ever did — the managers used to spell out OnSaveState/OnLoadState/OnClearState one by one, and
    // all nine were Reset().
    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
        => SplitFeatures.ResetAll();

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
        => SplitFeatures.ResetAll();

    public static void OnBeforeSaveState(Level level) {
        if (!Settings.Enabled) return;
        if (Settings.ShowSaveAndQuitSplitButton) SaveAndQuitTimer.OnBeforeSaveState(level);
    }

    public static void OnClearState() => SplitFeatures.ResetAll();

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
    {
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);
        ModMenuOptions.CreateMenu(menu);
    }

    private static readonly HashSet<string> warnedMissingAnchors = new();

    /// <summary>
    ///     Index of the vanilla pause-menu button carrying <paramref name="dialogId"/>, or -1 when
    ///     this menu does not have it.
    /// </summary>
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

    /// <summary>
    ///     A split button's description, with the number of frames it will actually wait filled in.
    /// </summary>
    // Dialog.Get and not Dialog.Clean. Language.LoadTxt builds the Cleaned dictionary by running
    // `\{(.*?)\}` over every value and replacing each match with "" unless it is {n} or {break} —
    // so Clean deletes the placeholder along with the dialogue markup it shares its braces with, and
    // the sentence reaches the player as "after  frames". Every parameterised string in this mod is
    // read the same way, for the same reason.
    //
    // Both arguments are always supplied; the entries that quote only frames ignore the second.
    private static string SplitDescription(string dialogId, int frames)
        => string.Format(Dialog.Get(dialogId), frames, SplitTimings.ToSeconds(frames));

    /// <summary>
    ///     Inserts a split button at <paramref name="index"/> together with the description that
    ///     eases in while it holds focus.
    /// </summary>
    /// <summary>
    ///     Which entry the Save and Quit split must be, counted the way a player counts: in Down
    ///     presses from where the cursor opens.
    /// </summary>
    private const int SAVE_AND_QUIT_SLOT = 2;

    /// <summary>
    ///     The insertion index that makes a new entry the <paramref name="slot"/>-th one the cursor
    ///     can land on.
    /// </summary>
    // Hoverable, not Selectable: TextMenu.MoveCursor loops `while (!Current.Hoverable)`, and
    // Hoverable is `Selectable && Visible && !Disabled`. A greyed-out Retry is drawn but stepped
    // over, so it costs no press and must not be counted — which is the whole reason this button
    // lands below Options during a wake-up and above it during normal play.
    //
    // Reading Disabled here is safe because vanilla sets it immediately after each Add, well before
    // Everest fires OnCreatePauseMenuButtons. Our handler is also last in the invocation list, moved
    // there by Initialize(), so no other mod inserts above us afterwards and shifts the count.
    //
    // Falls through to the end of the menu when it has fewer than `slot` reachable entries, which no
    // pause menu carrying a Save and Quit does — the gate on that button is what makes this a
    // fallback rather than a case to handle.
    private static int SlotIndex(TextMenu menu, int slot) {
        int reachable = 0;
        for (int i = 0; i < menu.Items.Count; i++) {
            if (reachable == slot) return i;
            if (menu.Items[i].Hoverable) reachable++;
        }
        return menu.Items.Count;
    }

    private static void InsertSplitButton(TextMenu menu, int index, TextMenu.Button button, string description) {
        EaseInSubHeaderExt descriptionText = new(description, false, menu, null) {
            HeightExtra = 0f
        };
        // Both go in at the same index, description first: the button displaces it and so ends up
        // above its own description. Swapping these two lines inverts the pair.
        menu.Insert(index, descriptionText);
        menu.Insert(index, button);
        button.OnEnter = () => descriptionText.FadeVisible = true;
        button.OnLeave = () => descriptionText.FadeVisible = false;
    }

    private void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
        if (!Settings.Enabled) return;

        // ⚠️ A fixed slot, not an anchor, and that is a deliberate reversal of what this used to do.
        // Runners reach this button blind, two Down presses from a menu they never look at, in the
        // states where they need it: a wake-up animation, or the moment after a heart or a cassette.
        // Vanilla greys Retry out in exactly those states, so its own Save and Quit is the third
        // reachable entry — and the habit is two presses. The requirement is that number, not a
        // neighbour.
        //
        // Anchoring on Options happened to satisfy it and stopped: ExtendedVariantMode inserts its
        // submenu button above Options, which pushes this one to the fourth slot while leaving its
        // offset from Options at 1. Any mod that inserts above Options does the same, so nothing
        // measured relative to Options can express the requirement.
        //
        // Still gated on vanilla Save and Quit being present, which is now the only reason to look a
        // button up: it is what this one mirrors, and a minimal menu does not have it.
        //
        // The cost, stated because it is real: in a menu where Retry *is* reachable, holding slot 2
        // puts this button above Options rather than below it, and in a cutscene it lands above both
        // Skip Cutscene entries. The slot wins there too — that is the decision.
        if (Settings.ShowSaveAndQuitSplitButton) {
            int vanillaSaveQuitIndex = VanillaButtonIndex(menu, "menu_pause_savequit", warnIfMissing: !minimal);
            if (vanillaSaveQuitIndex >= 0) {
                MainSaveAndQuitSplitButton sq_button = new(Dialog.Clean(DialogIds.SaveAndQuitSplitButtonId));
                sq_button.Pressed(() => {
                    MainSaveAndQuitSplitButton.PressedHandler(level);
                });
                InsertSplitButton(menu, SlotIndex(menu, SAVE_AND_QUIT_SLOT), sq_button, SplitDescription(
                    Settings.SaveAndQuitAndRetry ? DialogIds.SQButtonRetryDesc : DialogIds.SQButtonDesc,
                    SplitTimings.WIPE_FADEOUT_FRAMES));
            }
        }

        if (Settings.ShowSkipCutsceneSplitButton
            && level.endingChapterAfterCutscene
            && !SkipCutsceneTimer.Hidden) {

            // Into vanilla Skip Cutscene's own slot, pushing it down — index 2 during an ending
            // cutscene, which is what the old literal Insert(2) hit. The vanilla button is always
            // there when this one is, since both need InCutscene.
            int skipIndex = VanillaButtonIndex(menu, "menu_pause_skip_cutscene", warnIfMissing: true);
            if (skipIndex >= 0) {
                MainSkipCutsceneSplitButton sc_button = new(Dialog.Clean(DialogIds.SkipCutsceneSplitButtonId));
                sc_button.Pressed(() => {
                    MainSkipCutsceneSplitButton.PressedHandler(level);
                });
                InsertSplitButton(menu, skipIndex, sc_button, SplitDescription(
                    SkipCutsceneTimer.InPrologue ? DialogIds.SCSPrologueButtonDesc : DialogIds.SCSButtonDesc,
                    SkipCutsceneTimer.FadeoutFrames));
            }
        }

        if (Settings.ShowReturnToMapSplitButton) {
            // Below vanilla Return to Map, which is the last entry of a default chapter's menu —
            // where the old menu.Add() put it. Anchoring keeps it beside its counterpart even if
            // another mod appends entries after it.
            int returnIndex = VanillaButtonIndex(menu, "menu_pause_return", warnIfMissing: !minimal);
            if (returnIndex >= 0) {
                MainReturnToMapSplitButton rtm_button = new(Dialog.Clean(DialogIds.ReturnToMapSplitButtonId));
                rtm_button.Pressed(() => {
                    MainReturnToMapSplitButton.PressedHandler(level, menu);
                });
                InsertSplitButton(menu, returnIndex + 1, rtm_button,
                    SplitDescription(DialogIds.RTMButtonDesc, SplitTimings.WIPE_FADEOUT_FRAMES));
            }
        }
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    /// <summary>
    ///     Announces on screen which split button a hotkey just toggled, and to which state.
    /// </summary>
    // The hotkeys fire from gameplay, where nothing else reflects the new state: the mod menu is
    // closed and the split button itself only appears once paused. Without this the runner has to
    // pause to find out whether the press registered.
    private static void AnnounceToggle(string buttonNameId, bool enabled) {
        PopupMessage(string.Format(
            Dialog.Get(enabled ? DialogIds.ButtonEnabledId : DialogIds.ButtonDisabledId),
            Dialog.Clean(buttonNameId)));
    }

    private static void Level_OnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // Holds first, and above every settings gate. They are vanilla flags the mod borrowed, so
        // switching a button off — or the whole mod off — must not strand one half-held. Turning
        // Save and Quit off during its hold used to freeze the chapter clock for the rest of the
        // Level; see SaveAndQuitTimer.UpdateHold.
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.UpdateHold?.Invoke(self);
        }

        if (!Settings.Enabled) return;
        foreach (SplitFeature feature in SplitFeatures.All) {
            if (feature.Enabled()) feature.Update(self);
        }
    }

    private static void Engine_OnUpdate(On.Monocle.Engine.orig_Update orig, Monocle.Engine self, Microsoft.Xna.Framework.GameTime gameTime) {
        orig(self, gameTime);
        if (!Settings.Enabled) return;

        // Every hotkey is polled before any of them is acted on, as it was when these were three
        // separate blocks. Handling one toggle can write settings and show a popup, and none of that
        // should sit between two reads of the same input frame.
        ComboHotkey.UpdateStates();
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.Hotkey.Update();
        }

        foreach (SplitFeature feature in SplitFeatures.All) {
            if (!feature.Hotkey.Pressed) continue;

            bool enabled = !feature.Enabled();
            feature.Toggle(enabled);
            // The mod menu leaves this to Everest, which saves when the menu closes. Nothing closes
            // on a hotkey's behalf.
            Instance.SaveSettings();
            AnnounceToggle(feature.NameId, enabled);
        }
    }

    /// <summary>
    ///     Marks the hotkeys' current input state as already consumed.
    /// </summary>
    // Called right after a rebind. ComboHotkey fires on a rising edge, and the key that was just
    // bound is still held when the remap screen hands focus back — so without this, binding a key
    // immediately toggles the button it was bound to.
    internal static void ResyncHotkeys() {
        ComboHotkey.UpdateStates();
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.Hotkey.Resync();
        }
    }
}