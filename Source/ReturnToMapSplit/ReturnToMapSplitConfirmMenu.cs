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
            bool armed = ReturnToMapTimer.HandleButtonPressed(level);
            Finish(armed ? SplitStages.Confirmed : SplitStages.Refused);
            // Armed, the level stays paused through the wait (PausedWait), as vanilla's prompt
            // leaves it through its wipe. Refused, the player has the refusal to wait out in play.
            if (armed) CloseTheMenus(level, pauseMenu);
            else LeaveThePause(level, pauseMenu);
        });

        Button cancelButton = new(Dialog.Clean(DialogIds.VanillaReturnCancelId));
        cancelButton.Pressed(() => OnCancel());

        Add(confirmButton);
        Add(cancelButton);
    }

    /// <summary>
    ///     Closes this prompt and the pause menu under it, and hands the level back to the player.
    /// </summary>
    // ⚠️ Not level.Unpause(): it hands the first TextMenu, the pause menu, to
    // CloseAndRun(Everest.SaveSettings()), which leaves it in the scene for frames, and a fresh
    // Level.Pause stacks a second menu on it. Vanilla's own prompt hand-rolls these lines too.
    private void LeaveThePause(Level level, TextMenu pauseMenu) {
        CloseTheMenus(level, pauseMenu);
        level.Paused = false;
        Audio.Play(SFX.ui_game_unpause);
        level.unpauseTimer = 0.15f;
    }

    // After Close(), whose OnClose hands focus back to the pause menu and sets PauseMainMenuOpen.
    private void CloseTheMenus(Level level, TextMenu pauseMenu) {
        Close();
        pauseMenu.RemoveSelf();
        level.PauseMainMenuOpen = false;
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
