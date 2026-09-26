// MenuTools requires: RecursiveSubMenuBase.cs
namespace Celeste.Mod.SomeSplitButtons.MenuTools;

/// <summary>
/// Hint subheader that notifies its parent submenu when its height changes, just so we don't have to recalculate the
/// submenu size from Render. The ability to hide an item at the submenu level would probably be a better more general
/// way to do it but this is way easier for now.
/// </summary>
public class ParentAwareEaseInSubHeader : TextMenuExt.EaseInSubHeaderExt, ISubMenuAwareItem {
    /// <summary>
    /// The submenu containing the subheader, set automatically when it is added to one
    /// </summary>
    public RecursiveSubMenuBase ContainingSubMenu { get; set; }

    /// <param name="containingSubMenu">
    ///     No longer needed: the submenu sets it when the subheader is added. Kept for compatibility.
    /// </param>
    public ParentAwareEaseInSubHeader(string title, bool initiallyVisible, TextMenu containingMenu,
                                      RecursiveSubMenuBase containingSubMenu = null, string icon = null)
            : base(title, initiallyVisible, containingMenu, icon) {
        ContainingSubMenu = containingSubMenu;
    }

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
