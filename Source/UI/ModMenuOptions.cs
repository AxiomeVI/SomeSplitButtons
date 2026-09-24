using System.Collections.Generic;
using Celeste.Mod.SomeSplitButtons.Splits;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>The mod's section of Mod Options.</summary>
internal static class ModMenuOptions {
    internal static void CreateMenu(TextMenu menu) {
        // Belongs to the Save and Quit button, so it is built first and slotted in behind that row
        // below. Nothing else in the loop needs a companion, which is why this is the one exception
        // rather than a second table.
        TextMenu.OnOff saveAndQuitAndReenter =
            new(Dialog.Clean(DialogIds.SaveAndQuitAndReenterId),
                SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter);
        saveAndQuitAndReenter.Change(value => SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter = value);

        // One row per split button, in the order the player meets them in the pause menu — the mod
        // menu used to list them in a third order of its own.
        List<TextMenu.Item> subOptions = new();
        List<TextMenu.OnOff> featureRows = new();
        foreach (SplitFeature feature in SplitFeatures.InMenuOrder) {
            SplitFeature captured = feature;
            TextMenu.OnOff row = new(Dialog.Clean(feature.NameId), feature.Enabled());
            if (feature == SplitFeatures.SaveAndQuit) {
                row.Change(value => {
                    captured.Toggle(value);
                    saveAndQuitAndReenter.Disabled = !value;
                });
            }
            else {
                row.Change(value => captured.Toggle(value));
            }

            featureRows.Add(row);
            subOptions.Add(row);
            if (feature == SplitFeatures.SaveAndQuit) subOptions.Add(saveAndQuitAndReenter);
        }

        // After both buttons it applies to, which is where a player looks for it.
        TextMenu.OnOff berryCollectProtection =
            new(Dialog.Clean(DialogIds.BerryCollectProtectionId),
                SomeSplitButtonsModule.Settings.BerryCollectProtection);
        berryCollectProtection.Change(value => SomeSplitButtonsModule.Settings.BerryCollectProtection = value);
        subOptions.Add(berryCollectProtection);

        TextMenu.Button keybindButton = new(Dialog.Clean(DialogIds.KeybindConfigId));
        keybindButton.Pressed(() => {
            menu.Focused = false;
            KeybindConfigUi ui = new() {OnClose = () => menu.Focused = true};
            // The scene is captured, not read again at end of frame: Engine.Scene can be replaced
            // between the two, and the lambda would then flush the entity lists of whatever scene
            // came after while this one kept the menu queued.
            Scene scene = Engine.Scene;
            scene.Add(ui);
            scene.OnEndOfFrame += () => scene.Entities.UpdateLists();
        });
        subOptions.Add(keybindButton);

        // Everything below the master toggle appears and disappears with it. One list, so a row
        // added above cannot be forgotten here.
        void SetSubOptionsVisible(bool visible) {
            foreach (TextMenu.Item item in subOptions) item.Visible = visible;
            // Not part of the visibility rule, but always true alongside it: the re-entry option
            // belongs to the Save and Quit button and is greyed out whenever that button is off.
            saveAndQuitAndReenter.Disabled = !SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton;
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
        saveAndQuitAndReenter.AddDescription(menu, Dialog.Clean(DialogIds.SaveAndQuitAndReenterDescId));
        berryCollectProtection.AddDescription(menu, Dialog.Clean(DialogIds.BerryCollectProtectionDescId));
    }
}
