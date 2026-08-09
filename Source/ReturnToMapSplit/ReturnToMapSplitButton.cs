using Monocle;
using static Celeste.TextMenu;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplitButton;

public class MainReturnToMapSplitButton : Button {
    public MainReturnToMapSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }

    /// <summary>
    /// Vanilla Return to Map asks for confirmation before leaving, so the split button does too:
    /// the pause menu steps aside and the confirmation menu takes focus.
    /// </summary>
    public static void PressedHandler(Level level, TextMenu pauseMenu) {
        if (level == null) return;

        ReturnToMapSplitConfirmMenu confirmMenu = new(level, pauseMenu);
        pauseMenu.Focused = false;
        pauseMenu.Alpha = 0f;
        level.Add(confirmMenu);
        level.OnEndOfFrame += () => level.Entities.UpdateLists();
    }
}
