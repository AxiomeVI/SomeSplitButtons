using Celeste.Mod.SomeSplitButtons.Interop;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
/// Mirrors the vanilla Return to Map confirmation prompt, so practicing the split takes the same
/// menu inputs as the real thing.
/// </summary>
public class ReturnToMapSplitConfirmMenu : TextMenu {
    private bool finished;

    public ReturnToMapSplitConfirmMenu(Level level, TextMenu pauseMenu) {
        SplitEvents.Emit(SplitActions.ReturnToMap, SplitStages.Opened);

        // Vanilla adds a hint entity for the return prompt (and not for restart). This is that hint
        // with its caption replaced — see ReturnToMapSplitHint for why the vanilla one would lie.
        ReturnToMapSplitHint returnHint = new();
        level.Add(returnHint);

        // Same as vanilla's prompt: no scrolling, and lifted 100px above centre.
        AutoScroll = false;
        Position = new Vector2(Engine.Width / 2f, Engine.Height / 2f - 100f);

        OnCancel = () => {
            Finish(SplitStages.Cancelled);
            Close();
            Audio.Play(SFX.ui_main_button_back);
        };
        OnESC = OnPause = () => {
            Finish(SplitStages.Cancelled);
            Close();
            level.Unpause();
        };
        OnClose = () => {
            Finish(SplitStages.Cancelled);
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
            Finish(ReturnToMapTimer.HandleButtonPressed() ? SplitStages.Confirmed : SplitStages.Refused);
            Close();
            level.Unpause();
        });

        Button cancelButton = new(Dialog.Clean(DialogIds.VanillaReturnCancelId));
        cancelButton.Pressed(() => OnCancel());

        Add(confirmButton);
        Add(cancelButton);
    }

    // The two ways out that bypass Close(): something else removing the entity, and the scene ending
    // under it. A press that opened this menu is owed a terminal stage either way.
    public override void Removed(Scene scene) {
        Finish(SplitStages.Cancelled);
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene) {
        Finish(SplitStages.Cancelled);
        base.SceneEnd(scene);
    }

    /// <summary>Emits the terminal stage, once: whichever way out comes first names the outcome.</summary>
    private void Finish(string stage) {
        if (finished) return;
        finished = true;
        SplitEvents.Emit(SplitActions.ReturnToMap, stage);
    }
}
