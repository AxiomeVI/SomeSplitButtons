using Celeste.Mod.SomeSplitButtons.Splits;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.UI;

// [Tracked] so the hotkey loop can find this menu and stop polling while it is recording a binding.
// Nothing else queries the tracker for it.
[Tracked]
internal class KeybindConfigUi : TextMenu {
    // The feature being remapped and the device it is being remapped for are kept apart. Rolled into
    // one value the two multiply, so a fourth split button would mean eight cases everywhere below.
    private bool closing;
    private float inputDelay;
    private bool remapping;
    private float remappingEase;
    private SplitFeature remappingFeature;
    private bool remappingKeyboard;
    private float timeout;

    private const float REMAP_TIMEOUT = 5f;

    private string RemappingLabel => Dialog.Clean(remappingFeature.HotkeyLabelId);

    /// <summary>True while this menu is waiting for the key or button to bind.</summary>
    internal bool Remapping => remapping;

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
        OnESC = OnCancel = () => { Focused = false; closing = true; };
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
        foreach (SplitFeature feature in SplitFeatures.InMenuOrder) {
            Add(new Setting(Dialog.Clean(feature.HotkeyLabelId), feature.Binding().Keys)
                .Pressed(() => StartRemap(feature, keyboard: true)));
        }

        Add(new SubHeader(Dialog.Clean(DialogIds.BtnConfigTitle)));
        foreach (SplitFeature feature in SplitFeatures.InMenuOrder) {
            Add(new Setting(Dialog.Clean(feature.HotkeyLabelId), feature.Binding().Buttons)
                .Pressed(() => StartRemap(feature, keyboard: false)));
        }

        if (index >= 0) Selection = index;
    }

    private void StartRemap(SplitFeature feature, bool keyboard) {
        remapping = true;
        remappingFeature = feature;
        remappingKeyboard = keyboard;
        timeout = REMAP_TIMEOUT;
        Focused = false;
    }

    private void ApplyRemap<T>(T input, List<T> list) {
        remapping = false;
        inputDelay = 0.25f;
        if (!list.Remove(input)) list.Add(input);
        SomeSplitButtonsModule.Instance.SaveSettings();
        SomeSplitButtonsModule.ResyncHotkeys();
        Reload(Selection);
    }

    private void ApplyRemap(Keys key) => ApplyRemap(key, remappingFeature.Binding().Keys);

    private void ApplyRemap(Buttons button) => ApplyRemap(button, remappingFeature.Binding().Buttons);

    public override void Update() {
        base.Update();

        // RawDeltaTime throughout, never DeltaTime. This is a menu: its timers measure how long the
        // player has been looking at it, and DeltaTime carries Engine.TimeRate and Assist Mode's game
        // speed — at 50% the five-second timeout below became ten real seconds.
        if (inputDelay > 0f && !remapping) {
            inputDelay -= Engine.RawDeltaTime;
            if (inputDelay <= 0f) Focused = true;
        }

        remappingEase = Calc.Approach(remappingEase, remapping ? 1f : 0f, Engine.RawDeltaTime * 4f);

        if (remappingEase > 0.5f && remapping) {
            // Escape and the timeout only — never Input.MenuCancel. Cancelling on it made the
            // player's own cancel input unbindable: B on a controller, and whatever the keyboard
            // cancel is, were consumed as "stop recording" and could never be recorded. Vanilla's
            // and Everest's remap screens take Escape or the timeout for the same reason.
            if (Input.ESC.Pressed || timeout <= 0f) {
                Input.ESC.ConsumePress();
                remapping = false;
                Focused = true;
            } else if (remappingKeyboard) {
                // The first *newly pressed* key, not the last held one. GetPressedKeys comes back
                // in ascending Keys order, so LastOrDefault named the highest-valued key held — and
                // modifiers are 160..165, above every letter, digit, arrow and F-key. With Shift
                // still down from the confirm press, pressing C named LeftShift, which was not
                // pressed this frame, so nothing bound and the overlay timed out silently after five
                // seconds. Mirrors the controller branch below, which was always written this way.
                //
                // Keys.None is skipped rather than bound. It is what FNA hands back for a key absent
                // from its SDL→XNA table — AZERTY's ")" is one — so it stands for "some key we have
                // no name for", not for a key the player chose. Bound, it would match every such key.
                foreach (Keys key in MInput.Keyboard.CurrentState.GetPressedKeys()) {
                    if (key == Keys.None || !MInput.Keyboard.Pressed(key)) continue;
                    ApplyRemap(key);
                    break;
                }
            } else {
                var cur  = MInput.GamePads[Input.Gamepad].CurrentState;
                var prev = MInput.GamePads[Input.Gamepad].PreviousState;
                foreach (var btn in AllButtons)
                    if (cur.IsButtonDown(btn) && !prev.IsButtonDown(btn)) { ApplyRemap(btn); break; }
            }
            timeout -= Engine.RawDeltaTime;
        }

        Alpha = Calc.Approach(Alpha, closing ? 0f : 1f, Engine.RawDeltaTime * 8f);
        if (!closing || Alpha > 0f) return;

        OnClose?.Invoke();
        Close();
    }

    public override void Render() {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
        base.Render();
        if (remappingEase <= 0f) return;

        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(remappingEase));
        Vector2 pos = new Vector2(1920f, 1080f) * 0.5f;

        if (remappingKeyboard || Input.GuiInputController()) {
            ActiveFont.Draw(
                Dialog.Clean(DialogIds.KeybindComboSubId),
                pos + new Vector2(0f, -32f),
                new Vector2(0.5f, 2f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(remappingEase));
            ActiveFont.Draw(
                Dialog.Clean(remappingKeyboard ? DialogIds.KeyConfigChanging : DialogIds.BtnConfigChanging),
                pos + new Vector2(0f, -8f),
                new Vector2(0.5f, 1f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(remappingEase));
            ActiveFont.Draw(
                RemappingLabel,
                pos + new Vector2(0f, 8f),
                new Vector2(0.5f, 0f), Vector2.One * 2f,
                Color.White * Ease.CubeIn(remappingEase));
            // The overlay used to close on its own after five seconds with nothing said, which reads
            // as the binding having failed rather than as a timeout. Ceiling, so the first thing the
            // player sees is the full five and the last whole second is not skipped.
            ActiveFont.Draw(
                string.Format(Dialog.Get(DialogIds.KeybindTimeoutId), (int) System.Math.Ceiling(timeout)),
                pos + new Vector2(0f, 96f),
                new Vector2(0.5f, 0f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(remappingEase));
        } else {
            ActiveFont.Draw(
                Dialog.Clean(DialogIds.BtnConfigNoController),
                pos, new Vector2(0.5f, 0.5f), Vector2.One,
                Color.White * Ease.CubeIn(remappingEase));
        }
    }
}
