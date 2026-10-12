using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     The list that opens after the Return to Map split or the Skip Cutscene split, when its setting is
///     on, titled by whichever opened it.
/// </summary>
// TextMenu's own constructor sets Tag = Tags.PauseUpdate | Tags.HUD, and Level.Update's
// FrozenOrPaused branch runs those entities with MInput.Disabled = false — which is what lets this
// respond while level.Paused freezes Madeline behind it.
internal class ReturnToMapCheckpointMenu : TextMenu {
    /// <summary>The row count of the most recently built picker, for the fixtures.</summary>
    internal static int lastRowCount;

    /// <summary>The destination labels of the most recently built list, comma-separated, for the fixtures.</summary>
    internal static string lastDestinationLabels;

    /// <summary>Every row label of the most recently built list, |-separated with spaces as _, Cancel left out, for the fixtures.</summary>
    internal static string lastRowLabels;

    private bool finished;
    private readonly Action<string> onChosen;
    // True when the level goes on under the closed list, false when the scene is ending.
    private readonly Action<bool> onCancelled;

    internal ReturnToMapCheckpointMenu(List<(string Key, string Label)> rows, Action<string> onChosen,
        Action<bool> onCancelled, string titleId) {
        this.onChosen = onChosen;
        this.onCancelled = onCancelled;

        AutoScroll = false;
        Position = new Vector2(Engine.Width / 2f, Engine.Height / 2f - 100f);

        Add(new Header(Dialog.Clean(titleId)));

        foreach ((string key, string label) in rows) {
            Button row = new(label);
            row.Pressed(() => Choose(key));
            Add(row);
        }

        Button cancel = new(Dialog.Clean(DialogIds.VanillaReturnCancelId));
        cancel.Pressed(() => Cancel(levelGoesOn: true));
        Add(cancel);

        // Header is not a row the player counts; the checkpoints and Cancel are.
        lastRowCount = rows.Count + 1;
        List<string> destinations = new();
        foreach ((string key, string label) in rows) {
            if (Destinations.IsKey(key)) destinations.Add(label);
        }
        lastDestinationLabels = destinations.Count == 0 ? "none" : string.Join(",", destinations);
        lastRowLabels = rows.Count == 0 ? "none" : string.Join("|", rows.ConvertAll(row => row.Label.Replace(' ', '_')));

        OnCancel = OnESC = OnPause = () => Cancel(levelGoesOn: true);
    }

    private void Choose(string checkpointKey) {
        if (finished) return;
        finished = true;
        onChosen(checkpointKey);
    }

    private void Cancel(bool levelGoesOn) {
        if (finished) return;
        finished = true;
        Audio.Play(SFX.ui_main_button_back);
        onCancelled(levelGoesOn);
    }

    // The two ways out that bypass a button: something else removing the entity, and the scene
    // ending under it. Either way the hold this picker opened over is owed a release.
    public override void Removed(Scene scene) {
        Cancel(levelGoesOn: true);
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene) {
        Cancel(levelGoesOn: false);
        base.SceneEnd(scene);
    }
}
