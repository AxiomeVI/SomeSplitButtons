using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Celeste.Mod.SomeSplitButtons.UI;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Menu;

public static class ModMenuOptions {
    public static void CreateMenu(TextMenu menu)
    {
        TextMenu.OnOff _showSkipCutsceneSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSkipCutsceneSplitButtonId),
            SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton).Change(
                b =>
                {
                    SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton = b;
                    SkipCutsceneTimer.Reset();
                    if (b && Engine.Scene is Level level) SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex);
                }
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
                    SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton = b;
                    _saveAndQuitAndRetry.Disabled = !b;
                    SaveAndQuitTimer.Reset();
                }
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
                keybindButton.Visible = value;
                _saveAndQuitAndRetry.Disabled = !SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton;
                SkipCutsceneTimer.Reset();
                if (value && SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton && Engine.Scene is Level level) SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex);
                SaveAndQuitTimer.Reset();
            }
        ));

        menu.Add(_showSkipCutsceneSplitButton);
        menu.Add(_showSaveAndQuitSplitButton);
        menu.Add(_saveAndQuitAndRetry);
        menu.Add(keybindButton);

        _showSkipCutsceneSplitButton.Visible = SomeSplitButtonsModule.Settings.Enabled;
        _showSaveAndQuitSplitButton.Visible = SomeSplitButtonsModule.Settings.Enabled;
        _saveAndQuitAndRetry.Visible = SomeSplitButtonsModule.Settings.Enabled;
        _saveAndQuitAndRetry.Disabled = !SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton;

        _saveAndQuitAndRetry.AddDescription(menu, Dialog.Clean(DialogIds.SaveAndQuitAndRetryDescId));
    }
}
