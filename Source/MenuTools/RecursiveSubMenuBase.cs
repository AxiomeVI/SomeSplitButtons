// MenuTools requires: ISubMenuAwareItem.cs IInputHoldingItem.cs
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Mod.Core;
using Celeste.Mod.UI;

namespace Celeste.Mod.SomeSplitButtons.MenuTools;

/// <summary>
/// Base class for all flavors of recursive submenus. Includes most of the implementation for all of them.
/// </summary>
public abstract class RecursiveSubMenuBase : TextMenu.Item, IInputHoldingItem {
    // ================ Configuration values initialized by corresponding named constructor parameters =================
    /// <summary>
    /// Name to display as the title of the submenu
    /// </summary>
    public readonly string Label;
    /// <summary>
    /// Index of the initial menu to display
    /// </summary>
    public readonly int InitialMenuSelection;
    /// <summary>
    /// If false, align left and right columns to the global column division and reserve enough width accordingly.
    /// If true, reserve only enough width to fit each item individually and attempt to return a
    /// <see cref="RightWidth"/> that minimizes the overall width of the whole menu.
    /// </summary>
    public readonly bool CompactMode;
    /// <summary>
    /// Value to return for <see cref="RightWidth"/> when in compact mode.
    /// If initialized to nan, defaults to the right-width of an on-off slider.
    /// </summary>
    public readonly float CompactRightWidth;
    /// <summary>
    /// Vertical spacing between items
    /// </summary>
    public readonly float ItemSpacing;
    /// <summary>
    /// Horizontal indent for the body of the submenu
    /// </summary>
    public readonly float ItemIndent;

    // Additional appearance parameters not set by the constructor.
    /// <summary>
    /// Current color of the selected <see cref="TextMenu.Item"/>
    /// </summary>
    public Color HighlightColor { get; private set; } = Color.White;
    private readonly MTexture Icon            = GFX.Gui["downarrow"];
    private const float optionTextScale       = 0.8f;
    private const float bracketsReservedWidth = 130f;
    private const float iconXPadding          = 10f;

    // Handle passing focus and scrolling responsibility between nested submenus
    public enum FocusType { None, Title, Body, Child };
    public FocusType Focus { get; set; }  = FocusType.None;
    public bool AutoScroll { get; set; }  = false;
    protected bool receivedHover          = false;
    protected RecursiveSubMenuBase parent = null;

    // Menus are stored as lists associated with a label
    public struct LabeledMenu {
        public LabeledMenu(string label, List<TextMenu.Item> menu) {
            Label = label;
            Menu  = menu;
        }
        public string Label;
        public List<TextMenu.Item> Menu;
    }
    private List<LabeledMenu> menus           = [];
    private List<LabeledMenu> delayedAddMenus = [];
    bool added                                = false;

    /// <summary>
    /// Index of the currently selected list of <see cref="TextMenu.Item"/>s
    /// </summary>
    public int MenuIndex { get; private set; } = 0;

    // Menu chosen with SelectMenu before the submenu was added, used instead of InitialMenuSelection
    private int? pendingMenuSelection;

    /// <summary>
    /// Index of the currently selected <see cref="TextMenu.Item"/> within the selected list
    /// </summary>
    public int Selection { get; private set; } = -1;

    /// <summary>
    /// Invoked when the selected menu is changed
    /// </summary>
    public Action<int> OnValueChange;

    // Handle wiggling manually rather than letting it go through SelectWiggler
    protected Wiggler titleWiggler;
    protected Wiggler menuWiggler;

    // Size tracking
    protected float menuHeight;
    protected float titleHeight;
    protected float leftColumnWidth;
    protected float rightColumnWidth;
    protected float optionsWidth;
    protected float compactWidth;

    // Tracking for wiggling the arrows for the menu select
    private int lastDir;
    private float sine;

    // Handle smoothly switching between menus
    protected bool switchingMenus;
    protected int switchFromMenuIndex;
    protected float switchFromMenuHeight;
    private float switchMenuEase           = 1f;
    private const float maxSwitchMenuTime  = 0.25f;
    private const float switchMenuVelocity = 64f / 0.1f;  // Pixels per second
    private float switchMenuEaseRate       = 1f / maxSwitchMenuTime;  // Per second
    private float EasedMenuHeight {
        get { return switchFromMenuHeight + Ease.QuadOut(switchMenuEase) * (menuHeight - switchFromMenuHeight); }
    }

    // Handle smoothly adding, removing, showing and hiding items
    private enum AnimationKind { Add, Remove, Show, Hide }
    private class AddRemoveItemState {
        private const float maxAddRemoveItemTime  = 0.25f;
        private const float addRemoveItemVelocity = 64f / 0.1f;  // Pixels per second

        public bool Done => ease >= 1f;

        public AddRemoveItemState(TextMenu.Item item, int menuIndex, AnimationKind kind, Action<TextMenu.Item> doneCb) {
            this.Item          = item;
            this.MenuIndex     = menuIndex;
            this.Kind          = kind;
            this.DoneCb        = doneCb;
            this.WasVisible    = item.Visible;
            this.WasSelectable = item.Selectable;
            easeRate           = Math.Max(1f / maxAddRemoveItemTime,
                                          addRemoveItemVelocity / Math.Abs(item.Height()));
        }

        public void Update() {
            ease = Math.Min(ease + easeRate * Engine.RawDeltaTime, 1f);
        }

        public float EasedHeight(float itemSpacing) {
            bool growing = Kind is AnimationKind.Add or AnimationKind.Show;
            return (Item.Height() + itemSpacing) * (growing ? Ease.QuadOut(ease) : 1 - Ease.QuadOut(ease));
        }

        public readonly TextMenu.Item Item;
        public readonly int MenuIndex;
        public readonly AnimationKind Kind;
        public readonly Action<TextMenu.Item> DoneCb;
        // The item is hidden during the animation; these are restored when it finishes
        public readonly bool WasVisible;
        public readonly bool WasSelectable;
        private readonly float easeRate;  // Per second
        private float ease = 0f;
    }
    private List<AddRemoveItemState> addRemoveItems = [];

    // Reused every Update so items can add or remove items (their own submenu's included) while being updated
    private readonly List<TextMenu.Item> updatingItems = [];

    /// <summary>
    /// The selected set of <see cref="TextMenu.Item"/>s, empty if the submenu has no menus
    /// </summary>
    public List<TextMenu.Item> CurrentMenu {
        get { return (MenuIndex >= 0 && MenuIndex < menus.Count) ? menus[MenuIndex].Menu : noMenu; }
    }
    // Stands in for CurrentMenu when there are no menus (before any is added, or after Clear), so callers don't
    // need null checks. Items are only ever added to real menus.
    private readonly List<TextMenu.Item> noMenu = [];

    /// <summary>
    /// The list of <see cref="TextMenu.Item"/>s we're switching from as we smoothly transition between menus
    /// </summary>
    protected List<TextMenu.Item> SwitchFromMenu {
        get { return (menus.Count > 0 && switchingMenus) ? menus[switchFromMenuIndex].Menu : null; }
    }

    /// <summary>
    /// The selected <see cref="TextMenu.Item"/>
    /// </summary>
    public TextMenu.Item Current {
        get {
            if (Focus < FocusType.Body || Selection < 0 || Selection >= CurrentMenu.Count) {
                return null;
            }
            return CurrentMenu[Selection];
        }
        set {
            int index = CurrentMenu.IndexOf(value);
            if (index >= 0) {
                Selection = index;
            }
        }
    }

    /// <summary>
    /// Index of the first selectable item in the currently selected menu
    /// </summary>
    public int FirstPossibleSelection {
        get {
            for (int i = 0; i < CurrentMenu.Count; i++) {
                if (CurrentMenu[i] != null && CurrentMenu[i].Hoverable) {
                    return i;
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// Index of the last selectable item in the currently selected menu
    /// </summary>
    public int LastPossibleSelection {
        get {
            for (int i = CurrentMenu.Count - 1; i >= 0; i--) {
                if (CurrentMenu[i] != null && CurrentMenu[i].Hoverable) {
                    return i;
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// Target Y position for the menu to keep the current item on screen
    /// </summary>
    public float ScrollTargetY {
        get {
            if (Container.Height < Container.ScrollableMinSize) {
                return Engine.Height / 2f;
            } else {
                float min = Engine.Height - 150f - Container.Height * Container.Justify.Y;
                float max = 150f + Container.Height * Container.Justify.Y;
                return Calc.Clamp((Engine.Height / 2) + Container.Height * Container.Justify.Y - GetYOffsetOf(Current),
                                  min, max);
            }
        }
    }

    // OuiModOptions.menu is added by Everest (it is not a vanilla Celeste member), so
    // CelesteMod.Publicizer does not publicize it and it is not referenced directly; read through
    // reflection instead, resolved once.
    private static readonly FieldInfo OuiModOptionsMenuField =
        typeof(OuiModOptions).GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic);

    // Whether we are in the main-menu menu vs the in-game menu
    private bool InOverworldMenu {
        get { return OuiModOptionsMenuField != null && OuiModOptions.Instance != null &&
                  OuiModOptions.Instance.Selected &&
                  Container == (TextMenu) OuiModOptionsMenuField.GetValue(OuiModOptions.Instance); }
    }

    // ============ Additional knobs for subclasses to alter the behavior and appearance of the submenu ================
    /// <summary>
    /// True to allow selecting the title of the submenu like any other item, false to skip past the title instead
    /// (if false, <see cref="AutoEnter"/> should be true so some part of the submenu is selectable)
    /// </summary>
    protected abstract bool TitleSelectable { get; }
    /// <summary>
    /// Whether to automatically enter the menu body while scrolling, without needing to press Confirm on the title.
    /// Either scroll from the title to the body or skip the title entirely depending on <see cref="TitleSelectable"/>.
    /// </summary>
    protected abstract bool AutoEnter { get; }
    /// <summary>
    /// If true, automatically leave the submenu when selection leaves its bounds, rather than wrapping or stopping
    /// </summary>
    protected abstract bool AutoExit { get; }
    /// <summary>
    /// Whether to exit the parent submenu or container TextMenu when Cancel is pressed (useful for naked / hidden
    /// submenus to preserve the illusion)
    /// </summary>
    protected abstract bool RecursiveExit { get; }
    /// <summary>
    /// Whether to display the title of the submenu
    /// </summary>
    protected abstract bool ShowTitle { get; }
    /// <summary>
    /// Whether to display the arrow icon in the submenu title
    /// </summary>
    protected abstract bool ShowIcon { get; }
    /// <summary>
    /// Whether to display the option slider for switching between submenus
    /// </summary>
    protected abstract bool ShowMenusSlider { get; }
    /// <summary>
    /// Whether to show the items in the currently selected submenu
    /// </summary>
    protected abstract bool ShowMenu { get; }
    /// <summary>
    /// Which submenu to use when rendering items, or null to render no items
    /// </summary>
    protected abstract List<TextMenu.Item> RenderingMenu { get; }

    // =================================================================================================================
    /// <param name="label">               Initializes <see cref="Label"/></param>
    /// <param name="initialMenuSelection">Initializes <see cref="InitialMenuSelection"/></param>
    /// <param name="compactMode">         Initializes <see cref="CompactMode"/></param>
    /// <param name="compactRightWidth">   Initializes <see cref="CompactRightWidth"/></param>
    /// <param name="itemSpacing">         Initializes <see cref="ItemSpacing"/></param>
    /// <param name="itemIndent">          Initializes <see cref="ItemIndent"/></param>
    public RecursiveSubMenuBase(string label             = "",
                                int initialMenuSelection = 0,
                                bool compactMode         = true,
                                float compactRightWidth  = float.NaN,
                                float itemSpacing        = 4f,
                                float itemIndent         = 20f) : base() {
        Label                = label;
        InitialMenuSelection = initialMenuSelection;
        CompactMode          = compactMode;
        CompactRightWidth    = compactRightWidth;
        ItemSpacing          = itemSpacing;
        ItemIndent           = itemIndent;
        if (float.IsNaN(CompactRightWidth)) {
            CompactRightWidth = Math.Max(ActiveFont.Measure(Dialog.Clean("OPTIONS_ON")).X,
                                         ActiveFont.Measure(Dialog.Clean("OPTIONS_OFF")).X) + 120f;
        }

        // Initialize TextMenu.Item fields
        Selectable                = true;
        IncludeWidthInMeasurement = true;

        // Using these publics fields to do it is hacky, but this is necessary to make certain features work properly
        OnEnter = DefaultOnEnter;
        OnLeave = DefaultOnLeave;

        RecalculateSize();
    }

    public override void Added() {
        base.Added();
        List<LabeledMenu> addingMenus = delayedAddMenus;
        delayedAddMenus = [];
        // MenuIndex may already point past the menus being re-added here (set by SelectMenu before we were added)
        MenuIndex = 0;
        foreach (LabeledMenu menu in addingMenus) {
            AddMenu(menu.Label, menu.Menu);
        }
        MenuIndex = Calc.Clamp(pendingMenuSelection ?? InitialMenuSelection, 0, Math.Max(menus.Count - 1, 0));
        RecalculateSize();

        // Take manual control of wiggling
        SelectWiggler.Init(0f, 0f, null, false, false);
        SelectWiggler.StartZero = true;
        Container.Add(titleWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
        Container.Add(menuWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
        titleWiggler.UseRawDeltaTime = true;
        menuWiggler.UseRawDeltaTime  = true;
        added                        = true;
    }

    /// <summary>
    /// Set the action to be invoked when the selected menu is changed.
    /// </summary>
    public RecursiveSubMenuBase Change(Action<int> onValueChange) {
        OnValueChange = onValueChange;
        return this;
    }

    /// <summary>
    /// Remove all menus and <see cref="TextMenu.Item"/>s from the submenu. If the submenu has focus, it is given back
    /// to the parent first. Items can be added again afterwards; for single-menu submenus, the menu is recreated.
    /// </summary>
    public virtual void Clear() {
        ReleaseFocus();
        if (Container != null) {
            foreach (LabeledMenu menu in menus) {
                foreach (TextMenu.Item item in menu.Menu) {
                    DetachItem(item);
                }
            }
        }
        menus.Clear();
        delayedAddMenus.Clear();
        addRemoveItems.Clear();
        MenuIndex      = 0;
        Selection      = -1;
        switchingMenus = false;
        switchMenuEase = 1f;
        RecalculateSize();
    }

    /// <summary>
    /// Give up any focus held by this submenu or a submenu nested in it, returning focus to the parent
    /// </summary>
    protected void ReleaseFocus() {
        if (Focus == FocusType.Child && Current is RecursiveSubMenuBase child) {
            child.ReleaseFocus();
        }
        if (Focus != FocusType.None) {
            ForceExit();
        }
    }

    // =============================================== Add menus =======================================================
    /// <summary>
    ///     Insert a labeled menu into the list of submenus
    /// </summary>
    /// <param name="menuIndex">
    ///     Index at which to insert the menu, or -1 to add the menu to the end of the list
    /// </param>
    protected void InsertMenu(int menuIndex, string label, List<TextMenu.Item> items) {
        if (Container != null) {
            menus.Insert(menuIndex, new LabeledMenu(label, []));
        } else {
            delayedAddMenus.Insert(menuIndex, new LabeledMenu(label, []));
        }
        if (items != null) {
            foreach (TextMenu.Item item in items) {
                AddItem(menuIndex, item, false, null);
            }
        }
        RecalculateSize();
    }

    /// <summary>
    /// Add a labeled menu to the end of the list of submenus
    /// </summary>
    protected void AddMenu(string label, List<TextMenu.Item> items) {
        InsertMenu(Container != null ? menus.Count : delayedAddMenus.Count,
                   label, items);
    }

    // ================================================ Add items ======================================================
    /// <summary>
    ///     Insert an item into an existing submenu
    /// </summary>
    /// <param name="menuIndex">
    ///     Index of the menu into which to insert the item (the menu must already exist)
    /// </param>
    /// <param name="itemIndex">
    ///     Index in the selected menu at which to insert the item
    /// </param>
    /// <param name="smooth">
    ///     Whether to do an animation to smoothly add the item
    /// </param>
    /// <param name="smoothAddDoneCb">
    ///     Optional callback called after the smooth add animation completes
    /// </param>
    protected void InsertItem(int menuIndex, int itemIndex, TextMenu.Item item, bool smooth,
                              Action<TextMenu.Item> smoothAddDoneCb) {
        if (Container != null) {
            if (Focus >= FocusType.Body && menuIndex == this.MenuIndex && itemIndex <= Selection) {
                // Increment the selection to keep the same item selected if we're focused.
                // Don't go through MoveSelection since we're not actually moving anywhere,
                // it's just the indices that are changing.
                ++Selection;
            }
            menus[menuIndex].Menu.Insert(itemIndex, item);
            item.Container = Container;
            if (item is RecursiveSubMenuBase subSubMenu) {
                subSubMenu.parent = this;
            }
            if (item is ISubMenuAwareItem subMenuAwareItem) {
                subMenuAwareItem.ContainingSubMenu = this;
            }
            Container.Add(item.ValueWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
            Container.Add(item.SelectWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
            item.ValueWiggler.UseRawDeltaTime  = true;
            item.SelectWiggler.UseRawDeltaTime = true;
            item.Added();
            if (smooth) {
                addRemoveItems.Add(new(item, menuIndex, AnimationKind.Add, smoothAddDoneCb));
                item.Visible    = false;
                item.Selectable = false;
            }
            RecalculateSize();
        } else {
            // We haven't even been added ourselves yet, ignore the smooth request
            delayedAddMenus[menuIndex].Menu.Insert(itemIndex, item);
        }
    }

    /// <summary>
    ///     Insert an item into the last menu in the list
    /// </summary>
    /// <param name="itemIndex">
    ///     Index in the selected menu at which to insert the item
    /// </param>
    /// <param name="smooth">
    ///     Whether to do an animation to smoothly add the item
    /// </param>
    /// <param name="smoothAddDoneCb">
    ///     Optional callback called after the smooth add animation completes
    /// </param>
    public RecursiveSubMenuBase InsertItem(int itemIndex, TextMenu.Item item, bool smooth,
                                           Action<TextMenu.Item> smoothAddDoneCb) {
        InsertItem(LastMenuIndexForAdding(), itemIndex, item, smooth, smoothAddDoneCb);
        return this;
    }

    // Index of the last menu, creating an unlabeled one first if there are none (e.g. after Clear)
    private int LastMenuIndexForAdding() {
        if ((Container != null ? menus : delayedAddMenus).Count == 0) {
            AddMenu("", null);
        }
        return (Container != null ? menus : delayedAddMenus).Count - 1;
    }

    /// <summary>
    ///     Add an item to the end of an existing submenu
    /// </summary>
    /// <param name="menuIndex">
    ///     Index of the menu to which to add the item (the menu must already exist)
    /// </param>
    /// <param name="smooth">
    ///     Whether to do an animation to smoothly add the item
    /// </param>
    /// <param name="smoothAddDoneCb">
    ///     Optional callback called after the smooth add animation completes
    /// </param>
    protected void AddItem(int menuIndex, TextMenu.Item item, bool smooth, Action<TextMenu.Item> smoothAddDoneCb) {
        InsertItem(menuIndex,
                   Container != null ? menus[menuIndex].Menu.Count
                                     : delayedAddMenus[menuIndex].Menu.Count,
                   item, smooth, smoothAddDoneCb);
    }

    /// <summary>
    ///     Add an item to the end of the last menu in the list
    /// </summary>
    /// <param name="smooth">
    ///     Whether to do an animation to smoothly add the item
    /// </param>
    /// <param name="smoothAddDoneCb">
    ///     Optional callback called after the smooth add animation completes
    /// </param>
    public RecursiveSubMenuBase AddItem(TextMenu.Item item, bool smooth = false,
                                        Action<TextMenu.Item> smoothAddDoneCb = null) {
        AddItem(LastMenuIndexForAdding(), item, smooth, smoothAddDoneCb);
        return this;
    }

    // ============================================== Remove items =====================================================
    /// <summary>
    ///     Remove an item from a menu
    /// </summary>
    /// <param name="menuIndex">
    ///     Index of the menu from which to remove the item
    /// </param>
    /// <param name="item">
    ///     Item to remove
    /// </param>
    /// <param name="smooth">
    ///     Whether to do an animation to smoothly remove the item
    /// </param>
    /// <param name="smoothRemoveDoneCb">
    ///     Optional callback called after the smooth remove animation completes
    /// </param>
    protected void RemoveItem(int menuIndex, TextMenu.Item item, bool smooth,
                              Action<TextMenu.Item> smoothRemoveDoneCb) {
        if (Container != null) {
            int removeIndex = menus[menuIndex].Menu.IndexOf(item);
            bool beingRemoved = addRemoveItems.Exists((AddRemoveItemState addRemoveItem) =>
                                                          addRemoveItem.Item == item &&
                                                          addRemoveItem.Kind == AnimationKind.Remove);
            if (removeIndex < 0 || beingRemoved) {
                // Not in this menu, or already being removed
                return;
            }
            // Removing an item that is still animating in cancels that animation
            addRemoveItems.RemoveAll((AddRemoveItemState addRemoveItem) => addRemoveItem.Item == item);
            if (menuIndex == this.MenuIndex && Focus >= FocusType.Body && removeIndex == Selection) {
                // If we're removing the current selection, attempt to move off of it first so focus, etc
                // is handled properly. This also ensures we're not left selecting an unselectable item during
                // the remove animation if we're doing it.
                MoveSelectionOffCurrent();
            }
            if (smooth) {
                // If we're doing the remove animation, defer actually removing the item until the animation
                // is done to hold its place for the spacer
                addRemoveItems.Add(new(item, menuIndex, AnimationKind.Remove, smoothRemoveDoneCb));
                item.Visible    = false;
                item.Selectable = false;
                RecalculateSize();
                return;
            }
            if (menuIndex == this.MenuIndex && Focus >= FocusType.Body && removeIndex < Selection) {
                // Decrement the selection index to keep the same item selected.
                // Don't go through MoveSelection since we're not actually moving anywhere,
                // it's just the indices that are changing.
                // This includes the case where we just moved down off the item we're removing.
                --Selection;
            }
            menus[menuIndex].Menu.Remove(item);
            DetachItem(item);
            RecalculateSize();
        } else {
            // We haven't even been added ourselves yet, ignore the smooth request
            delayedAddMenus[menuIndex].Menu.Remove(item);
        }
    }

    // Move the selection off the current item before it disappears, giving focus back if there's nowhere to go
    protected internal void MoveSelectionOffCurrent() {
        if (Selection != FirstPossibleSelection) {
            MoveSelection(-1, false, true, out _);
        } else if (Selection != LastPossibleSelection) {
            MoveSelection(1, false, true, out _);
        } else {
            // It's the only selectable item, so there's nothing left to select in here
            ForceExit();
        }
    }

    // Apply the end state of an animation that has been taken out of addRemoveItems
    private void FinishAnimation(AddRemoveItemState addRemoveItem) {
        TextMenu.Item item = addRemoveItem.Item;
        switch (addRemoveItem.Kind) {
            case AnimationKind.Add:
                item.Visible    = addRemoveItem.WasVisible;
                item.Selectable = addRemoveItem.WasSelectable;
                break;
            case AnimationKind.Show:
                item.Visible    = true;
                item.Selectable = addRemoveItem.WasSelectable;
                break;
            case AnimationKind.Hide:
                item.Visible    = false;
                item.Selectable = addRemoveItem.WasSelectable;
                break;
            case AnimationKind.Remove:
                RemoveItem(addRemoveItem.MenuIndex, item, false, null);
                break;
        }
        addRemoveItem.DoneCb?.Invoke(item);
    }

    // ============================================== Show / hide items ================================================
    /// <summary>
    ///     Show or hide an item of this submenu. A hidden item takes no space and can't be selected.
    /// </summary>
    /// <param name="item">
    ///     The item to show or hide, in any of the submenu's menus
    /// </param>
    /// <param name="visible">
    ///     Whether the item should be shown
    /// </param>
    /// <param name="smooth">
    ///     Whether to animate the item's height, like <see cref="AddItem(TextMenu.Item, bool, Action{TextMenu.Item})"/>
    ///     and <see cref="RemoveItem(TextMenu.Item, bool, Action{TextMenu.Item})"/> do
    /// </param>
    public void SetItemVisible(TextMenu.Item item, bool visible, bool smooth = false) {
        int menuIndex = menus.FindIndex((LabeledMenu menu) => menu.Menu.Contains(item));
        if (Container == null || menuIndex < 0) {
            // Not added yet (or not ours): nothing to animate or move the selection off
            item.Visible = visible;
            return;
        }
        if (addRemoveItems.Exists((AddRemoveItemState addRemoveItem) =>
                                      addRemoveItem.Item == item &&
                                      addRemoveItem.Kind is AnimationKind.Add or AnimationKind.Remove)) {
            // Being added or removed, which decides its visibility
            return;
        }
        // Settle a show or hide that's still animating before starting over
        foreach (AddRemoveItemState addRemoveItem in addRemoveItems.FindAll((AddRemoveItemState addRemoveItem) =>
                                                                                addRemoveItem.Item == item)) {
            addRemoveItems.Remove(addRemoveItem);
            FinishAnimation(addRemoveItem);
        }
        if (item.Visible == visible) {
            return;
        }
        if (!visible && menuIndex == MenuIndex && Focus >= FocusType.Body && CurrentMenu.IndexOf(item) == Selection) {
            MoveSelectionOffCurrent();
        }
        if (smooth) {
            addRemoveItems.Add(new(item, menuIndex, visible ? AnimationKind.Show : AnimationKind.Hide, null));
            item.Visible    = false;
            item.Selectable = false;
        } else {
            item.Visible = visible;
        }
        RecalculateSize();
    }

    // Undo what InsertItem did to attach the item to our container
    private void DetachItem(TextMenu.Item item) {
        item.Container = null;
        if (item is RecursiveSubMenuBase submenuItem) {
            submenuItem.parent = null;
        }
        if (item is ISubMenuAwareItem subMenuAwareItem && subMenuAwareItem.ContainingSubMenu == this) {
            subMenuAwareItem.ContainingSubMenu = null;
        }
        Container.Remove(item.ValueWiggler);
        Container.Remove(item.SelectWiggler);
    }

    /// <summary>
    ///     Remove an item from the last menu in the list
    /// </summary>
    /// <param name="item">
    ///     Item to remove
    /// </param>
    /// <param name="smooth">
    ///     Whether to do an animation to smoothly remove the item
    /// </param>
    /// <param name="smoothRemoveDoneCb">
    ///     Optional callback called after the smooth remove animation completes
    /// </param>
    public RecursiveSubMenuBase RemoveItem(TextMenu.Item item, bool smooth = false,
                                           Action<TextMenu.Item> smoothRemoveDoneCb = null) {
        int lastMenuIndex = (Container != null ? menus : delayedAddMenus).Count - 1;
        if (lastMenuIndex >= 0) {
            RemoveItem(lastMenuIndex, item, smooth, smoothRemoveDoneCb);
        }
        return this;
    }

    // ============================================ Move selection =====================================================
    /// <summary>
    /// Set the selection to the first possible <see cref="TextMenu.Item"/> in the currently selected list
    /// </summary>
    /// <param name="wiggle">Whether to wiggle the item being selected</param>
    public void FirstSelection(bool wiggle) {
        Selection = -1;
        if (CurrentMenu.Count > 0) {
            MoveSelection(1, false, wiggle, out _);
        }
    }

    /// <summary>
    /// Set the selection to the last possible <see cref="TextMenu.Item"/> in the currently selected list
    /// </summary>
    /// <param name="wiggle">Whether to wiggle the item being selected</param>
    public void LastSelection(bool wiggle) {
        Selection = LastPossibleSelection + 1;
        if (CurrentMenu.Count > 0) {
            MoveSelection(-1, false, wiggle, out _);
        }
    }

    /// <summary>
    ///     Returns true if a move in direction <paramref name="dir"/> would cause the menu selection to wrap
    ///     around, false otherwise.
    /// </summary>
    /// <param name="dir">
    ///     Which direction to move in (down is positive)
    /// </param>
    /// <param name="allowExit">
    ///     Whether a submenu with auto-exit is allowed to use it for this move
    /// </param>
    public bool MoveWouldWrap(int dir, bool allowExit) {
        if ((dir == 1 && Selection != LastPossibleSelection) ||
                (dir == -1 && Selection != FirstPossibleSelection)) {
            // Just moving inside this submenu without wrapping
            return false;
        }
        if (!(AutoExit && allowExit)) {
            // This submenu will wrap the move
            return true;
        }
        if (parent == null) {
            // The move would exit into the top-level menu, check if it would wrap
            return (dir == 1 && Container.IndexOf(this) == Container.LastPossibleSelection ||
                    dir == -1 && Container.IndexOf(this) == Container.FirstPossibleSelection);
        } else {
            // The move would exit into the parent submenu, check if it would wrap
            return parent.MoveWouldWrap(dir, allowExit);
        }
    }

    /// <summary>
    ///     Move the selection to the next possible item in the currently selected list
    /// </summary>
    /// <param name="direction">
    ///     The direction of movement (Down is positive)
    /// </param>
    /// <param name="allowExit">
    ///     Whether a submenu with auto-exit is allowed to use it for this move
    /// </param>
    /// <param name="wiggle">
    ///     Whether to wiggle the item being selected
    /// </param>
    /// <param name="playedScrollSFX">
    ///     Whether the container TextMenu played a scroll up/down SFX
    ///     (submenus themselves don't play the SFX here to avoid double-playing it,
    ///      but vanilla TextMenus do if this move ends up triggering a move in the container)
    /// </param>
    public void MoveSelection(int direction, bool allowExit, bool wiggle, out bool playedScrollSFX) {
        playedScrollSFX = false;
        int selection   = Selection;
        direction       = Math.Sign(direction);
        int count       = 0;
        foreach (TextMenu.Item item in CurrentMenu) {
            if (item.Hoverable) {
                count++;
            }
        }
        do {
            Selection += direction;
            if (!(AutoExit && allowExit)) {
                if (count > 1) {
                    if (Selection < 0) {
                        Selection = CurrentMenu.Count - 1;
                    } else if (Selection >= CurrentMenu.Count) {
                        Selection = 0;
                    }
                } else if (Selection < 0 || Selection > CurrentMenu.Count - 1) {
                    Selection = Calc.Clamp(Selection, 0, CurrentMenu.Count - 1);
                    break;
                }
            }
        } while (Selection >= 0 && Selection < CurrentMenu.Count && !Current.Hoverable);

        if (Selection != selection) {
            if (Selection < 0 || Selection >= CurrentMenu.Count) {
                // Exiting into parent from auto-exit submenu
                Selection = selection;
                Exit(direction, wiggle, false, out playedScrollSFX, out _);
                return;
            }
            if (!Current.Hoverable) {
                Selection = selection;
            }
            if (Selection != selection && Current != null) {
                if (selection >= 0 && selection < CurrentMenu.Count) {
                    CurrentMenu[selection]?.OnLeave?.Invoke();
                }
                Current.OnEnter?.Invoke();
                if (wiggle) {
                    Current.SelectWiggler.Start();
                }
            }
        }
    }

    /// <summary>
    ///     Move the item selection to a particular y position
    /// </summary>
    /// <param name="toY">
    ///     Y position to move to. This is in the reference frame used by <see cref="GetYOffsetOf"/>
    /// </param>
    /// <param name="dir">
    ///     Intended direction of motion, any "backwards" moves where toY and dir disagree will be ignored
    /// </param>
    /// <param name="allowExit">
    ///     Whether a submenu with auto-exit is allowed to use it this move
    /// </param>
    public void MoveToY(float toY, int dir, bool allowExit) {
        float startY  = GetYOffsetOf(Current);
        float travelY = toY - startY;
        if (Math.Sign(travelY) != dir) {
            // Avoid infinite oscillations from multiple submenus trying to get to a point in between them
            return;
        }
        while (Math.Abs(GetYOffsetOf(Current) - startY) < Math.Abs(travelY) &&
                !MoveWouldWrap(dir, allowExit) && Focus != FocusType.None) {
            if (Focus == FocusType.Title) {
                if (dir > 0 && AutoEnter && CurrentMenu.Count > 0) {
                    Enter(dir, false);
                } else {
                    Exit(dir, false, false, out _, out _);
                }
            } else {
                MoveSelection(dir, allowExit, false, out _);
            }
        }
    }

    // ======================================= Size / position calcs ===================================================
    /// <summary>
    /// Recalculate the necessary width to hold all items in any list, and
    /// the height to hold all items in the currently selected list
    /// </summary>
    public void RecalculateSize() {
        titleHeight      = (ShowTitle || ShowMenusSlider) ? ActiveFont.LineHeight: 0f;
        menuHeight       = 0f;
        // The icon is drawn iconXPadding to the right of the label (see Render)
        float titleWidth = ShowTitle ? ActiveFont.Measure(Label).X + (ShowIcon ? Icon.Width + iconXPadding : 0f)
                                     : 0f;
        leftColumnWidth  = titleWidth;
        rightColumnWidth = 0f;
        optionsWidth     = 0f;
        compactWidth     = titleWidth;

        // Calculate width for the title and all submenus
        foreach (LabeledMenu menu in menus) {
            if (ShowMenusSlider) {
                float optionWidth = optionTextScale * ActiveFont.Measure(menu.Label).X + bracketsReservedWidth;
                rightColumnWidth  = Math.Max(rightColumnWidth, optionWidth);
                optionsWidth      = Math.Max(optionsWidth, optionWidth);
                compactWidth      = Math.Max(compactWidth, titleWidth + optionWidth);
            }
            foreach (TextMenu.Item item in menu.Menu) {
                if (item.IncludeWidthInMeasurement) {
                    float leftWidth  = ItemIndent + item.LeftWidth();
                    float rightWidth = item.RightWidth();
                    leftColumnWidth  = Math.Max(leftColumnWidth, leftWidth);
                    rightColumnWidth = Math.Max(rightColumnWidth, rightWidth);
                    compactWidth     = Math.Max(compactWidth, leftWidth + rightWidth);
                }
            }
        }
        // Calculate height for the current menu
        bool first = true;
        if (CurrentMenu != null && ShowMenu) {
            foreach (TextMenu.Item item in CurrentMenu) {
                if (item.Visible) {
                    menuHeight += item.Height();
                    if (!first || ShowTitle || ShowMenusSlider) {
                        // Add space between items and between the title and first item if we're showing the title
                        menuHeight += ItemSpacing;
                    }
                    first = false;
                }
            }
            foreach (AddRemoveItemState addRemoveItem in addRemoveItems) {
                if (addRemoveItem.MenuIndex == MenuIndex) {
                    menuHeight += addRemoveItem.EasedHeight((!first || ShowTitle || ShowMenusSlider) ? ItemSpacing
                                                                                                     : 0f);
                    first = false;
                }
            }
        }
        // Allow subclass to modify the results of the size calculation
        TweakSizeCalc(ref titleHeight, ref menuHeight, titleWidth, ref leftColumnWidth, ref rightColumnWidth,
                      ref optionsWidth, ref compactWidth);
        // Hack to avoid inserting extra spacing when we're not showing anything. This could be done less hackily
        // with Visible, but then height transitions aren't smooth
        if (titleHeight == 0f && menuHeight == 0f) {
            titleHeight = (parent != null) ? -parent.ItemSpacing
                                           : (Container != null) ? -Container.ItemSpacing : 0f;
        }
        // Propagate size change
        parent?.RecalculateSize();
    }

    /// <summary>
    /// Hook to allow subclasses to modify the results of <see cref="RecalculateSize"/>
    /// (for example, to reserve additional space for displaying things the base class doesn't know about)
    /// </summary>
    protected virtual void TweakSizeCalc(ref float titleHeight, ref float menuHeight, float titleWidth,
                                         ref float leftColumnWidth, ref float rightColumnWidth, ref float optionsWidth,
                                         ref float compactWidth) {}

    public override float LeftWidth() {
        return CompactMode ? compactWidth - CompactRightWidth : leftColumnWidth;
    }

    public override float RightWidth() {
        return CompactMode ? CompactRightWidth : rightColumnWidth;
    }

    public override float Height() {
        return titleHeight + EasedMenuHeight;
    }

    /// <summary>
    ///     Get the Y position of a <see cref="TextMenu.Item"/> relative to the menu position
    /// </summary>
    /// <param name="item">
    ///     An item contained in the currently selected list, or null to get the offset of the title
    /// </param>
    public float GetYOffsetOf(TextMenu.Item item) {
        float offset = (parent != null ? parent.GetYOffsetOf(this) : Container.GetYOffsetOf(this)) -
                           Height() * 0.5f;
        if (item == null) {
            // No items to show or we want the offset of the title itself
            return offset + titleHeight * 0.5f;
        }
        offset += titleHeight;
        foreach (TextMenu.Item child in CurrentMenu) {
            if (child.Visible) {
                offset += child.Height() + ItemSpacing;
            }
            if (child == item) {
                break;
            }
        }
        return offset - item.Height() * 0.5f - ItemSpacing;
    }

    // ============================================= Handle hover ======================================================
    /// <summary>
    ///     Like <see cref="TextMenu.Item.Enter"/>, but runs <paramref name="onEnter"/> after
    ///     <see cref="DefaultOnEnter"/> instead of replacing it
    /// </summary>
    /// <remarks>
    ///     Only takes effect when called on a submenu-typed reference. Called through a <see cref="TextMenu.Item"/>
    ///     reference, the base method replaces <see cref="TextMenu.Item.OnEnter"/> and breaks the submenu.
    /// </remarks>
    public new RecursiveSubMenuBase Enter(Action onEnter) {
        OnEnter = () => {
            DefaultOnEnter();
            onEnter?.Invoke();
        };
        return this;
    }

    /// <summary>
    ///     Like <see cref="TextMenu.Item.Leave"/>, but runs <paramref name="onLeave"/> before
    ///     <see cref="DefaultOnLeave"/> instead of replacing it
    /// </summary>
    /// <remarks>
    ///     Only takes effect when called on a submenu-typed reference. Called through a <see cref="TextMenu.Item"/>
    ///     reference, the base method replaces <see cref="TextMenu.Item.OnLeave"/> and breaks the submenu.
    /// </remarks>
    public new RecursiveSubMenuBase Leave(Action onLeave) {
        OnLeave = () => {
            onLeave?.Invoke();
            DefaultOnLeave();
        };
        return this;
    }

    /// <summary>
    ///     Tasks that must be done from <see cref="TextMenu.Item.OnEnter"/> for the submenu implementation
    ///     to function properly
    /// </summary>
    /// <remarks>
    ///     If you assign an action to <see cref="TextMenu.Item.OnEnter"/>, you must call this method
    ///     from that action for submenus to continue working properly
    /// </remarks>
    public void DefaultOnEnter() {
        if (!added) {
            // Annoyingly, vanilla calls OnEnter before Added (but after setting this.Container, hence the separate
            // added bool) if we're the first item in the page, causing all kinds of problems. Just ignore the call if
            // this happens -- if it's a TextMenuPage, it'll call us again on entering the page anyway.
            return;
        }
        // Silly everest sometimes calls OnEnter multiple times in a row, so this needs to be idempotent
        if (!receivedHover) {
            // Steal autoscroll responsibilies from the parent menu
            if (parent == null) {
                AutoScroll           = Container.AutoScroll;
                Container.AutoScroll = false;
            } else {
                AutoScroll        = parent.AutoScroll;
                parent.AutoScroll = false;
            }
            receivedHover = true;
            bool paging   = CoreModule.Settings.MenuPageDown.Pressed ||
                            CoreModule.Settings.MenuPageUp.Pressed;
            if (AutoEnter) {
                // With slightly different Enter and Exit rules we could do this for all submenus rather than just
                // AutoEnter ones, but keeping focus with the parent where possible helps with pageup / pagedown
                Enter(Input.MenuDown.Pressed || CoreModule.Settings.MenuPageDown.Pressed
                          ? 1
                          : Input.MenuUp.Pressed || CoreModule.Settings.MenuPageUp.Pressed
                              ? -1
                              : 0,
                      !paging);
                // Make page up / page down reasonably continuous when auto-entering submenus
                // Using the current view position as an (imperfect) estimate for the starting page position.
                // That position is in a different reference frame compared to the one used by MoveToY, so this math
                // does the conversion
                float offsetSpaceContainerY = (Engine.Height / 2) + Container.Height * Container.Justify.Y -
                                                  Container.Position.Y;
                if (CoreModule.Settings.MenuPageDown.Pressed) {
                    MoveToY(offsetSpaceContainerY + 1080f, 1, false);
                } else if (CoreModule.Settings.MenuPageUp.Pressed) {
                    MoveToY(offsetSpaceContainerY - 1080f, -1, false);
                }
                Input.MenuDown.ConsumePress();
                Input.MenuUp.ConsumePress();
            } else if (!paging) {
                titleWiggler.Start();
            }
        }
    }

    /// <summary>
    ///     Tasks that must be done from <see cref="TextMenu.Item.OnLeave"/> for the submenu implementation
    ///     to function properly
    /// </summary>
    /// <remarks>
    ///     If you assign an action to <see cref="TextMenu.Item.OnLeave"/>, you must call this method
    ///     from that action for autoscrolling to continue working properly
    /// </remarks>
    public void DefaultOnLeave() {
        // Silly everest sometimes calls OnLeave multiple times in a row, so this needs to be idempotent
        if (receivedHover) {
            // Make sure we exit if we haven't already
            // (this happens during page up / page down for auto-exit submenus)
            if (Focus != FocusType.None) {
                ForceExit();
            }
            // Return autoscroll responsibilities to the parent
            if (parent == null) {
                Container.AutoScroll = AutoScroll;
                AutoScroll           = false;
            } else {
                parent.AutoScroll = AutoScroll;
                AutoScroll        = false;
            }
            receivedHover = false;
        }
    }

    // ============================================= Enter / exit ======================================================
    protected virtual void Enter(int dir, bool wiggle) {
        if (parent == null) {
            Container.Focused = false;
        } else {
            parent.Focus = FocusType.Child;
        }
        if (Focus == FocusType.None) {
            // We weren't selected at all, need to decide whether to select the title or body
            if (dir >= 0) {
                // Moving down or entering with confirm, go to the top (title or top of body)
                if (TitleSelectable && AutoEnter) {
                    // For AutoEnter submenus only, we'll get called on hover to take focus automatically
                    // Keep focus on the title for now if we can
                    Focus = FocusType.Title;
                    if (wiggle) {
                        titleWiggler.Start();
                    }
                } else {
                    // For non-AutoEnter submenus, skip to the body so we don't have to take focus on hover
                    // For AutoEnter submenus where we can't select the title, skip to the body
                    Focus = FocusType.Body;
                    FirstSelection(wiggle);
                }
            } else {
                // Moving up, go to the bottom (bottom of body or title)
                if (AutoEnter && CurrentMenu.Count > 0) {
                    // Go to the bottom of the body if we can
                    Focus = FocusType.Body;
                    LastSelection(wiggle);
                } else {
                    // Can't enter to body, have to skip to title
                    // Assuming we only see these conditions when TitleSelectable is true
                    Focus = FocusType.Title;
                    if (wiggle) {
                        titleWiggler.Start();
                    }
                }
            }
        } else if (Focus == FocusType.Title) {
            // Title was selected, we definitely want to go to the body
            Focus = FocusType.Body;
            FirstSelection(wiggle);
        }
        // Shouldn't get called with FocusType >= Body
    }

    protected virtual void Exit(int dir, bool wiggle, bool exitAll, out bool playedScrollSFX, out bool playedBackSFX) {
        playedBackSFX         = false;
        playedScrollSFX       = false;
        bool shouldExitParent = exitAll;
        Current?.OnLeave?.Invoke();

        // First decide how far to exit
        if (Focus == FocusType.Body) {
            // Body was selected, need to decide whether to move to the title or outside the submenu entirely
            if (dir > 0) {
                // Moving down, exit entirely and move in the parent
                Focus = FocusType.None;
            } else if (dir < 0) {
                // Moving up, either move to the title or skip it and move in the parent
                if (TitleSelectable) {
                    Focus = FocusType.Title;
                } else {
                    Focus = FocusType.None;
                }
            } else {
                // Exiting with Cancel, exit to title or recursively exit
                if (!RecursiveExit) {
                    // Note: forcing title selection even if !TitleSelectable here
                    // Thus menus without a title to select should always set RecursiveExit
                    Focus = FocusType.Title;
                } else {
                    Focus            = FocusType.None;
                    shouldExitParent = true;
                }
            }
        } else if (Focus == FocusType.Title) {
            // Title was selected, definitely exit entirely
            Focus = FocusType.None;
            if (dir == 0) {
                // We want to act like the parent had focus instead of us, so exit the parent as well
                shouldExitParent = true;
            }
        }

        // We chose where to exit to, now finish the operation
        if (Focus == FocusType.Title) {
            if (wiggle) {
                titleWiggler.Start();
            }
            if (!AutoEnter) {
                // Exit all the way if we don't need to stay in title focus
                Focus = FocusType.None;
            }
        }
        if (Focus == FocusType.None) {
            if (parent == null) {
                Container.Focused = true;
                if (dir != 0) {
                    Container.MoveSelection(dir, wiggle);
                    playedScrollSFX = true;
                }
            } else {
                parent.Focus = FocusType.Body;
                if (dir != 0) {
                    parent.MoveSelection(dir, true, wiggle, out playedScrollSFX);
                }
            }
        }
        if (shouldExitParent) {
            if (parent == null) {
                // Assuming the container will play the back button SFX
                playedBackSFX = true;
                if (Input.MenuCancel.Pressed) {
                    Container.OnCancel?.Invoke();
                } else if (Input.ESC.Pressed) {
                    Container.OnESC?.Invoke();
                } else if (Input.Pause.Pressed) {
                    Container.OnPause?.Invoke();
                }
                // Annoyingly, the main-menu version of the mod options menu doesn't use OnCancel to exit,
                // instead it's a separate check and external handling that we have to replicate here
                if (InOverworldMenu) {
                    OuiModOptions.Instance.Overworld.Goto<OuiMainMenu>();
                    // Container doesn't play the back button SFX in this case
                    playedBackSFX = false;
                }
            } else {
                parent.Exit(0, wiggle, exitAll, out _, out playedBackSFX);
            }
        }
    }

    protected virtual void ForceExit() {
        // The parent requested we return control immediately, no fancy exit-to-title or recursive exit
        Current?.OnLeave?.Invoke();
        Focus = FocusType.None;
        if (parent == null) {
            Container.Focused = true;
        } else {
            parent.Focus = FocusType.Body;
        }
    }

    // =============================== Give up input to a page or overlay opened on top ================================
    /// <summary>
    ///     The innermost submenu that has the selection, this one or one nested in it, or null if none does
    /// </summary>
    public IInputHoldingItem InputHolder {
        get {
            RecursiveSubMenuBase subMenu = this;
            while (subMenu is { Focus: FocusType.Child }) {
                subMenu = subMenu.Current as RecursiveSubMenuBase;
            }
            return (subMenu != null && subMenu.Focus != FocusType.None) ? subMenu : null;
        }
    }

    /// <summary>
    ///     Takes the focus away from this submenu until the returned action is called, which restores it
    /// </summary>
    /// <param name="stopAutoScroll">Also turn off <see cref="AutoScroll"/> in the meantime</param>
    public Action SuspendInput(bool stopAutoScroll) {
        FocusType savedFocus = Focus;
        bool savedAutoScroll = AutoScroll;
        Focus                = FocusType.None;
        if (stopAutoScroll) {
            AutoScroll = false;
        }
        return () => {
            Focus = savedFocus;
            if (stopAutoScroll) {
                AutoScroll = savedAutoScroll;
            }
        };
    }

    // ================================= Force enter from somewhere else in the menu ===================================
    /// <summary>
    ///     Moves focus and selection to this submenu (the title or an item within it), recursively setting
    ///     parent selections and focus to maintain a valid state
    /// </summary>
    /// <param name="item">
    ///     The item within this submenu to move the selection to, or null to move the selection to the submenu title.
    ///     If the item is in a menu other than the one shown, the submenu switches to that menu. If it is not in this
    ///     submenu at all, nothing happens.
    /// </param>
    /// <param name="autoScroll">
    ///     Whether this submenu should autoscroll after taking focus
    /// </param>
    /// <param name="snapScroll">
    ///     Whether this submenu should instantly snap the scroll position to the new value rather than waiting for
    ///     the smooth autoscroll to apply (<paramref name="autoScroll"/> must be true for this to apply)
    /// </param>
    public void MoveSelectionTo(TextMenu.Item item, bool autoScroll, bool snapScroll) {
        if (item != null) {
            int itemMenuIndex = menus.FindIndex((LabeledMenu menu) => menu.Menu.Contains(item));
            if (itemMenuIndex < 0) {
                // Not an item of this submenu
                return;
            }
            if (itemMenuIndex != MenuIndex) {
                StartMenuSwitch(itemMenuIndex - MenuIndex);
            }
        }
        // Take the selection away from wherever it is now, like the TextMenu does when its selection moves
        RecursiveSubMenuBase topSubMenu = this;
        while (topSubMenu.parent != null) {
            topSubMenu = topSubMenu.parent;
        }
        if (Container.Current != topSubMenu) {
            Container.Current?.OnLeave?.Invoke();
        } else {
            topSubMenu.ReleaseFocus();
        }

        receivedHover = true;
        Focus         = (item != null) ? FocusType.Body
                                       : (TitleSelectable && AutoEnter ? FocusType.Title : FocusType.None);
        AutoScroll    = autoScroll;
        if (item != null) {
            Selection = CurrentMenu.IndexOf(item);
            Current.OnEnter?.Invoke();
        }
        if (parent == null) {
            Container.Focused    = (item == null);
            Container.AutoScroll = false;
            Container.Selection  = Container.IndexOf(this);
        } else {
            parent.MoveSelectionToChild(this, item == null);
        }
        RecalculateSize();
        Container.RecalculateSize();
        if (autoScroll && snapScroll) {
            Container.Position.Y = ScrollTargetY;
        }
    }

    private void MoveSelectionToChild(TextMenu.Item item, bool focus) {
        receivedHover = true;
        Focus         = focus ? FocusType.Body : FocusType.Child;
        AutoScroll    = false;
        Selection     = CurrentMenu.IndexOf(item);
        if (parent == null) {
            Container.Focused    = false;
            Container.AutoScroll = false;
            Container.Selection  = Container.IndexOf(this);
        } else {
            parent.MoveSelectionToChild(this, false);
        }
    }

    // =============================== Handle button presses when the parent has focus =================================
    public override void ConfirmPressed() {
        if (CurrentMenu.Count > 0) {
            Audio.Play(SFX.ui_main_button_select);
            Enter(1, true);
            Input.MenuConfirm.ConsumePress();
        }
    }

    public override void LeftPressed() {
        if (MenuIndex > 0) {
            Audio.Play(SFX.ui_main_button_toggle_off);
            StartMenuSwitch(-1);
        }
    }

    public override void RightPressed() {
        if (MenuIndex < menus.Count - 1) {
            Audio.Play(SFX.ui_main_button_toggle_on);
            StartMenuSwitch(1);
        }
    }

    /// <summary>
    ///     Switch to another of the submenu's menus, as if the player had pressed left or right on the title.
    ///     If the selection is inside the submenu, it is given back to the parent first.
    /// </summary>
    /// <param name="menuIndex">
    ///     Index of the menu to switch to; ignored if out of range
    /// </param>
    /// <param name="animate">
    ///     Whether to animate the height change, as when the player switches menus
    /// </param>
    public void SelectMenu(int menuIndex, bool animate = true) {
        if (Container == null) {
            // Not added yet, so the menus aren't either: Added starts on this menu
            pendingMenuSelection = menuIndex;
            MenuIndex            = menuIndex;
            return;
        }
        if (menuIndex < 0 || menuIndex >= menus.Count || menuIndex == MenuIndex) {
            return;
        }
        if (Focus >= FocusType.Body) {
            ReleaseFocus();
        }
        if (animate) {
            StartMenuSwitch(menuIndex - MenuIndex);
        } else {
            MenuIndex = menuIndex;
            RecalculateSize();
            OnValueChange?.Invoke(MenuIndex);
        }
    }

    protected void StartMenuSwitch(int dir) {
        switchingMenus       = true;
        switchFromMenuIndex  = MenuIndex;
        switchFromMenuHeight = EasedMenuHeight;
        switchMenuEase       = 0f;
        MenuIndex += dir;
        lastDir = dir;
        RecalculateSize();
        switchMenuEaseRate = Math.Max(1f / maxSwitchMenuTime,
                                      switchMenuVelocity / Math.Abs(menuHeight - switchFromMenuHeight));
        if (RenderingMenu == CurrentMenu) {
            menuWiggler.Start();
        }
        if (dir != 0) {
            ValueWiggler.Start();
            OnValueChange?.Invoke(MenuIndex);
        }
    }

    // =================================================================================================================
    public override void Update() {
        if (switchMenuEase < 1f) {
            switchMenuEase = Calc.Approach(switchMenuEase, 1f, switchMenuEaseRate * Engine.RawDeltaTime);
            if (switchMenuEase == 1f) {
                if (RenderingMenu != CurrentMenu) {
                    menuWiggler.Start();
                }
                switchingMenus = false;
            }
            parent?.RecalculateSize();
        }
        if (addRemoveItems.Count > 0) {
            foreach (AddRemoveItemState addRemoveItem in addRemoveItems) {
                addRemoveItem.Update();
            }
            // Take finished animations out of the list before finishing them, since the done callbacks may start new
            // ones
            List<AddRemoveItemState> finished = addRemoveItems.FindAll((AddRemoveItemState addRemoveItem) =>
                                                                          addRemoveItem.Done);
            addRemoveItems.RemoveAll((AddRemoveItemState addRemoveItem) => addRemoveItem.Done);
            foreach (AddRemoveItemState addRemoveItem in finished) {
                FinishAnimation(addRemoveItem);
            }
            RecalculateSize();
        }
        sine += Engine.RawDeltaTime;
        base.Update();

        if (Focus == FocusType.Body && CurrentMenu != null && !switchingMenus) {
            if (Input.MenuDown.Pressed) {
                if (!Input.MenuDown.Repeating || !MoveWouldWrap(1, true)) {
                    MoveSelection(1, true, true, out bool playedScrollSFX);
                    if (!playedScrollSFX) {
                        Audio.Play(SFX.ui_main_roll_down);
                    }
                    Input.MenuDown.ConsumePress();
                }
            } else if (Input.MenuUp.Pressed) {
                if (!Input.MenuUp.Repeating || !MoveWouldWrap(-1, true)) {
                    MoveSelection(-1, true, true, out bool playedScrollSFX);
                    if (!playedScrollSFX) {
                        Audio.Play(SFX.ui_main_roll_up);
                    }
                    Input.MenuUp.ConsumePress();
                }
            }
            if (Current != null) {
                if (Input.MenuLeft.Pressed) {
                    Current.LeftPressed();
                }
                if (Input.MenuRight.Pressed) {
                    Current.RightPressed();
                }
                if (Input.MenuConfirm.Pressed) {
                    Current.ConfirmPressed();
                    Current.OnPressed?.Invoke();
                }
                if (Input.MenuJournal.Pressed && Current.OnAltPressed != null) {
                    Current.OnAltPressed();
                }
            }
        } else if (Focus == FocusType.Title) {
            // In title focus, listen to all the same inputs but handle them differently
            if (Input.MenuDown.Pressed) {
                bool playedScrollSFX = false;
                if (AutoEnter && CurrentMenu.Count > 0) {
                    Enter(1, true);
                } else {
                    Exit(1, true, false, out playedScrollSFX, out _);
                }
                if (!playedScrollSFX) {
                    Audio.Play(SFX.ui_main_roll_down);
                }
                Input.MenuDown.ConsumePress();
            } else if (Input.MenuUp.Pressed) {
                Exit(-1, true, false, out bool playedScrollSFX, out _);
                if (!playedScrollSFX) {
                    Audio.Play(SFX.ui_main_roll_up);
                }
                Input.MenuUp.ConsumePress();
            }
            if (Input.MenuLeft.Pressed) {
                LeftPressed();
            }
            if (Input.MenuRight.Pressed) {
                RightPressed();
            }
            if (Input.MenuConfirm.Pressed) {
                ConfirmPressed();
            }
            if (Input.MenuJournal.Pressed) {
                OnAltPressed?.Invoke();
            }
        }
        if (Focus == FocusType.Title || Focus == FocusType.Body) {
            // Common behavior between title and body focus since MoveToY and Exit handle the differences
            if (CoreModule.Settings.MenuPageDown.Pressed) {
                MoveToY(GetYOffsetOf(Current) + 1080f, 1, true);
                Audio.Play(SFX.ui_main_roll_down);
                CoreModule.Settings.MenuPageDown.ConsumePress();
            } else if (CoreModule.Settings.MenuPageUp.Pressed) {
                MoveToY(GetYOffsetOf(Current) - 1080f, -1, true);
                Audio.Play(SFX.ui_main_roll_up);
                CoreModule.Settings.MenuPageUp.ConsumePress();
            }
            bool exitTopMenu = Input.ESC.Pressed || Input.Pause.Pressed;
            if (!Input.MenuConfirm.Pressed && (Input.MenuCancel.Pressed || exitTopMenu)) {
                Exit(0, true, exitTopMenu, out _, out bool playedBackSFX);
                if (!playedBackSFX) {
                    Audio.Play(SFX.ui_main_button_back);
                }
                Input.MenuCancel.ConsumePress();
                Input.ESC.ConsumePress();
                Input.Pause.ConsumePress();
            }
        }

        updatingItems.Clear();
        foreach (LabeledMenu menu in menus) {
            updatingItems.AddRange(menu.Menu);
        }
        foreach (TextMenu.Item item in updatingItems) {
            if (item.Container == null) {
                // Removed by an item updated earlier this frame
                continue;
            }
            item.OnUpdate?.Invoke();
            item.Update();
        }

        if (Settings.Instance.DisableFlashes) {
            HighlightColor = TextMenu.HighlightColorA;
        } else if (Engine.Scene.OnRawInterval(0.1f)) {
            if (HighlightColor == TextMenu.HighlightColorA) {
                HighlightColor = TextMenu.HighlightColorB;
            } else {
                HighlightColor = TextMenu.HighlightColorA;
            }
        }

        if (AutoScroll) {
            Container.Position.Y += (ScrollTargetY - Container.Position.Y) *
                                        (1f - (float) Math.Pow(0.01f, Engine.RawDeltaTime));
        }
    }

    // =================================================================================================================
    public override void Render(Vector2 position, bool highlighted) {
        Vector2 top = new Vector2(position.X, position.Y - (Height() / 2));

        float alpha      = Container.Alpha;
        Color titleColor = Disabled ? Color.DarkSlateGray
                                    : ((highlighted || Focus == FocusType.Title ? Container.HighlightColor
                                                                                : Color.White) * alpha);
        Color strokeColor = Color.Black * (alpha * alpha * alpha);

        // Render title and icon
        Vector2 titlePosition = top + (Vector2.UnitY * titleHeight / 2) + new Vector2(0f, titleWiggler.Value * 8f);
        Vector2 justify       = new Vector2(0f, 0.5f);
        Vector2 iconJustify   = new Vector2(ActiveFont.Measure(Label).X + 0.5f * Icon.Width + iconXPadding, 5f);
        if (ShowIcon) {
            DrawIcon(titlePosition, Icon, iconJustify, true,
                     (Disabled || CurrentMenu?.Count < 1 ? Color.DarkSlateGray
                                                         : (Focus != FocusType.None ? Container.HighlightColor
                                                                                    : Color.White)) * alpha,
                     0.8f);
        }
        if (ShowTitle) {
            ActiveFont.DrawOutline(Label, titlePosition, justify, Vector2.One, titleColor, 2f, strokeColor);
        }

        // Render menu select
        if (menus.Count > 0 && ShowMenusSlider) {
            ActiveFont.DrawOutline(menus[MenuIndex].Label,
                                   titlePosition + new Vector2(Container.Width - optionsWidth * 0.5f +
                                                                   lastDir * ValueWiggler.Value * 8f,
                                                               0f),
                                   new Vector2(0.5f, 0.5f), Vector2.One * optionTextScale, titleColor, 2f, strokeColor);
            Vector2 wiggle = Vector2.UnitX * (highlighted ? ((float) Math.Sin(sine * 4f) * 4f)
                                                          : 0f);
            Color arrowColor = MenuIndex > 0 ? titleColor : (Color.DarkSlateGray * alpha);
            Vector2 arrowPosition =
                titlePosition + new Vector2(Container.Width - optionsWidth + 40f +
                                                ((lastDir < 0) ? (-ValueWiggler.Value * 8f) : 0f),
                                            0f) -
                (MenuIndex > 0 ? wiggle : Vector2.Zero);
            ActiveFont.DrawOutline("<", arrowPosition, new Vector2(0.5f, 0.5f),
                                   Vector2.One, arrowColor, 2f, strokeColor);

            arrowColor = MenuIndex < menus.Count - 1 ? titleColor : (Color.DarkSlateGray * alpha);
            arrowPosition =
                titlePosition + new Vector2(Container.Width - 40f +
                                                ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f),
                                            0f) +
                (MenuIndex < menus.Count - 1 ? wiggle : Vector2.Zero);
            ActiveFont.DrawOutline(">", arrowPosition, new Vector2(0.5f, 0.5f),
                                   Vector2.One, arrowColor, 2f, strokeColor);
        }

        // Render items
        if (RenderingMenu != null) {
            Vector2 menuPosition = new Vector2(top.X + ItemIndent, top.Y + titleHeight) +
                                       new Vector2(0f, menuWiggler.Value * 8f);
            if (ShowTitle || ShowMenusSlider) {
                // Add spacing between title and first item if we're showing the title
                menuPosition += new Vector2(0f, ItemSpacing);
            }
            foreach (TextMenu.Item item in RenderingMenu) {
                if (item.Visible) {
                    float height = item.Height();
                    Vector2 itemPosition = menuPosition + new Vector2(0f,
                                                                      height * 0.5f + item.SelectWiggler.Value * 8f);
                    if (itemPosition.Y + height * 0.5f > 0f &&
                            itemPosition.Y - height * 0.5f < Engine.Height) {
                        // Temporarily spoof Container.Width to properly handle the right-alignment of indented items
                        // since existing items only look at the top-level menu width to do right-alignment
                        Container.Width -= ItemIndent;
                        try {
                            item.Render(itemPosition, Focus == FocusType.Body && Current == item);
                        } finally {
                            Container.Width += ItemIndent;
                        }
                    }
                    menuPosition.Y += height + ItemSpacing;
                } else if (addRemoveItems.Find((AddRemoveItemState addRemoveItem) => addRemoveItem.Item == item)
                               is AddRemoveItemState addRemoveItem) {
                    // Add spacing for the smooth add/remove animation
                    // Searching the list for every item seems inefficient but it should always be a small list
                    menuPosition.Y += addRemoveItem.EasedHeight(ItemSpacing);
                }
            }
        }
    }

    private static void DrawIcon(Vector2 position, MTexture icon, Vector2 justify,
                                 bool outline, Color color, float scale) {
        if (outline) {
            icon.DrawOutlineCentered(position + justify, color);
        } else {
            icon.DrawCentered(position + justify, color, scale);
        }
    }
}
