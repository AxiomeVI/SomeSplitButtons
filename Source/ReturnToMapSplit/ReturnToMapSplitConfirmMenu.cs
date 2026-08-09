using Celeste.Mod.SomeSplitButtons.ReturnToMapSplitManager;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplitButton;

/// <summary>
/// Mirrors the vanilla Return to Map confirmation prompt, so practicing the split takes the same
/// menu inputs as the real thing.
/// </summary>
public class ReturnToMapSplitConfirmMenu : TextMenu {
    public ReturnToMapSplitConfirmMenu(Level level, TextMenu pauseMenu) {
        OnCancel = () => {
            Close();
            Audio.Play(SFX.ui_main_button_back);
        };
        OnESC = OnPause = () => {
            Close();
            level.Unpause();
        };
        OnClose = () => {
            pauseMenu.Focused = true;
            pauseMenu.Alpha = 1f;
        };

        Add(new Header(Dialog.Clean(DialogIds.ReturnToMapSplitMenuHeaderId)));

        Button confirmButton = new(Dialog.Clean(DialogIds.VanillaReturnContinueId));
        confirmButton.Pressed(() => {
            ReturnToMapTimer.HandleButtonPressed();
            Close();
            level.Unpause();
        });

        Button cancelButton = new(Dialog.Clean(DialogIds.VanillaReturnCancelId));
        cancelButton.Pressed(() => OnCancel());

        Add(confirmButton);
        Add(cancelButton);
    }
}
