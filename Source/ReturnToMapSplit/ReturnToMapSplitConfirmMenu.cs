using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
/// Mirrors the vanilla Return to Map confirmation prompt, so practicing the split takes the same
/// menu inputs as the real thing.
/// </summary>
public class ReturnToMapSplitConfirmMenu : TextMenu {
    public ReturnToMapSplitConfirmMenu(Level level, TextMenu pauseMenu) {
        // Vanilla adds a hint entity for the return prompt (and not for restart). This is that hint
        // with its caption replaced — see ReturnToMapSplitHint for why the vanilla one would lie.
        ReturnToMapSplitHint returnHint = new();
        level.Add(returnHint);

        // Same as vanilla's prompt: no scrolling, and lifted 100px above centre.
        AutoScroll = false;
        Position = new Vector2(Engine.Width / 2f, Engine.Height / 2f - 100f);

        OnCancel = () => {
            Close();
            Audio.Play(SFX.ui_main_button_back);
        };
        OnESC = OnPause = () => {
            Close();
            level.Unpause();
        };
        OnClose = () => {
            returnHint.RemoveSelf();
            pauseMenu.Focused = true;
            pauseMenu.Alpha = 1f;
            // Vanilla clears this before opening the prompt and gets it back from the Pause()
            // rebuild. Left true, holding the journal button hides the HUD during the prompt, which
            // Level.Update gates on `!Paused || !PauseMainMenuOpen`.
            level.PauseMainMenuOpen = true;
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
