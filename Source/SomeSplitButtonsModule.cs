using System;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.UI;
using Celeste.Mod.SomeSplitButtons.Splits;
using Microsoft.Xna.Framework.Input;
using MonoMod.ModInterop;
using static Celeste.TextMenuExt;
using FMOD.Studio;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons;

public class SomeSplitButtonsModule : EverestModule {
    public static SomeSplitButtonsModule Instance { get; private set; }

    public override Type SettingsType => typeof(SomeSplitButtonsModuleSettings);
    public static SomeSplitButtonsModuleSettings Settings => (SomeSplitButtonsModuleSettings) Instance._Settings;

    // No SessionType or SaveDataType. Both were registered with empty classes behind them, which made
    // Everest serialise an empty object into every save file and every session for nothing. Declare
    // them again when there is a field to put in them, not before.
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
            // Keys.None must not survive into a hotkey. It is FNA's "no XNA key for this" sentinel,
            // and a keyboard state reports it held whenever an unmappable key is — AZERTY's ")" is
            // one — so a binding carrying it toggles a split button on an unrelated press. Settings
            // written while the defaults still seeded it are on disk, so strip it on the way in
            // rather than trusting the file.
            feature.Binding().Keys.RemoveAll(key => key == Keys.None);
            feature.Hotkey = new ComboHotkey(feature.Binding());
        }
        // Engine.Update, not Level.Update: the hotkeys arm split buttons for a run that has not
        // started yet, so they have to answer on the overworld and the chapter card too. The
        // instances above must exist before the hook goes up.
        On.Monocle.Engine.Update += Engine_OnUpdate;

        SpeedrunToolHooks.Install();
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
        SpeedrunToolHooks.Uninstall();
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
    ///     Which entry each split button must be, counted the way a player counts: in Down presses
    ///     from where the cursor opens.
    /// </summary>
    // These are the placement rules, and they are absolute. Every one of these buttons is reached
    // blind, by a thumb that has already moved before the menu is read — so what matters is the
    // number of presses, not which vanilla entry happens to be adjacent.
    //
    // ⚠️ The order the three are inserted in is load-bearing and must match the order of these
    // constants. Each insertion is measured against the menu as it stands, so a later one placed
    // above an earlier one would silently push it down a slot. Skip Cutscene is inserted first for
    // exactly that reason.
    private const int SKIP_CUTSCENE_SLOT = 1;
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
    // ⚠️ The returned index is always *before a reachable entry*, never before an unreachable one.
    // That is what keeps a split button from landing between an earlier split button and its own
    // description: the pair occupies one reachable slot and two items, and the count reaches its
    // target while the description is still the next item along. Returning that index would cut the
    // pair in half and leave each button showing its neighbour's text.
    //
    // Falls through to the end of the menu when it has fewer than `slot` reachable entries. That is
    // the correct answer rather than a fallback — a button asked for a slot the menu does not have
    // belongs after everything, which is also how the Return to Map split asks for last place.
    private static int SlotIndex(TextMenu menu, int slot) {
        int reachable = 0;
        for (int i = 0; i < menu.Items.Count; i++) {
            if (!menu.Items[i].Hoverable) continue;
            if (reachable == slot) return i;
            reachable++;
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

        // ⚠️ Fixed slots, not anchors, and that is a deliberate reversal of what this used to do.
        // Every vanilla lookup below is now a *gate* — it decides whether this menu should carry a
        // split button at all — and none of them decides where it goes.
        //
        // Anchoring satisfied the real requirement by coincidence until it stopped. ExtendedVariantMode
        // inserts its submenu button above Options, which pushed Save and Quit from the third
        // reachable entry to the fourth while leaving its offset from Options at 1. Any mod inserting
        // above an anchor does the same, so nothing measured relative to one can express "two Down
        // presses".
        //
        // ⚠️ Insertion order is the rule, in ascending slot order. Each call measures the menu as it
        // stands, so inserting a lower slot after a higher one pushes the higher one down without
        // anything noticing.

        // Slot 1 — one Down press. First in, because it is the smallest slot: placing it after Save
        // and Quit would move Save and Quit to 3, which is how it behaved before these rules and is
        // the defect they close.
        if (Settings.ShowSkipCutsceneSplitButton
            && level.endingChapterAfterCutscene
            && !SkipCutsceneTimer.Hidden) {

            // The vanilla button is always there when this one is, since both need InCutscene — so
            // its absence is a conflict with another mod and always worth a warning.
            if (VanillaButtonIndex(menu, "menu_pause_skip_cutscene", warnIfMissing: true) >= 0) {
                SkipCutsceneSplitButton sc_button = new(Dialog.Clean(DialogIds.SkipCutsceneSplitButtonId));
                sc_button.Pressed(() => {
                    SkipCutsceneSplitButton.PressedHandler(level);
                });
                InsertSplitButton(menu, SlotIndex(menu, SKIP_CUTSCENE_SLOT), sc_button, SplitDescription(
                    SkipCutsceneTimer.InPrologue ? DialogIds.SCSPrologueButtonDesc : DialogIds.SCSButtonDesc,
                    SkipCutsceneTimer.FadeoutFrames));
            }
        }

        // Slot 2 — two Down presses. The habit comes from vanilla: it greys Retry out during a
        // wake-up, and after a heart or a cassette, which are the moments this button is for. Its own
        // Save and Quit is the third reachable entry there, and the thumb has already moved.
        if (Settings.ShowSaveAndQuitSplitButton) {
            if (VanillaButtonIndex(menu, "menu_pause_savequit", warnIfMissing: !minimal) >= 0) {
                SaveAndQuitSplitButton sq_button = new(Dialog.Clean(DialogIds.SaveAndQuitSplitButtonId));
                sq_button.Pressed(() => {
                    SaveAndQuitSplitButton.PressedHandler(level);
                });
                InsertSplitButton(menu, SlotIndex(menu, SAVE_AND_QUIT_SLOT), sq_button, SplitDescription(
                    Settings.SaveAndQuitAndReenter ? DialogIds.SQButtonReenterDesc : DialogIds.SQButtonDesc,
                    SplitTimings.WIPE_FADEOUT_FRAMES));
            }
        }

        // Last, always. Reached by holding Down rather than by counting, so the requirement is the
        // end of the menu and not a distance from vanilla Return to Map — which is merely what
        // usually sits there.
        if (Settings.ShowReturnToMapSplitButton) {
            if (VanillaButtonIndex(menu, "menu_pause_return", warnIfMissing: !minimal) >= 0) {
                ReturnToMapSplitButton rtm_button = new(Dialog.Clean(DialogIds.ReturnToMapSplitButtonId));
                rtm_button.Pressed(() => {
                    ReturnToMapSplitButton.PressedHandler(level, menu);
                });
                InsertSplitButton(menu, menu.Items.Count, rtm_button,
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
        // The countdowns do not stop for a pause, and that is the intended behaviour rather than an
        // accident of where this sits. `Level.Update` runs while paused and the count happens after
        // `orig`, so re-pausing inside the 31 (or 18) frames keeps the counter going. A button that
        // promises a split in 31 frames promises 31 frames; a player who re-pauses during a fade-out
        // is fumbling, not asking to suspend their split. Suspending would also have to answer what
        // happens when the pause lasts, and a split arriving ten seconds late is worse than one
        // arriving slightly early.
        foreach (SplitFeature feature in SplitFeatures.All) {
            if (feature.Enabled()) feature.Update(self);
        }
    }

    private static void Engine_OnUpdate(On.Monocle.Engine.orig_Update orig, Monocle.Engine self, Microsoft.Xna.Framework.GameTime gameTime) {
        orig(self, gameTime);
        if (!Settings.Enabled) return;

        // One snapshot for every hotkey. That, rather than the two loops, is now what guarantees
        // they all answer the same input frame — handling a toggle writes settings and shows a
        // popup, and this used to be a live read that such work could sit in the middle of.
        InputSnapshot input = InputSnapshot.Current();
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.Hotkey.Update(input);
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
        InputSnapshot input = InputSnapshot.Current();
        foreach (SplitFeature feature in SplitFeatures.All) {
            feature.Hotkey.Resync(input);
        }
    }
}