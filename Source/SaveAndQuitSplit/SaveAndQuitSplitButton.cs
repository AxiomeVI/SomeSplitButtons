using static Celeste.TextMenu;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitButton;
public class MainSaveAndQuitSplitButton : Button {
    public MainSaveAndQuitSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }
    
    public void PressedHandler(Level level) {
        if (level == null) return;
        SaveAndQuitTimer.HandleButtonPressed();
        level.Unpause();
        if (SomeSplitButtonsModule.Settings.SaveAndQuitAndRetry)
        {
            level.Tracker.GetEntity<Player>()?.Die(Vector2.Zero);
        }
    }
}
