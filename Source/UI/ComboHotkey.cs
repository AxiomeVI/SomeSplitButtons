using System.Linq;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>
///     One frame of the input a hotkey is allowed to look at.
/// </summary>
// Passed in rather than parked in two static fields filled by an external caller. The old shape had
// an unwritten contract — UpdateStates() before Update(), every time — that nothing checked and
// whose only symptom when broken was hotkeys answering one frame late.
//
// Both readings come from MInput, which is the pad Celeste itself is playing on: `Input.Gamepad` is
// the index the player chose, and KeybindConfigUi reads the same one when it records a binding. The
// old code took the *first connected* pad instead, looping 0→3, so with two controllers plugged in
// a button could be bound on one and watched on the other — a hotkey that simply never fires.
internal readonly record struct InputSnapshot(KeyboardState Keyboard, GamePadState Pad) {
    internal static InputSnapshot Current() => new(
        MInput.Keyboard.CurrentState,
        MInput.GamePads[Input.Gamepad].CurrentState
    );
}

/// Wraps a ButtonBinding and detects combo presses (all bound keys held simultaneously).
/// Rising-edge only: Pressed is true for exactly one frame when the combo activates.
internal class ComboHotkey(ButtonBinding binding) {
    private bool _lastCheck;

    // No `!= default` guard on either state. `All` over an empty set is true, which is why the Count
    // checks are here; but a state with nothing held already answers false to every IsKeyDown and
    // IsButtonDown, so testing it against default only added a branch to a path that is subtle
    // enough already.
    private bool IsDown(in InputSnapshot input) {
        if (binding.Keys.Count > 0 && binding.Keys.All(input.Keyboard.IsKeyDown))
            return true;
        if (binding.Buttons.Count > 0 && binding.Buttons.All(input.Pad.IsButtonDown))
            return true;
        return false;
    }

    public void Update(in InputSnapshot input) {
        bool current = IsDown(input);
        Pressed = !_lastCheck && current;
        _lastCheck = current;
    }

    /// Swallows the edge of whatever is held right now, so the next Update sees no rising edge
    /// until the combo has been released and pressed again.
    internal void Resync(in InputSnapshot input) {
        _lastCheck = IsDown(input);
        Pressed = false;
    }

    public bool Pressed { get; private set; }
}
