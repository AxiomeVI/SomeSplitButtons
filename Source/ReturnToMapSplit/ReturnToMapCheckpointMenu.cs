using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     The checkpoint picker that opens after the Return to Map split, when the setting is on.
/// </summary>
// TextMenu's own constructor sets Tag = Tags.PauseUpdate | Tags.HUD, and Level.Update's
// FrozenOrPaused branch runs those entities with MInput.Disabled = false — which is what lets this
// respond while level.Paused freezes Madeline behind it.
internal class ReturnToMapCheckpointMenu : TextMenu {
    /// <summary>The row count of the most recently built picker, for the fixtures.</summary>
    internal static int lastRowCount;

    private bool finished;
    private readonly Action<string> onChosen;
    private readonly Action onCancelled;

    internal ReturnToMapCheckpointMenu(Level level, Action<string> onChosen, Action onCancelled) {
        this.onChosen = onChosen;
        this.onCancelled = onCancelled;

        AutoScroll = false;
        Position = new Vector2(Engine.Width / 2f, Engine.Height / 2f - 100f);

        Add(new Header(Dialog.Clean(DialogIds.CheckpointMenuHeaderId)));

        Button startOfChapter = new(Dialog.Clean(DialogIds.CheckpointMenuStartOfChapterId));
        startOfChapter.Pressed(() => Choose(null));
        Add(startOfChapter);

        List<(string Key, string Label)> rows = CheckpointList.ForArea(level.Session.Area);
        foreach ((string key, string label) in rows) {
            string captured = key;
            Button row = new(label);
            row.Pressed(() => Choose(captured));
            Add(row);
        }

        Button cancel = new(Dialog.Clean(DialogIds.VanillaReturnCancelId));
        cancel.Pressed(() => Cancel());
        Add(cancel);

        // Header is not a row the player counts; the two fixed buttons plus the checkpoints are.
        lastRowCount = rows.Count + 2;

        OnCancel = OnESC = OnPause = Cancel;
    }

    private void Choose(string checkpointKey) {
        if (finished) return;
        finished = true;
        onChosen(checkpointKey);
    }

    private void Cancel() {
        if (finished) return;
        finished = true;
        Audio.Play(SFX.ui_main_button_back);
        onCancelled();
    }

    // The two ways out that bypass a button: something else removing the entity, and the scene
    // ending under it. Either way the hold this picker opened over is owed a release.
    public override void Removed(Scene scene) {
        Cancel();
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene) {
        Cancel();
        base.SceneEnd(scene);
    }
}
