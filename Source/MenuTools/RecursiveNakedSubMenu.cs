// MenuTools requires: RecursiveSubMenuBase.cs
using System;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.MenuTools;

/// <summary>
/// Recursive submenu that displays no title and must be expanded and collapsed through its external API
/// </summary>
public class RecursiveNakedSubMenu : RecursiveSubMenuBase {
    protected override bool TitleSelectable => false;
    protected override bool AutoEnter       => true;
    protected override bool AutoExit        => true;
    protected override bool RecursiveExit   => true;
    protected override bool ShowTitle       => false;
    protected override bool ShowIcon        => false;
    protected override bool ShowMenusSlider => false;
    protected override bool ShowMenu        => _Expanded;
    protected override List<TextMenu.Item> RenderingMenu =>
        (switchingMenus || !_Expanded) ? null : CurrentMenu;

    /// <summary>
    /// Set this property to expand or collapse the menu
    /// </summary>
    public bool Expanded {
        get => _Expanded;
        set {
            bool wasExpanded = _Expanded;
            _Expanded        = value;
            if (wasExpanded == value) {
                return;
            }
            if (!value) {
                // Don't leave the selection on items that are no longer shown
                ReleaseFocus();
            }
            Selectable = value;
            if (!value) {
                MoveSelectionOffSelf();
            }
            StartMenuSwitch(0);
        }
    }
    private bool _Expanded;

    // Once collapsed the submenu can't be selected, so whatever had its selection on it moves to a neighbouring item
    private void MoveSelectionOffSelf() {
        if (parent != null) {
            if (parent.Focus >= FocusType.Body && parent.Current == this) {
                parent.MoveSelectionOffCurrent();
            }
        } else if (Container != null && Container.Current == this) {
            Container.MoveSelection(-1);
            if (Container.Current == this) {
                Container.MoveSelection(1);
            }
        }
    }

    /// <param name="initiallyExpanded">Whether the submenu should be initially expanded or collapsed</param>
    /// <param name="compactMode">      Initializes <see cref="RecursiveSubMenuBase.CompactMode"/></param>
    /// <param name="compactRightWidth">Initializes <see cref="RecursiveSubMenuBase.CompactRightWidth"/></param>
    /// <param name="itemSpacing">      Initializes <see cref="RecursiveSubMenuBase.ItemSpacing"/></param>
    /// <param name="itemIndent">       Initializes <see cref="RecursiveSubMenuBase.ItemIndent"/></param>
    /// <param name="items">            Initial items in the submenu</param>
    public RecursiveNakedSubMenu(bool initiallyExpanded    = false,
                                 bool compactMode          = true,
                                 float compactRightWidth   = float.NaN,
                                 float itemSpacing         = 4f,
                                 float itemIndent          = 0f,
                                 List<TextMenu.Item> items = null)
            : base(label                : "",
                   initialMenuSelection : 0,
                   compactMode          : compactMode,
                   compactRightWidth    : compactRightWidth,
                   itemIndent           : itemIndent,
                   itemSpacing          : itemSpacing) {
        _Expanded  = initiallyExpanded;
        Selectable = initiallyExpanded;
        base.AddMenu("", items);
    }
}
