// MenuTools requires: RecursiveSubMenuBase.cs
namespace Celeste.Mod.SomeSplitButtons.MenuTools;

/// <summary>
/// Implement on a <see cref="TextMenu.Item"/> to be told which recursive submenu contains it, for example to call
/// <see cref="RecursiveSubMenuBase.RecalculateSize"/> when the item's size changes. The submenu sets it when the item
/// is added and resets it to null when the item is removed.
/// </summary>
public interface ISubMenuAwareItem {
    RecursiveSubMenuBase ContainingSubMenu { get; set; }
}
