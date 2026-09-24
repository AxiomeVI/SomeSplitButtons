using Celeste.Mod.SomeSplitButtons.Splits;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>A split button in the pause menu, and the feature it belongs to.</summary>
// The feature is carried rather than the label, because two buttons can share a label and the test
// probe has to tell the three apart after the menu is built.
internal class SplitButton : TextMenu.Button {
    internal SplitFeature Feature { get; }

    internal SplitButton(SplitFeature feature, string label) : base(label) {
        Feature = feature;
        ConfirmSfx = SFX.ui_main_message_confirm;
    }
}
