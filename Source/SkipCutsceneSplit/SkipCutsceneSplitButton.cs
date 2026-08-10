using static Celeste.TextMenu;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;
public class SkipCutsceneSplitButton : Button {
    public SkipCutsceneSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }
    
    public static void PressedHandler(Level level) {
        if (level == null) return;
        SkipCutsceneTimer.HandleButtonPressed();
        level.Unpause();
    }
}