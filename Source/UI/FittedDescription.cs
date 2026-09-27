using Microsoft.Xna.Framework;
using Monocle;
using static Celeste.TextMenuExt;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>A split button's description, scaled down when a translation runs wider than the screen.</summary>
// SubHeaderExt draws at a fixed 0.6 and reports that width to the menu, and the pause menu is as wide
// as its widest item: one long line would push the whole menu past both edges.
internal sealed class FittedDescription : EaseInSubHeaderExt {
    /// <summary>The widest a line may be drawn, leaving 80 pixels either side of a 1920 screen.</summary>
    internal const float MaxLineWidth = 1760f;

    private const float PreferredScale = 0.6f;

    private readonly float scale;

    public FittedDescription(string title, TextMenu containingMenu)
        : base(title, false, containingMenu) {
        scale = FitScale(title, PreferredScale);
    }

    /// <summary><paramref name="preferred"/>, or less if the line would be wider than <see cref="MaxLineWidth"/>.</summary>
    internal static float FitScale(string line, float preferred) {
        float width = ActiveFont.Measure(line).X * preferred;
        return width <= MaxLineWidth ? preferred : preferred * MaxLineWidth / width;
    }

    public override float LeftWidth() => ActiveFont.Measure(Title).X * scale;

    // SubHeaderExt.Render with the scale changed and the icon left out, since these carry none.
    public override void Render(Vector2 position, bool highlighted) {
        position += Offset;
        float alpha = Container.Alpha * Alpha;
        Color stroke = Color.Black * (alpha * alpha * alpha);
        bool left = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;
        Vector2 at = position + new Vector2(left ? 0f : Container.Width * 0.5f, MathHelper.Max(0f, -16f + HeightExtra));
        Vector2 justify = new(left ? 0f : 0.5f, 0.5f);
        if (Title.Length > 0) ActiveFont.DrawOutline(Title, at, justify, Vector2.One * scale, TextColor * alpha, 2f, stroke);
    }
}
