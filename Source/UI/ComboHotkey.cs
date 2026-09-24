using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>One frame of the input a hotkey is allowed to look at.</summary>
// Both readings come from MInput, on `Input.Gamepad` — the pad index the player chose, and the
// one KeybindConfigUi records a binding against. Taking the first *connected* pad instead means
// that with two controllers plugged in a button can be bound on one and watched on the other, which
// is a hotkey that simply never fires.
internal readonly record struct InputSnapshot(KeyboardState Keyboard, GamePadState Pad) {
    internal static InputSnapshot Current() => new(
        MInput.Keyboard.CurrentState,
        MInput.GamePads[Input.Gamepad].CurrentState
    );
}

/// <summary>
///     Wraps a ButtonBinding and detects combo presses — every bound input held at once.
///     Rising-edge only: <see cref="Pressed"/> is true for exactly one frame per activation.
/// </summary>
internal class ComboHotkey(ButtonBinding binding) {
    private bool lastCheck;

    /// <summary>The primary-constructor parameter, reachable from the static pass below.</summary>
    private ButtonBinding Binding => binding;

    /// <summary>The modifiers a binding has to name before it may fire while one of them is held.</summary>
    // Modifiers only — never "nothing else is held". These hotkeys are pressed mid-run with
    // movement, jump and dash down, so a rule about every held key would switch them off in exactly
    // the situation they exist for.
    private static readonly Keys[] Modifiers = {
        Keys.LeftShift, Keys.RightShift,
        Keys.LeftControl, Keys.RightControl,
        Keys.LeftAlt, Keys.RightAlt,
    };

    // The Count checks are there because "every bound input is held" over an empty set is true. No
    // `!= default` guard beyond that: a state with nothing held already answers false to every
    // IsKeyDown and IsButtonDown.
    //
    // Indexed loops rather than LINQ: IsKeyDown and IsButtonDown are instance methods on structs, so
    // a method group passed to All boxes the state and allocates a delegate — three hotkeys times
    // two devices, every frame, for the whole session.
    private bool IsDown(in InputSnapshot input) {
        if (binding.Keys.Count > 0 && AllHeld(binding.Keys, input.Keyboard) && NoUnboundModifier(input.Keyboard))
            return true;
        if (binding.Buttons.Count > 0 && AllHeld(binding.Buttons, input.Pad))
            return true;
        return false;
    }

    private static bool AllHeld(List<Keys> keys, in KeyboardState keyboard) {
        for (int i = 0; i < keys.Count; i++) {
            if (!keyboard.IsKeyDown(keys[i])) return false;
        }
        return true;
    }

    private static bool AllHeld(List<Buttons> buttons, in GamePadState pad) {
        for (int i = 0; i < buttons.Count; i++) {
            if (!pad.IsButtonDown(buttons[i])) return false;
        }
        return true;
    }

    /// <summary>False when a modifier is held that this binding does not name.</summary>
    // What makes a binding exclusive against the longer combos built on top of it. "Every bound key
    // is held" is satisfied by any superset, so F alone also matches Ctrl+F.
    private bool NoUnboundModifier(in KeyboardState keyboard) {
        for (int i = 0; i < Modifiers.Length; i++) {
            if (keyboard.IsKeyDown(Modifiers[i]) && !binding.Keys.Contains(Modifiers[i])) return false;
        }
        return true;
    }

    public void Update(in InputSnapshot input) {
        bool current = IsDown(input);
        Pressed = !lastCheck && current;
        lastCheck = current;
    }

    /// <summary>
    ///     Swallows the edge of whatever is held right now, so the next Update sees no rising edge
    ///     until the combo has been released and pressed again.
    /// </summary>
    internal void Resync(in InputSnapshot input) {
        lastCheck = IsDown(input);
        Pressed = false;
    }

    public bool Pressed { get; private set; }

    /// <summary>
    ///     Clears the press of any hotkey whose binding is a strict subset of another hotkey firing
    ///     on the same frame.
    /// </summary>
    // Covers what NoUnboundModifier cannot: two bindings differing by something that is not a
    // modifier — LB against LB+RB on a pad. Whichever names more inputs is the one the player meant.
    //
    // The firing set is read before anything is cleared. Suppressing in place would let the
    // survivor of three overlapping bindings depend on iteration order.
    internal static void SuppressSubsetPresses(ComboHotkey[] hotkeys) {
        Span<bool> firing = stackalloc bool[hotkeys.Length];
        bool any = false;
        for (int i = 0; i < hotkeys.Length; i++) {
            firing[i] = hotkeys[i].Pressed;
            any |= firing[i];
        }
        if (!any) return;

        for (int i = 0; i < hotkeys.Length; i++) {
            if (!firing[i]) continue;
            for (int j = 0; j < hotkeys.Length; j++) {
                if (i == j || !firing[j]) continue;
                if (!IsStrictSubset(hotkeys[i].Binding, hotkeys[j].Binding)) continue;
                hotkeys[i].Pressed = false;
                break;
            }
        }
    }

    // Per device, because the two sets are alternatives inside one binding: a keyboard combo says
    // nothing about which pad binding the player meant.
    private static bool IsStrictSubset(ButtonBinding smaller, ButtonBinding larger) =>
        IsStrictSubset(smaller.Keys, larger.Keys) || IsStrictSubset(smaller.Buttons, larger.Buttons);

    private static bool IsStrictSubset<T>(List<T> smaller, List<T> larger) {
        if (smaller.Count == 0 || smaller.Count >= larger.Count) return false;
        for (int i = 0; i < smaller.Count; i++) {
            if (!larger.Contains(smaller[i])) return false;
        }
        return true;
    }
}
