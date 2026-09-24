using Celeste.Mod.SomeSplitButtons.Interop;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
/// Mirrors the vanilla Return to Map confirmation prompt, so practicing the split takes the same
/// menu inputs as the real thing.
/// </summary>
internal class ReturnToMapSplitConfirmMenu : TextMenu {
    private bool finished;

    internal ReturnToMapSplitConfirmMenu(Level level, TextMenu pauseMenu) {
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
            LeaveThePause(level, pauseMenu);
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
            LeaveThePause(level, pauseMenu);
        });

        Button cancelButton = new(Dialog.Clean(DialogIds.VanillaReturnCancelId));
        cancelButton.Pressed(() => OnCancel());

        Add(confirmButton);
        Add(cancelButton);
    }

    /// <summary>
    ///     Closes this prompt and the pause menu under it, and hands the level back to the player.
    /// </summary>
    // ⚠️ Not level.Unpause(), which is the *pause menu's* own exit and wrong for a prompt sitting on
    // top of it. Unpause hands the first TextMenu in the scene to CloseAndRun(Everest.SaveSettings()),
    // so the pause menu is removed only once a settings file has been written — measured still in
    // the scene three frames later. During that window it is an invisible, unfocused TextMenu that a
    // fresh Level.Pause stacks a second menu on top of.
    //
    // Vanilla's own Return to Map prompt hand-rolls these four lines for the same reason, and saves
    // no settings either: a confirmation prompt changes none.
    private void LeaveThePause(Level level, TextMenu pauseMenu) {
        Close();
        pauseMenu.RemoveSelf();
        level.PauseMainMenuOpen = false;
        level.Paused = false;
        Audio.Play(SFX.ui_game_unpause);
        level.unpauseTimer = 0.15f;
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
