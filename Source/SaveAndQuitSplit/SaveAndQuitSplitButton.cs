using static Celeste.TextMenu;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
public class MainSaveAndQuitSplitButton : Button {
    public MainSaveAndQuitSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }
    
    public void PressedHandler(Level level) {
        if (level == null) return;
        Timer.HandleTimerButtonPressed();
        level.Unpause();
    }
}
