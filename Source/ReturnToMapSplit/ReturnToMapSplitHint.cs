using System;
using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.UI;
using Celeste.Mod.SomeSplitButtons.Utils;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>The vanilla Return to Map hint with its caption replaced.</summary>
internal class ReturnToMapSplitHint : ReturnMapHint {
    // Same entry as the pause-menu button's description, and parameterised, so it has to be formatted
    // here too — Dialog.Clean would leave "{0}" on screen. Once, not per Render: neither the dialog
    // entry nor the frame count can change while the prompt is open, and this drew a fresh string and
    // measured it sixty times a second.
    private readonly string text =
        string.Format(PluralDialog.Get(DialogIds.RTMButtonDesc, SplitTimings.WIPE_FADEOUT_FRAMES),
                      SplitTimings.WIPE_FADEOUT_FRAMES);

    private readonly float textScale;
    private readonly float textWidth;

    // Vanilla's 0.75, or less when a translation would push the line and the picture beside it past
    // FittedDescription.MaxLineWidth. Fitted against the wider of the two pictures, so one scale
    // serves both layouts.
    internal ReturnToMapSplitHint() {
        float picture = Math.Max(MTN.Checkpoints["polaroid"].Width * 0.25f, GFX.Gui["checkpoint"].Width * 0.75f);
        float room = FittedDescription.MaxLineWidth - picture - 64f;
        float measured = ActiveFont.Measure(text).X;
        textScale = Math.Min(0.75f, room / measured);
        textWidth = measured * textScale;
    }

    /// <summary>The scale the caption is drawn at. Read by the test probe.</summary>
    internal float TextScale => textScale;

    public override void Render() {
        MTexture icon = GFX.Gui["checkpoint"];
        MTexture polaroid = MTN.Checkpoints["polaroid"];

        if (checkpoint != null) {
            float polaroidWidth = polaroid.Width * 0.25f;
            Vector2 at = new((1920f - textWidth - polaroidWidth - 64f) / 2f, 730f);
            float previewScale = 720f / checkpoint.ClipRect.Width;

            ActiveFont.DrawOutline(text, at + new Vector2(textWidth / 2f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * textScale, Color.LightGray, 2f, Color.Black);
            at.X += textWidth + 64f;
            polaroid.DrawCentered(at + new Vector2(polaroidWidth / 2f, 0f), Color.White, 0.25f, 0.1f);
            checkpoint.DrawCentered(at + new Vector2(polaroidWidth / 2f, 0f), Color.White, 0.25f * previewScale, 0.1f);
            icon.DrawCentered(at + new Vector2(polaroidWidth * 0.8f, polaroid.Height * 0.25f * 0.5f * 0.8f), Color.White, 0.75f);
        }
        else {
            float iconWidth = icon.Width * 0.75f;
            Vector2 at = new((1920f - textWidth - iconWidth - 64f) / 2f, 730f);

            ActiveFont.DrawOutline(text, at + new Vector2(textWidth / 2f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * textScale, Color.LightGray, 2f, Color.Black);
            at.X += textWidth + 64f;
            icon.DrawCentered(at + new Vector2(iconWidth * 0.5f, 0f), Color.White, 0.75f);
        }
    }
}
