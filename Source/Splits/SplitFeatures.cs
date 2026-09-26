using System;
using Celeste.Mod.CelesteHotkeys;
using Celeste.Mod.SomeSplitButtons.Interop;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     One split button, described uniformly enough that callers can loop over the set instead of
///     naming each one.
/// </summary>
// ⚠️ Every delegate that touches Settings is deferred. This table is built at type load, and
// SomeSplitButtonsModule.Settings needs Instance._Settings, which Everest fills in later.
internal sealed class SplitFeature {
    /// <summary>Dialog id of the button's name, used when a hotkey announces the toggle.</summary>
    internal required string NameId { get; init; }

    /// <summary>The hotkey that toggles this button: its row on the hotkey screen and its binding.</summary>
    internal required Keybind<SomeSplitButtonsModuleSettings> Keybind { get; init; }

    /// <summary>
    ///     Dialog id of the line that fades in under this feature's mod-menu row, or null for the
    ///     features whose name says everything.
    /// </summary>
    internal string MenuDescriptionId { get; init; }

    /// <summary>Dialog id of this feature's button in the pause menu.</summary>
    internal required string ButtonLabelId { get; init; }

    /// <summary>The <see cref="SplitActions"/> string this feature's interop events carry.</summary>
    internal required string InteropAction { get; init; }

    /// <summary>
    ///     The vanilla pause-menu button whose presence says this split belongs in this menu. A
    ///     gate, never an anchor: where the button goes is <see cref="Slot"/>'s business.
    /// </summary>
    internal required string AnchorDialogId { get; init; }

    /// <summary>
    ///     Which entry this button must be, counted the way a player counts: in Down presses from
    ///     where the cursor opens. <see cref="LAST"/> for the end of the menu.
    /// </summary>
    internal required int Slot { get; init; }

    /// <summary>A Slot meaning "after everything", however long the menu turns out to be.</summary>
    // int.MaxValue rather than a sentinel with its own branch: the slot walk already falls through
    // to the end when there are fewer reachable entries than asked for.
    internal const int LAST = int.MaxValue;

    /// <summary>Whether a missing anchor is worth warning about even in a minimal pause menu.</summary>
    internal bool WarnsWhenAnchorMissingInMinimal { get; init; }

    /// <summary>
    ///     Anything beyond the setting that decides whether the button appears, or null for always.
    /// </summary>
    internal Func<Level, bool> Available { get; init; }

    /// <summary>Dialog id of the description that eases in under the button.</summary>
    // Deferred: two of the three pick between two entries by a setting or by the chapter, and that
    // choice belongs to the moment the menu is built.
    internal required Func<string> DescriptionId { get; init; }

    /// <summary>The frame count that description quotes.</summary>
    internal required Func<int> DescriptionFrames { get; init; }

    /// <summary>
    ///     What pressing the button does. Returns the terminal interop stage, or null when the
    ///     feature emits its own — Return to Map's confirmation prompt owns the outcome from the
    ///     moment it opens.
    /// </summary>
    internal required Func<Level, TextMenu, string> Press { get; init; }
    internal required Func<bool> Enabled { get; init; }
    internal required Action<bool> SetEnabled { get; init; }
    internal required Action Reset { get; init; }
    internal required Action<Level> Update { get; init; }

    /// <summary>
    ///     Maintains and releases whatever vanilla state this feature holds between frames. Runs
    ///     every frame regardless of any setting. Null for features that hold none.
    /// </summary>
    // The asymmetry with Update is the point: Update stops when the button is switched off, but a
    // borrowed vanilla flag dropped half-held leaves a state only this code knows how to undo.
    internal Action<Level> UpdateHold { get; init; }

    /// <summary>
    ///     Releases whatever vanilla state this feature holds, on the frame SpeedrunTool is about to
    ///     clone the level. Runs regardless of any setting, for <see cref="UpdateHold"/>'s reason.
    ///     Null for features that hold none.
    /// </summary>
    internal Action<Level> BeforeSaveState { get; init; }

    /// <summary>
    ///     Recomputes whatever this feature derives from the chapter it is in. Null for the features
    ///     that derive nothing.
    /// </summary>
    internal Action<Level> OnLevelKnown { get; init; }

    /// <summary>
    ///     Turns the button on or off: writes the setting, disarms the timer, and refreshes what the
    ///     feature derives from the chapter it is in.
    /// </summary>
    // Deliberately does not save settings: Everest writes them when the mod menu closes, and the
    // hotkey path saves for itself because nothing closes on its behalf.
    internal void Toggle(bool enabled) {
        SetEnabled(enabled);
        Reset();
        // Outside a level there is no chapter to test; the next Level_OnLoadingThread does it.
        if (enabled && Engine.Scene is Level level) OnLevelKnown?.Invoke(level);
    }
}

/// <summary>
///     The set of split buttons. Adding a fourth means adding one entry here and nothing else.
/// </summary>
// The point is not brevity: a missing entry here is impossible to write rather than easy to notice.
// Two separate defects were each one manager missing from a hand-written list, and neither crashed
// — the symptom is a timer left armed that splits on its own later, invisible until it costs a run.
internal static class SplitFeatures {
    internal static readonly SplitFeature SaveAndQuit = new() {
        NameId = DialogIds.EnableSaveAndQuitSplitButtonId,
        Keybind = new(DialogIds.ToggleSaveQuitKeyId, nameof(SomeSplitButtonsModuleSettings.ButtonToggleSaveQuit)),
        Enabled = () => SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton,
        SetEnabled = value => SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton = value,
        Reset = SaveAndQuitTimer.Reset,
        Update = SaveAndQuitTimer.Update,
        ButtonLabelId = DialogIds.SaveAndQuitSplitButtonId,
        InteropAction = SplitActions.SaveAndQuit,
        AnchorDialogId = DialogIds.VanillaPauseSaveQuitId,
        // Two Down presses, matching vanilla: it greys Retry out during a wake-up and after a heart
        // or a cassette, which are the moments this button is for, and its own Save and Quit is the
        // third reachable entry there.
        Slot = 2,
        DescriptionId = () => SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter
            ? DialogIds.SQButtonReenterDesc
            : DialogIds.SQButtonDesc,
        DescriptionFrames = () => SplitTimings.WIPE_FADEOUT_FRAMES,
        Press = (level, _) =>
            SaveAndQuitTimer.Press(level) ? SplitStages.Confirmed : SplitStages.Refused,
        // The only feature that borrows a vanilla flag: level.TimerStopped, held frame by frame
        // between the split and the moment the clock would restart on its own.
        UpdateHold = SaveAndQuitTimer.UpdateHold,
        BeforeSaveState = SaveAndQuitTimer.ReleaseHoldForSaveState,
    };

    internal static readonly SplitFeature SkipCutscene = new() {
        NameId = DialogIds.EnableSkipCutsceneSplitButtonId,
        Keybind = new(DialogIds.ToggleSkipCutsceneKeyId, nameof(SomeSplitButtonsModuleSettings.ButtonToggleSkipCutscene)),
        MenuDescriptionId = DialogIds.EnableSkipCutsceneSplitButtonDescId,
        Enabled = () => SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton,
        SetEnabled = value => SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton = value,
        Reset = SkipCutsceneTimer.Reset,
        Update = SkipCutsceneTimer.Update,
        OnLevelKnown = level => SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex),
        ButtonLabelId = DialogIds.SkipCutsceneSplitButtonId,
        InteropAction = SplitActions.SkipCutscene,
        AnchorDialogId = DialogIds.VanillaPauseSkipCutsceneId,
        Slot = 1,
        // Both need InCutscene, so the vanilla button is always there when this one is: its absence
        // is a conflict with another mod and always worth saying.
        WarnsWhenAnchorMissingInMinimal = true,
        Available = level => level.endingChapterAfterCutscene && !SkipCutsceneTimer.Hidden,
        DescriptionId = () => SkipCutsceneTimer.InPrologue
            ? DialogIds.SCSPrologueButtonDesc
            : DialogIds.SCSButtonDesc,
        DescriptionFrames = () => SkipCutsceneTimer.FadeoutFrames,
        Press = (level, _) => {
            SkipCutsceneTimer.Press(level);
            return SplitStages.Confirmed;
        },
    };

    internal static readonly SplitFeature ReturnToMap = new() {
        NameId = DialogIds.EnableReturnToMapSplitButtonId,
        Keybind = new(DialogIds.ToggleReturnToMapKeyId, nameof(SomeSplitButtonsModuleSettings.ButtonToggleReturnToMap)),
        Enabled = () => SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton,
        SetEnabled = value => SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton = value,
        Reset = ReturnToMapTimer.Reset,
        Update = ReturnToMapTimer.Update,
        ButtonLabelId = DialogIds.ReturnToMapSplitButtonId,
        InteropAction = SplitActions.ReturnToMap,
        AnchorDialogId = DialogIds.VanillaPauseReturnId,
        // Reached by holding Down rather than by counting, so the requirement is the end of the menu
        // and not a distance from vanilla Return to Map — which is merely what usually sits there.
        Slot = SplitFeature.LAST,
        DescriptionId = () => DialogIds.RTMButtonDesc,
        DescriptionFrames = () => SplitTimings.WIPE_FADEOUT_FRAMES,
        Press = (level, pauseMenu) => {
            ReturnToMapTimer.Press(level, pauseMenu);
            return null;
        },
        // Borrows level.TimerStopped between the split and the checkpoint load, for the same reason
        // Save and Quit does and with the same outside-the-gates rule.
        UpdateHold = ReturnToMapReentry.UpdateHold,
        BeforeSaveState = ReturnToMapReentry.ReleaseHoldForSaveState,
    };

    /// <summary>
    ///     Iteration order is Save and Quit, Skip Cutscene, Return to Map, and it is the order the
    ///     split frame counts were measured through. The managers do not touch each other, so
    ///     nothing is known to depend on it — but a reorder invalidates those measurements.
    /// </summary>
    internal static readonly SplitFeature[] All = { SaveAndQuit, SkipCutscene, ReturnToMap };

    /// <summary>The same three, in the order a player meets them in the pause menu.</summary>
    // Every list the player reads uses this one, because the pause menu is the order they learn by
    // muscle memory. `All` keeps the update order above, which is measured rather than chosen.
    //
    // Ascending by Slot, and it has to stay that way: PauseMenuButtons inserts in this order and
    // each insertion is measured against the menu as it stands, so a lower slot inserted after a
    // higher one pushes the higher one down.
    internal static readonly SplitFeature[] InMenuOrder = { SkipCutscene, SaveAndQuit, ReturnToMap };

    /// <summary>Disarms every split timer, enabled or not.</summary>
    // Unconditional on purpose: a disabled feature can still be armed, because arming is not gated
    // on the setting — only Update is. Gating this leaves an armed timer that splits on its own.
    internal static void ResetAll() {
        foreach (SplitFeature feature in All) feature.Reset();
    }

    /// <summary>
    ///     Releases every borrowed vanilla flag, enabled or not, before SpeedrunTool clones a level.
    /// </summary>
    internal static void BeforeSaveStateAll(Level level) {
        foreach (SplitFeature feature in All) feature.BeforeSaveState?.Invoke(level);
    }

    /// <summary>Tells every feature which chapter it is now in.</summary>
    internal static void RefreshAll(Level level) {
        foreach (SplitFeature feature in All) feature.OnLevelKnown?.Invoke(level);
    }
}
