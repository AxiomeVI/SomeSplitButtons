using System;
using Celeste.Mod.CelesteHotkeys;
using Celeste.Mod.SomeSplitButtons.Splits;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>The split buttons' hotkeys, on the shared CelesteHotkeys layer.</summary>
// Built from the features rather than listed by hand, so a fourth split button gets its hotkey from
// its own SplitFeatures entry. The screen lists them in the pause menu's order.
//
// ⚠️ A binding here is a COMBO — every bound key held at once — not Everest's "any one key". Never
// read a binding's own Pressed: that is Everest's meaning, and it ignores every rule of this layer.
internal static class Hotkeys {
    internal static readonly Keybind<SomeSplitButtonsModuleSettings>[] All =
        Array.ConvertAll(SplitFeatures.InMenuOrder, feature => feature.Keybind);

    internal static readonly HotkeySet<SomeSplitButtonsModuleSettings> Set =
        new(() => SomeSplitButtonsModule.Settings, All);

    internal static readonly KeybindScreenText Text = new() {
        HeaderId = DialogIds.KeybindConfigId,
        ComboHintId = DialogIds.KeybindComboSubId,
        ClearHintId = DialogIds.KeybindClearSubId,
        TimeoutFormatId = DialogIds.KeybindTimeoutId,
    };
}
