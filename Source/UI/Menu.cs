namespace Celeste.Mod.SomeSplitButtons.Menu;

public static class ModMenuOptions {

    private static TextMenu.OnOff _showSkipCutsceneSplitButton;
    private static TextMenu.OnOff _showSaveAndQuitSplitButton;
    private static TextMenu.OnOff _saveAndQuitAndRetry;

    private static SomeSplitButtonsModuleSettings _settings = SomeSplitButtonsModule.Settings;

    public static void CreateMenu(TextMenu menu)
    {
        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled).Change(
            value =>
            {
                _settings.Enabled = value;
                _showSkipCutsceneSplitButton.Visible = value;
                _showSaveAndQuitSplitButton.Visible = value;
                _saveAndQuitAndRetry.Visible = value && _settings.ShowSaveAndQuitSplitButton;
            }
        ));

        _showSkipCutsceneSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSkipCutsceneSplitButtonId), 
            _settings.ShowSkipCutsceneSplitButton).Change(
                b => _settings.ShowSkipCutsceneSplitButton = b
        );
        _showSaveAndQuitSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSaveAndQuitSplitButtonId),
            _settings.ShowSaveAndQuitSplitButton).Change(
                b => 
                {
                    _settings.ShowSaveAndQuitSplitButton = b;
                    _saveAndQuitAndRetry.Visible = b && _settings.Enabled;
                }
        );
        _saveAndQuitAndRetry = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.SaveAndQuitAndRetryId),
            _settings.SaveAndQuitAndRetry).Change(
                b => _settings.SaveAndQuitAndRetry = b
        );

        menu.Add(_showSkipCutsceneSplitButton);
        menu.Add(_showSaveAndQuitSplitButton);
        menu.Add(_saveAndQuitAndRetry);
        _showSkipCutsceneSplitButton.Visible = _settings.Enabled;
        _showSaveAndQuitSplitButton.Visible = _settings.Enabled;
        _saveAndQuitAndRetry.Visible = _settings.Enabled;
        _saveAndQuitAndRetry.AddDescription(menu, Dialog.Clean(DialogIds.SaveAndQuitAndRetryDescId));
    }
}