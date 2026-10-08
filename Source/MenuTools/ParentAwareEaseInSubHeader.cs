// MenuTools requires: RecursiveSubMenuBase.cs
namespace Celeste.Mod.SomeSplitButtons.MenuTools;

/// <summary>
/// A subheader that eases in and out, and has the recursive submenu that holds it recalculate its size while its
/// height changes. Use it in place of <see cref="TextMenuExt.EaseInSubHeaderExt"/> inside a recursive submenu, which
/// measures its items only when told to and would keep the height from before the fade. Show or hide it with
/// <see cref="TextMenuExt.EaseInSubHeaderExt.FadeVisible"/>.
/// </summary>
public class ParentAwareEaseInSubHeader : TextMenuExt.EaseInSubHeaderExt, ISubMenuAwareItem {
    /// <summary>
    /// The submenu containing the subheader. The submenu sets it when it attaches the subheader, which waits for the
    /// submenu to be in a menu, and resets it to null when the subheader leaves it.
    /// </summary>
    public RecursiveSubMenuBase ContainingSubMenu { get; set; }

    /// <param name="title">The text of the subheader</param>
    /// <param name="initiallyVisible">Whether the subheader is shown at first</param>
    /// <param name="containingMenu">
    ///     The menu the submenu is in or will be in. Must not be null: the subheader reads its item spacing.
    /// </param>
    /// <param name="containingSubMenu">
    ///     Optional: the submenu sets <see cref="ContainingSubMenu"/> itself when the subheader is added to it.
    ///     Default: null.
    /// </param>
    /// <param name="icon">Name of a texture in the Gui atlas to draw before the text. Default: null, for none.</param>
    public ParentAwareEaseInSubHeader(string title, bool initiallyVisible, TextMenu containingMenu,
                                      RecursiveSubMenuBase containingSubMenu = null, string icon = null)
            : base(title, initiallyVisible, containingMenu, icon) {
        ContainingSubMenu   = containingSubMenu;
        this.containingMenu = containingMenu;
    }

    private readonly TextMenu containingMenu;

    /// <summary>
    /// The height, which follows the fade. Hidden, it is negative and cancels the spacing its row gets: that of the
    /// submenu that holds it, or of the menu when no submenu does.
    /// </summary>
    public override float Height() {
        float eased = base.Height();
        if (ContainingSubMenu == null || containingMenu == null) {
            return eased;
        }
        // The base class eases from minus the menu's spacing
        return eased - (1f - Alpha) * (ContainingSubMenu.ItemSpacing - containingMenu.ItemSpacing);
    }

    /// <summary>
    /// Advances the fade while the menu is visible, and has the submenu recalculate its size when the height changed
    /// </summary>
    public override void Update() {
        if (Container.Visible) {
            // Height change is accomplished by adjusting Alpha and calculating height from Alpha
            float oldAlpha = Alpha;
            base.Update();
            if (oldAlpha != Alpha) {
                ContainingSubMenu?.RecalculateSize();
            }
        }
    }
}
