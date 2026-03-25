using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SomeSplitButtons.UI;

[Tracked]
internal class KeybindConfigUi : TextMenu {
    private enum Slot {
        SaveQuitKeyboard, SaveQuitController,
        SkipCutsceneKeyboard, SkipCutsceneController
    }

    private bool _closing;
    private float _inputDelay;
    private bool _remapping;
    private float _remappingEase;
    private Slot _remappingSlot;
    private float _timeout;

    private bool IsRemappingKeyboard => _remappingSlot is Slot.SaveQuitKeyboard or Slot.SkipCutsceneKeyboard;
    private string RemappingLabel => Dialog.Clean(
        _remappingSlot is Slot.SaveQuitKeyboard or Slot.SaveQuitController
            ? DialogIds.ToggleSaveQuitKeyId
            : DialogIds.ToggleSkipCutsceneKeyId);

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
        var s = SomeSplitButtonsModule.Settings;

        Add(new Header(Dialog.Clean(DialogIds.KeybindConfigId)));

        Add(new SubHeader(Dialog.Clean(DialogIds.KeyConfigTitle)));
        Add(new Setting(Dialog.Clean(DialogIds.ToggleSaveQuitKeyId), s.ButtonToggleSaveQuit.Keys)
            .Pressed(() => StartRemap(Slot.SaveQuitKeyboard)));
        Add(new Setting(Dialog.Clean(DialogIds.ToggleSkipCutsceneKeyId), s.ButtonToggleSkipCutscene.Keys)
            .Pressed(() => StartRemap(Slot.SkipCutsceneKeyboard)));

        Add(new SubHeader(Dialog.Clean(DialogIds.BtnConfigTitle)));
        Add(new Setting(Dialog.Clean(DialogIds.ToggleSaveQuitKeyId), s.ButtonToggleSaveQuit.Buttons)
            .Pressed(() => StartRemap(Slot.SaveQuitController)));
        Add(new Setting(Dialog.Clean(DialogIds.ToggleSkipCutsceneKeyId), s.ButtonToggleSkipCutscene.Buttons)
            .Pressed(() => StartRemap(Slot.SkipCutsceneController)));

        if (index >= 0) Selection = index;
    }

    private void StartRemap(Slot slot) {
        _remapping = true;
        _remappingSlot = slot;
        _timeout = 5f;
        Focused = false;
    }

    private void ApplyRemap<T>(T input, List<T> list) {
        _remapping = false;
        _inputDelay = 0.25f;
        if (!list.Remove(input)) list.Add(input);
        SomeSplitButtonsModule.Instance.SaveSettings();
        Reload(Selection);
    }

    private void ApplyRemap(Keys key) {
        var list = _remappingSlot is Slot.SaveQuitKeyboard
            ? SomeSplitButtonsModule.Settings.ButtonToggleSaveQuit.Keys
            : SomeSplitButtonsModule.Settings.ButtonToggleSkipCutscene.Keys;
        ApplyRemap(key, list);
    }

    private void ApplyRemap(Buttons button) {
        var list = _remappingSlot is Slot.SaveQuitController
            ? SomeSplitButtonsModule.Settings.ButtonToggleSaveQuit.Buttons
            : SomeSplitButtonsModule.Settings.ButtonToggleSkipCutscene.Buttons;
        ApplyRemap(button, list);
    }

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
            } else if (IsRemappingKeyboard) {
                Keys[] pressed = MInput.Keyboard.CurrentState.GetPressedKeys();
                if (pressed?.LastOrDefault() is { } k && MInput.Keyboard.Pressed(k))
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

        if (IsRemappingKeyboard || Input.GuiInputController()) {
            ActiveFont.Draw(
                Dialog.Clean(DialogIds.KeybindComboSubId),
                pos + new Vector2(0f, -32f),
                new Vector2(0.5f, 2f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(_remappingEase));
            ActiveFont.Draw(
                Dialog.Clean(IsRemappingKeyboard ? DialogIds.KeyConfigChanging : DialogIds.BtnConfigChanging),
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
