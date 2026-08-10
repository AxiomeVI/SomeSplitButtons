using static Celeste.TextMenu;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
public class SaveAndQuitSplitButton : Button {
    public SaveAndQuitSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }
    
    public static void PressedHandler(Level level) {
        if (level == null) return;
        SaveAndQuitTimer.HandleButtonPressed();
        level.Unpause();
        if (SomeSplitButtonsModule.Settings.SaveAndQuitAndRetry)
        {
            level.Tracker.GetEntity<Player>()?.Die(Vector2.Zero);
        }
    }
}
