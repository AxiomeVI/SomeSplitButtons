using System;
using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.UI;
using Celeste.Mod.SomeSplitButtons.Utils;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>The vanilla Return to Map hint with its caption replaced.</summary>
internal class ReturnToMapSplitHint : ReturnMapHint {
    // Formatted (Dialog.Clean would show "{0}") and measured once: nothing in it changes while the
    // prompt is open.
    private readonly string text =
        string.Format(PluralDialog.Get(SplitFeatures.ReturnToMap.DescriptionId(), SplitTimings.WIPE_FADEOUT_FRAMES),
                      SplitTimings.WIPE_FADEOUT_FRAMES);

    private readonly float textScale;
    private readonly float textWidth;
    private readonly float fittedWidth;

    // Vanilla's 0.75, or less when a translation would push the line and the picture beside it past
    // FittedDescription.MaxLineWidth. Fitted against the wider of the two pictures, so one scale
    // serves both layouts.
    internal ReturnToMapSplitHint() {
        float picture = Math.Max(MTN.Checkpoints["polaroid"].Width * 0.25f, GFX.Gui["checkpoint"].Width * 0.75f);
        float room = FittedDescription.MaxLineWidth - picture - 64f;
        float measured = ActiveFont.Measure(text).X;
        textScale = Math.Min(0.75f, room / measured);
        textWidth = measured * textScale;
        fittedWidth = textWidth + 64f + picture;
    }

    /// <summary>The width fitted against FittedDescription.MaxLineWidth. Read by the test probe.</summary>
    internal float FittedWidth => fittedWidth;

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
