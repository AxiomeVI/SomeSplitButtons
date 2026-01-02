using static Celeste.TextMenu;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;

namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitButton;
public class MainSkipCutsceneSplitButton : Button {
    public MainSkipCutsceneSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }
    
    public void PressedHandler(Level level) {
        if (level == null) return;
        StaticSkipCutsceneSplitManager.HandleButtonPressed();
        SkipCutsceneTimer.HandleTimerButtonPressed(level.Session.Area.ChapterIndex);
        level.Unpause();
    }
}