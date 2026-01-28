using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
using Celeste.Mod.SomeSplitButtons.SkipCutsceneSplitManager;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Menu;

public static class ModMenuOptions {
    private static SomeSplitButtonsModuleSettings _settings = SomeSplitButtonsModule.Settings;

    public static void CreateMenu(TextMenu menu)
    {
        TextMenu.OnOff _showSkipCutsceneSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSkipCutsceneSplitButtonId), 
            _settings.ShowSkipCutsceneSplitButton).Change(
                b => 
                {
                    _settings.ShowSkipCutsceneSplitButton = b;
                    SkipCutsceneTimer.Reset();
                    if (b && Engine.Scene is Level level) SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex);
                }
        );

        TextMenu.OnOff _saveAndQuitAndRetry = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.SaveAndQuitAndRetryId),
            _settings.SaveAndQuitAndRetry).Change(
                b => _settings.SaveAndQuitAndRetry = b
        );

        TextMenu.OnOff _showSaveAndQuitSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSaveAndQuitSplitButtonId),
            _settings.ShowSaveAndQuitSplitButton).Change(
                b => 
                {
                    _settings.ShowSaveAndQuitSplitButton = b;
                    _saveAndQuitAndRetry.Disabled = !b;
                    SaveAndQuitTimer.Reset();
                }
        );
        
        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled).Change(
            value =>
            {
                _settings.Enabled = value;
                _showSkipCutsceneSplitButton.Visible = value;
                _showSaveAndQuitSplitButton.Visible = value;
                _saveAndQuitAndRetry.Visible = value;
                _saveAndQuitAndRetry.Disabled = !_settings.ShowSaveAndQuitSplitButton;
                SkipCutsceneTimer.Reset();
                if (value && _settings.ShowSaveAndQuitSplitButton && Engine.Scene is Level level) SkipCutsceneTimer.PrologueCheck(level.Session.Area.ChapterIndex);
                SaveAndQuitTimer.Reset();
            }
        ));

        menu.Add(_showSkipCutsceneSplitButton);
        menu.Add(_showSaveAndQuitSplitButton);
        menu.Add(_saveAndQuitAndRetry);

        _showSkipCutsceneSplitButton.Visible = _settings.Enabled;
        _showSaveAndQuitSplitButton.Visible = _settings.Enabled;
        _saveAndQuitAndRetry.Visible = _settings.Enabled;
        _saveAndQuitAndRetry.Disabled = !_settings.ShowSaveAndQuitSplitButton;

        _saveAndQuitAndRetry.AddDescription(menu, Dialog.Clean(DialogIds.SaveAndQuitAndRetryDescId));
    }
}