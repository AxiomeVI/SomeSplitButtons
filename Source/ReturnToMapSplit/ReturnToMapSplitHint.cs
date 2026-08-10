using Celeste.Mod.SomeSplitButtons.Splits;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplitButton;

/// <summary>
///     The vanilla Return to Map hint with its caption replaced.
/// </summary>
public class ReturnToMapSplitHint : ReturnMapHint {
    public override void Render() {
        MTexture icon = GFX.Gui["checkpoint"];
        MTexture polaroid = MTN.Checkpoints["polaroid"];
        // Same entry as the pause-menu button's description, and now parameterised, so it has to be
        // formatted here too — Dialog.Clean would leave "{0}" on screen.
        string text = string.Format(Dialog.Get(DialogIds.RTMButtonDesc), SplitTimings.WIPE_FADEOUT_FRAMES);
        float textWidth = ActiveFont.Measure(text).X * 0.75f;

        if (checkpoint != null) {
            float polaroidWidth = polaroid.Width * 0.25f;
            Vector2 at = new((1920f - textWidth - polaroidWidth - 64f) / 2f, 730f);
            float previewScale = 720f / checkpoint.ClipRect.Width;

            ActiveFont.DrawOutline(text, at + new Vector2(textWidth / 2f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * 0.75f, Color.LightGray, 2f, Color.Black);
            at.X += textWidth + 64f;
            polaroid.DrawCentered(at + new Vector2(polaroidWidth / 2f, 0f), Color.White, 0.25f, 0.1f);
            checkpoint.DrawCentered(at + new Vector2(polaroidWidth / 2f, 0f), Color.White, 0.25f * previewScale, 0.1f);
            icon.DrawCentered(at + new Vector2(polaroidWidth * 0.8f, polaroid.Height * 0.25f * 0.5f * 0.8f), Color.White, 0.75f);
        }
        else {
            float iconWidth = icon.Width * 0.75f;
            Vector2 at = new((1920f - textWidth - iconWidth - 64f) / 2f, 730f);

            ActiveFont.DrawOutline(text, at + new Vector2(textWidth / 2f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * 0.75f, Color.LightGray, 2f, Color.Black);
            at.X += textWidth + 64f;
            icon.DrawCentered(at + new Vector2(iconWidth * 0.5f, 0f), Color.White, 0.75f);
        }
    }
}
