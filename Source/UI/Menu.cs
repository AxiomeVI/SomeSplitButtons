using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.UI;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Menu;

public static class ModMenuOptions {
    public static void CreateMenu(TextMenu menu)
    {
        TextMenu.OnOff _showSkipCutsceneSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSkipCutsceneSplitButtonId),
            SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton).Change(
                b => SplitFeatures.SkipCutscene.Toggle(b)
        );

        TextMenu.OnOff _saveAndQuitAndRetry = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.SaveAndQuitAndRetryId),
            SomeSplitButtonsModule.Settings.SaveAndQuitAndRetry).Change(
                b => SomeSplitButtonsModule.Settings.SaveAndQuitAndRetry = b
        );

        TextMenu.OnOff _showSaveAndQuitSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSaveAndQuitSplitButtonId),
            SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton).Change(
                b =>
                {
                    SplitFeatures.SaveAndQuit.Toggle(b);
                    _saveAndQuitAndRetry.Disabled = !b;
                }
        );

        TextMenu.OnOff _showReturnToMapSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableReturnToMapSplitButtonId),
            SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton).Change(
                b => SplitFeatures.ReturnToMap.Toggle(b)
        );

        TextMenu.Button keybindButton = new TextMenu.Button(Dialog.Clean(DialogIds.KeybindConfigId)) {
            Visible = SomeSplitButtonsModule.Settings.Enabled
        };
        keybindButton.Pressed(() => {
            menu.Focused = false;
            var ui = new KeybindConfigUi();
            ui.OnClose = () => menu.Focused = true;
            Engine.Scene.Add(ui);
            Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
        });

        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), SomeSplitButtonsModule.Settings.Enabled).Change(
            value =>
            {
                SomeSplitButtonsModule.Settings.Enabled = value;
                _showSkipCutsceneSplitButton.Visible = value;
                _showSaveAndQuitSplitButton.Visible = value;
                _saveAndQuitAndRetry.Visible = value;
                _showReturnToMapSplitButton.Visible = value;
                keybindButton.Visible = value;
                _saveAndQuitAndRetry.Disabled = !SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton;
                SplitFeatures.ResetAll();
                // Unconditional where this used to test ShowSkipCutsceneSplitButton as well. What it
                // refreshes is only ever read by an enabled feature's Update, and a level load
                // refreshes it regardless — so the extra test could only ever agree with what was
                // already there.
                if (value && Engine.Scene is Level level) SplitFeatures.RefreshAll(level);
            }
        ));

        menu.Add(_showSkipCutsceneSplitButton);
        menu.Add(_showSaveAndQuitSplitButton);
        menu.Add(_saveAndQuitAndRetry);
        menu.Add(_showReturnToMapSplitButton);
        menu.Add(keybindButton);

        _showSkipCutsceneSplitButton.Visible = SomeSplitButtonsModule.Settings.Enabled;
        _showSaveAndQuitSplitButton.Visible = SomeSplitButtonsModule.Settings.Enabled;
        _saveAndQuitAndRetry.Visible = SomeSplitButtonsModule.Settings.Enabled;
        _showReturnToMapSplitButton.Visible = SomeSplitButtonsModule.Settings.Enabled;
        _saveAndQuitAndRetry.Disabled = !SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton;

        _saveAndQuitAndRetry.AddDescription(menu, Dialog.Clean(DialogIds.SaveAndQuitAndRetryDescId));
    }
}
