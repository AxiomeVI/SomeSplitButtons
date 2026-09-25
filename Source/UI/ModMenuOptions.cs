using System;
using System.Collections.Generic;
using Celeste.Mod.CelesteHotkeys;
using Celeste.Mod.SomeSplitButtons.Splits;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>The mod's section of Mod Options.</summary>
internal static class ModMenuOptions {
    internal static void CreateMenu(TextMenu menu) {
        // One companion row per feature that has one, built before the loop because each is slotted
        // in directly behind its own feature's row. A table rather than two special cases: the
        // second companion is what made the old "one exception" comment false.
        Dictionary<SplitFeature, TextMenu.OnOff> companions = new() {
            [SplitFeatures.SaveAndQuit] = MakeCompanion(
                DialogIds.SaveAndQuitAndReenterId,
                SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter,
                value => SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter = value),
            [SplitFeatures.ReturnToMap] = MakeCompanion(
                DialogIds.ReturnToMapCheckpointMenuId,
                SomeSplitButtonsModule.Settings.ReturnToMapCheckpointMenu,
                value => SomeSplitButtonsModule.Settings.ReturnToMapCheckpointMenu = value),
        };

        Dictionary<SplitFeature, string> companionDescriptions = new() {
            [SplitFeatures.SaveAndQuit] = DialogIds.SaveAndQuitAndReenterDescId,
            [SplitFeatures.ReturnToMap] = DialogIds.ReturnToMapCheckpointMenuDescId,
        };

        // One row per split button, in the order the player meets them in the pause menu — the mod
        // menu used to list them in a third order of its own.
        List<TextMenu.Item> subOptions = new();
        List<TextMenu.OnOff> featureRows = new();
        foreach (SplitFeature feature in SplitFeatures.InMenuOrder) {
            SplitFeature captured = feature;
            TextMenu.OnOff row = new(Dialog.Clean(feature.NameId), feature.Enabled());
            row.Change(value => {
                captured.Toggle(value);
                // A companion belongs to its feature and is greyed out whenever that feature is off.
                if (companions.TryGetValue(captured, out TextMenu.OnOff companion)) {
                    companion.Disabled = !value;
                }
            });

            featureRows.Add(row);
            subOptions.Add(row);
            if (companions.TryGetValue(feature, out TextMenu.OnOff own)) subOptions.Add(own);
        }

        // After both buttons it applies to, which is where a player looks for it.
        TextMenu.OnOff berryCollectProtection =
            new(Dialog.Clean(DialogIds.BerryCollectProtectionId),
                SomeSplitButtonsModule.Settings.BerryCollectProtection);
        berryCollectProtection.Change(value => SomeSplitButtonsModule.Settings.BerryCollectProtection = value);
        subOptions.Add(berryCollectProtection);

        // Last, and inside the range the master toggle hides: it is the only way to bind a hotkey.
        subOptions.Add(HotkeyMenu.OpenButton(
            menu, Hotkeys.Set, Hotkeys.Text, SomeSplitButtonsModule.Instance.SaveSettings));

        // Everything below the master toggle appears and disappears with it. One list, so a row
        // added above cannot be forgotten here.
        void SetSubOptionsVisible(bool visible) {
            foreach (TextMenu.Item item in subOptions) item.Visible = visible;
            // Not part of the visibility rule, but always true alongside it.
            foreach ((SplitFeature feature, TextMenu.OnOff companion) in companions) {
                companion.Disabled = !feature.Enabled();
            }
        }

        TextMenu.OnOff enabled =
            new(Dialog.Clean(DialogIds.EnabledId), SomeSplitButtonsModule.Settings.Enabled);
        enabled.Change(value => {
            SomeSplitButtonsModule.Settings.Enabled = value;
            SetSubOptionsVisible(value);
            SplitFeatures.ResetAll();
            if (value && Engine.Scene is Level level) SplitFeatures.RefreshAll(level);
        });

        menu.Add(enabled);
        foreach (TextMenu.Item item in subOptions) menu.Add(item);

        SetSubOptionsVisible(SomeSplitButtonsModule.Settings.Enabled);

        // After the Add calls, not before: AddDescription inserts the description at the option's
        // index in the menu and does nothing at all when the option is not in it yet.
        //
        // The descriptions are not listed in subOptions, and must not be. They start invisible and
        // only fade in from the option's OnEnter — an option hidden by the master toggle can never
        // be hovered, so it can never show its description.
        for (int i = 0; i < SplitFeatures.InMenuOrder.Length; i++) {
            string descriptionId = SplitFeatures.InMenuOrder[i].MenuDescriptionId;
            if (descriptionId != null) featureRows[i].AddDescription(menu, Dialog.Clean(descriptionId));
        }
        foreach ((SplitFeature feature, TextMenu.OnOff companion) in companions) {
            companion.AddDescription(menu, Dialog.Clean(companionDescriptions[feature]));
        }
        berryCollectProtection.AddDescription(menu, Dialog.Clean(DialogIds.BerryCollectProtectionDescId));
    }

    private static TextMenu.OnOff MakeCompanion(string labelId, bool value, Action<bool> setter) {
        TextMenu.OnOff row = new(Dialog.Clean(labelId), value);
        row.Change(v => setter(v));
        return row;
    }
}
