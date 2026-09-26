using System;
using System.Collections.Generic;
using Celeste.Mod.CelesteHotkeys;
using Celeste.Mod.SomeSplitButtons.MenuTools;
using Celeste.Mod.SomeSplitButtons.Splits;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>The mod's section of Mod Options.</summary>
internal static class ModMenuOptions {
    // How far a follow-up row sits right of the button it belongs to.
    private const float CompanionIndent = 40f;

    internal static void CreateMenu(TextMenu menu) {
        SomeSplitButtonsModuleSettings settings = SomeSplitButtonsModule.Settings;

        // Everything below the master toggle, flush with it, appearing and disappearing with it.
        RecursiveNakedSubMenu section = new(initiallyExpanded: settings.Enabled);

        // One indented group per feature that has a follow-up row, open while that feature is on.
        // Built before the loop because each is slotted in directly behind its own feature's row.
        Dictionary<SplitFeature, RecursiveNakedSubMenu> companions = new() {
            [SplitFeatures.SaveAndQuit] = MakeCompanion(
                menu, SplitFeatures.SaveAndQuit,
                DialogIds.SaveAndQuitAndReenterId,
                DialogIds.SaveAndQuitAndReenterDescId,
                settings.SaveAndQuitAndReenter,
                value => settings.SaveAndQuitAndReenter = value),
            [SplitFeatures.ReturnToMap] = MakeCompanion(
                menu, SplitFeatures.ReturnToMap,
                DialogIds.ReturnToMapCheckpointMenuId,
                DialogIds.ReturnToMapCheckpointMenuDescId,
                settings.ReturnToMapCheckpointMenu,
                value => settings.ReturnToMapCheckpointMenu = value),
        };

        // Berry Collect Protection guards the S&Q and RTM splits only, so it is shown only while one
        // of them is on. Skip Cutscene arms unconditionally.
        static bool BerryApplies() => SplitFeatures.SaveAndQuit.Enabled() || SplitFeatures.ReturnToMap.Enabled();
        RecursiveNakedSubMenu berry = new(initiallyExpanded: BerryApplies());
        TextMenu.OnOff berryCollectProtection =
            new(Dialog.Clean(DialogIds.BerryCollectProtectionId), settings.BerryCollectProtection);
        berryCollectProtection.Change(value => settings.BerryCollectProtection = value);
        AddWithDescription(berry, berryCollectProtection, DialogIds.BerryCollectProtectionDescId, menu);

        // One row per split button, in the order the player meets them in the pause menu.
        foreach (SplitFeature feature in SplitFeatures.InMenuOrder) {
            SplitFeature captured = feature;
            TextMenu.OnOff row = new(Dialog.Clean(feature.NameId), feature.Enabled());
            row.Change(value => {
                captured.Toggle(value);
                if (companions.TryGetValue(captured, out RecursiveNakedSubMenu group)) group.Expanded = value;
                berry.Expanded = BerryApplies();
            });
            AddWithDescription(section, row, feature.MenuDescriptionId, menu);
            if (companions.TryGetValue(feature, out RecursiveNakedSubMenu own)) section.AddItem(own);
        }
        // After both buttons it applies to, which is where a player looks for it.
        section.AddItem(berry);

        // At the root, never inside `section`: the hotkey screen only unfocuses `menu`, and a submenu
        // holding the selection reads input itself, so it would keep moving behind the screen.
        // The cost is that this row snaps in and out while the section animates.
        TextMenu.Button hotkeys = HotkeyMenu.OpenButton(
            menu, Hotkeys.Set, Hotkeys.Text, SomeSplitButtonsModule.Instance.SaveSettings);

        TextMenu.OnOff enabled = new(Dialog.Clean(DialogIds.EnabledId), settings.Enabled);
        enabled.Change(value => {
            settings.Enabled = value;
            section.Expanded = value;
            hotkeys.Visible = value;
            SplitFeatures.ResetAll();
            if (value && Engine.Scene is Level level) SplitFeatures.RefreshAll(level);
        });

        // A recursive submenu must not be the menu's first item. Everest's section header is.
        menu.Add(enabled);
        menu.Add(section);
        menu.Add(hotkeys);
        hotkeys.Visible = settings.Enabled;
    }

    private static RecursiveNakedSubMenu MakeCompanion(TextMenu menu, SplitFeature feature, string labelId,
                                                       string descriptionId, bool value, Action<bool> setter) {
        RecursiveNakedSubMenu group = new(initiallyExpanded: feature.Enabled(), itemIndent: CompanionIndent);
        TextMenu.OnOff row = new(Dialog.Clean(labelId), value);
        row.Change(v => setter(v));
        AddWithDescription(group, row, descriptionId, menu);
        return group;
    }

    // Everest's AddDescription only inserts into the TextMenu itself, and adds nothing at all for an
    // option inside a submenu. This is its behaviour rebuilt as items of the submenu.
    private static void AddWithDescription(RecursiveSubMenuBase group, TextMenu.Item item, string descriptionId,
                                           TextMenu menu) {
        group.AddItem(item);
        if (descriptionId == null) return;
        ParentAwareEaseInSubHeader description = new(Dialog.Clean(descriptionId), false, menu) {
            TextColor = Color.Gray, HeightExtra = 0f,
        };
        group.AddItem(description);
        item.OnEnter += () => description.FadeVisible = true;
        item.OnLeave += () => description.FadeVisible = false;
    }
}
