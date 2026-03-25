using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// Wraps a ButtonBinding and detects combo presses (all bound keys held simultaneously).
/// Rising-edge only: Pressed is true for exactly one frame when the combo activates.
internal class ComboHotkey(ButtonBinding binding) {
    private static KeyboardState _kbState;
    private static GamePadState _padState;

    private bool _lastCheck;

    internal static void UpdateStates() {
        _kbState = Keyboard.GetState();
        _padState = GetGamePadState();
    }

    private static GamePadState GetGamePadState() {
        for (int i = 0; i < 4; i++) {
            var state = GamePad.GetState((PlayerIndex) i);
            if (state.IsConnected) return state;
        }
        return default;
    }

    private bool IsDown() {
        if (binding.Keys.Count > 0 && _kbState != default && binding.Keys.All(_kbState.IsKeyDown))
            return true;
        if (binding.Buttons.Count > 0 && _padState != default && binding.Buttons.All(_padState.IsButtonDown))
            return true;
        return false;
    }

    public void Update() {
        bool current = IsDown();
        Pressed = !_lastCheck && current;
        _lastCheck = current;
    }

    public bool Pressed { get; private set; }
}
