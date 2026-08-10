using System;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplitManager;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SomeSplitButtons.UI;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     One split button, described uniformly enough that callers can loop over the set instead of
///     naming each one.
/// </summary>
// Delegates rather than an interface, because the three managers are static classes holding
// process-wide state — that is deliberate, and wrapping each in an instance just to satisfy an
// interface would add a type per manager without adding a capability.
//
// Every delegate that touches Settings is deferred: this table is built at type load, and
// SomeSplitButtonsModule.Settings needs Instance._Settings, which Everest fills in later.
internal sealed class SplitFeature {
    /// <summary>Dialog id of the button's name, used when a hotkey announces the toggle.</summary>
    internal required string NameId { get; init; }
    internal required Func<bool> Enabled { get; init; }
    internal required Action<bool> SetEnabled { get; init; }
    internal required Func<ButtonBinding> Binding { get; init; }
    internal required Action Reset { get; init; }
    internal required Action<Level> Update { get; init; }

    /// <summary>
    ///     Maintains and releases whatever vanilla state this feature holds between frames. Runs
    ///     every frame regardless of any setting. Null for features that hold none.
    /// </summary>
    // The asymmetry with Update is the point. Update is the mod's own business and stops when the
    // button is switched off; a hold is a vanilla flag the mod borrowed, and dropping it half-held
    // leaves the game in a state only this code knows how to undo.
    internal Action<Level> UpdateHold { get; init; }

    /// <summary>
    ///     Recomputes whatever this feature derives from the chapter it is in. Null for the features
    ///     that derive nothing.
    /// </summary>
    internal Action<Level> OnLevelKnown { get; init; }

    /// <summary>Built in <c>Load()</c>, once Settings exist.</summary>
    internal ComboHotkey Hotkey;

    /// <summary>
    ///     Turns the button on or off: writes the setting, disarms the timer, and refreshes what the
    ///     feature derives from the chapter it is in.
    /// </summary>
    // Both ways of toggling a split button — the mod menu and the hotkey — used to spell these three
    // steps out separately, which is two copies of one rule and a chance for them to drift.
    //
    // It deliberately does not save settings. The mod menu does not either, and that is on purpose:
    // Everest writes them when the menu closes. The hotkey path saves for itself, since nothing
    // closes on its behalf.
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
// ⚠️ This list exists because the alternative had already failed twice. Before it, nine call sites
// wrote the three managers out longhand, and the engine audit records two defects that were each one
// manager missing from one of those lists — a disabled feature keeping its state across a level
// load, and Level_OnLevelExit not resetting SkipCutsceneTimer. Neither crashes: the symptom is a
// timer left armed that splits on its own later, which is invisible until it happens in a run.
//
// So the point is not brevity. It is that a missing entry is now impossible to write rather than
// merely easy to notice.
internal static class SplitFeatures {
    internal static readonly SplitFeature SaveAndQuit = new() {
        NameId = DialogIds.EnableSaveAndQuitSplitButtonId,
        Enabled = () => SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton,
        SetEnabled = value => SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton = value,
        Binding = () => SomeSplitButtonsModule.Settings.ButtonToggleSaveQuit,
        Reset = SaveAndQuitTimer.Reset,
        Update = SaveAndQuitTimer.Update,
        // The only feature that borrows a vanilla flag: level.TimerStopped, held frame by frame
        // between the split and the moment the clock would restart on its own.
        UpdateHold = SaveAndQuitTimer.UpdateHold,
    };

    internal static readonly SplitFeature SkipCutscene = new() {
        NameId = DialogIds.EnableSkipCutsceneSplitButtonId,
        Enabled = () => SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton,
        SetEnabled = value => SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton = value,
        Binding = () => SomeSplitButtonsModule.Settings.ButtonToggleSkipCutscene,
        Reset = SkipCutsceneTimer.Reset,
        Update = SkipCutsceneTimer.Update,
        OnLevelKnown = level => SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex),
    };

    internal static readonly SplitFeature ReturnToMap = new() {
        NameId = DialogIds.EnableReturnToMapSplitButtonId,
        Enabled = () => SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton,
        SetEnabled = value => SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton = value,
        Binding = () => SomeSplitButtonsModule.Settings.ButtonToggleReturnToMap,
        Reset = ReturnToMapTimer.Reset,
        // The only manager whose Update needs no level; the parameter is dropped rather than added
        // to it, since nothing in there has a use for one.
        Update = _ => ReturnToMapTimer.Update(),
    };

    /// <summary>
    ///     ⚠️ Iteration order is Save and Quit, Skip Cutscene, Return to Map — the order
    ///     <c>Level_OnUpdate</c> ticked them in before this list existed. The managers do not touch
    ///     each other, so nothing is known to depend on it; it is preserved because the `.tas` suite
    ///     measures frame boundaries through these calls and a reorder is not worth re-measuring.
    /// </summary>
    internal static readonly SplitFeature[] All = { SaveAndQuit, SkipCutscene, ReturnToMap };

    /// <summary>
    ///     Disarms every split timer, enabled or not.
    /// </summary>
    // Unconditional on purpose: a disabled feature can still be armed, because HandleButtonPressed
    // is not gated on the setting — only Update is. Gating this is the P3 defect the Isolation/
    // tests exist for.
    internal static void ResetAll() {
        foreach (SplitFeature feature in All) feature.Reset();
    }

    /// <summary>
    ///     Tells every feature which chapter it is now in.
    /// </summary>
    internal static void RefreshAll(Level level) {
        foreach (SplitFeature feature in All) feature.OnLevelKnown?.Invoke(level);
    }
}
