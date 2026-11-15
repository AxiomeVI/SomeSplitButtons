using System;
using System.Reflection;
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
        PageComboHintId = DialogIds.KeybindPageComboId,
        ClearHintId = DialogIds.KeybindClearSubId,
        TimeoutFormatId = DialogIds.KeybindTimeoutId,
    };

    /// <summary>Whether a text box is reading the keyboard: a bare letter is being typed, not pressed.</summary>
    // Everest's TextInput.OnInput has a subscriber exactly while a box types: the Mod Options search,
    // map search, file naming, string and number entry, the console, and any mod's own box. Not
    // MInput.Disabled, which a paused level resets and vanilla cutscenes set, and not a list of
    // screens, which misses a mod's own box. The event's handlers are private, hence the reflection.
    internal static bool TextInputHasFocus {
        get {
            if (TextInputHandlers?.GetValue(null) is not Delegate handlers) {
                lastHandlers = null;
                return false;
            }
            // A delegate is immutable, so the same one gives the same answer: no array per frame.
            if (!ReferenceEquals(handlers, lastHandlers)) {
                lastHandlers = handlers;
                lastTyping = Array.Exists(handlers.GetInvocationList(), handler => !IsAlwaysSubscribed(handler));
            }
            return lastTyping;
        }
    }

    private static Delegate lastHandlers;
    private static bool lastTyping;

    // ⚠️ ImGuiHelper subscribes once for the whole session, so counting it would silence every hotkey
    // for good. CelesteTAS skips the same handler for its own hotkeys (Hotkeys.UpdateMeta).
    private static bool IsAlwaysSubscribed(Delegate handler) =>
        handler.Target?.GetType().FullName == "Celeste.Mod.ImGuiHelper.ImGuiRenderer+<>c";

    // Null if a later Everest renames the field: the hotkeys then stay live, as before the gate.
    internal static readonly FieldInfo TextInputHandlers =
        typeof(TextInput).GetField("_OnInput", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
}
