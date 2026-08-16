using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SomeSplitButtons.UI;

[Tracked]
internal class KeybindConfigUi : TextMenu {
    // The action being remapped and the device it is being remapped for are kept apart. As one
    // enum the two multiply, so a fourth split button would mean eight members and eight more
    // branches in every selector below.
    private enum Slot { SaveQuit, SkipCutscene, ReturnToMap }

    private static readonly Slot[] AllSlots = { Slot.SaveQuit, Slot.SkipCutscene, Slot.ReturnToMap };

    private static ButtonBinding Binding(Slot slot) => slot switch {
        Slot.SaveQuit => SomeSplitButtonsModule.Settings.ButtonToggleSaveQuit,
        Slot.SkipCutscene => SomeSplitButtonsModule.Settings.ButtonToggleSkipCutscene,
        _ => SomeSplitButtonsModule.Settings.ButtonToggleReturnToMap,
    };

    private static string LabelId(Slot slot) => slot switch {
        Slot.SaveQuit => DialogIds.ToggleSaveQuitKeyId,
        Slot.SkipCutscene => DialogIds.ToggleSkipCutsceneKeyId,
        _ => DialogIds.ToggleReturnToMapKeyId,
    };

    private bool _closing;
    private float _inputDelay;
    private bool _remapping;
    private float _remappingEase;
    private Slot _remappingSlot;
    private bool _remappingKeyboard;
    private float _timeout;

    private string RemappingLabel => Dialog.Clean(LabelId(_remappingSlot));

    private static readonly Buttons[] AllButtons = {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftTrigger, Buttons.RightTrigger,
        Buttons.Back, Buttons.Start,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
    };

    public KeybindConfigUi() {
        Reload();
        OnESC = OnCancel = () => { Focused = false; _closing = true; };
        MinWidth = 600f;
        Position.Y = ScrollTargetY;
        Alpha = 0f;
    }

    private void Reload(int index = -1) {
        Clear();

        Add(new Header(Dialog.Clean(DialogIds.KeybindConfigId)));

        // Both sections walk AllSlots, so a keyboard row cannot exist without its controller
        // counterpart — the pair used to be written out by hand.
        Add(new SubHeader(Dialog.Clean(DialogIds.KeyConfigTitle)));
        foreach (Slot slot in AllSlots) {
            Add(new Setting(Dialog.Clean(LabelId(slot)), Binding(slot).Keys)
                .Pressed(() => StartRemap(slot, keyboard: true)));
        }

        Add(new SubHeader(Dialog.Clean(DialogIds.BtnConfigTitle)));
        foreach (Slot slot in AllSlots) {
            Add(new Setting(Dialog.Clean(LabelId(slot)), Binding(slot).Buttons)
                .Pressed(() => StartRemap(slot, keyboard: false)));
        }

        if (index >= 0) Selection = index;
    }

    private void StartRemap(Slot slot, bool keyboard) {
        _remapping = true;
        _remappingSlot = slot;
        _remappingKeyboard = keyboard;
        _timeout = 5f;
        Focused = false;
    }

    private void ApplyRemap<T>(T input, List<T> list) {
        _remapping = false;
        _inputDelay = 0.25f;
        if (!list.Remove(input)) list.Add(input);
        SomeSplitButtonsModule.Instance.SaveSettings();
        SomeSplitButtonsModule.ResyncHotkeys();
        Reload(Selection);
    }

    private void ApplyRemap(Keys key) => ApplyRemap(key, Binding(_remappingSlot).Keys);

    private void ApplyRemap(Buttons button) => ApplyRemap(button, Binding(_remappingSlot).Buttons);

    public override void Update() {
        base.Update();

        if (_inputDelay > 0f && !_remapping) {
            _inputDelay -= Engine.DeltaTime;
            if (_inputDelay <= 0f) Focused = true;
        }

        _remappingEase = Calc.Approach(_remappingEase, _remapping ? 1f : 0f, Engine.DeltaTime * 4f);

        if (_remappingEase > 0.5f && _remapping) {
            if (Input.ESC.Pressed || Input.MenuCancel || _timeout <= 0f) {
                Input.ESC.ConsumePress();
                _remapping = false;
                Focused = true;
            } else if (_remappingKeyboard) {
                // Keys.None is skipped rather than bound. It is what FNA hands back for a key absent
                // from its SDL→XNA table — AZERTY's ")" is one — so it stands for "some key we have
                // no name for", not for a key the player chose. Bound, it would match every such key.
                Keys k = MInput.Keyboard.CurrentState.GetPressedKeys().LastOrDefault(key => key != Keys.None);
                if (k != Keys.None && MInput.Keyboard.Pressed(k))
                    ApplyRemap(k);
            } else {
                var cur  = MInput.GamePads[Input.Gamepad].CurrentState;
                var prev = MInput.GamePads[Input.Gamepad].PreviousState;
                foreach (var btn in AllButtons)
                    if (cur.IsButtonDown(btn) && !prev.IsButtonDown(btn)) { ApplyRemap(btn); break; }
            }
            _timeout -= Engine.DeltaTime;
        }

        Alpha = Calc.Approach(Alpha, _closing ? 0f : 1f, Engine.DeltaTime * 8f);
        if (!_closing || Alpha > 0f) return;

        OnClose?.Invoke();
        Close();
    }

    public override void Render() {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
        base.Render();
        if (_remappingEase <= 0f) return;

        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(_remappingEase));
        Vector2 pos = new Vector2(1920f, 1080f) * 0.5f;

        if (_remappingKeyboard || Input.GuiInputController()) {
            ActiveFont.Draw(
                Dialog.Clean(DialogIds.KeybindComboSubId),
                pos + new Vector2(0f, -32f),
                new Vector2(0.5f, 2f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(_remappingEase));
            ActiveFont.Draw(
                Dialog.Clean(_remappingKeyboard ? DialogIds.KeyConfigChanging : DialogIds.BtnConfigChanging),
                pos + new Vector2(0f, -8f),
                new Vector2(0.5f, 1f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(_remappingEase));
            ActiveFont.Draw(
                RemappingLabel,
                pos + new Vector2(0f, 8f),
                new Vector2(0.5f, 0f), Vector2.One * 2f,
                Color.White * Ease.CubeIn(_remappingEase));
        } else {
            ActiveFont.Draw(
                Dialog.Clean(DialogIds.BtnConfigNoController),
                pos, new Vector2(0.5f, 0.5f), Vector2.One,
                Color.White * Ease.CubeIn(_remappingEase));
        }
    }
}
