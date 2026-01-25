using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SomeSplitButtons;
[SettingName(DialogIds.SomeSplitButtonsId)]

public class SomeSplitButtonsModuleSettings : EverestModuleSettings {

    [SettingName(DialogIds.EnabledId)]
    public bool Enabled { get; set; } = true;

    [SettingName(DialogIds.EnableSkipCutsceneSplitButtonId)]
    public bool ShowSkipCutsceneSplitButton { get; set; } = false;

    [SettingName(DialogIds.EnableSaveAndQuitSplitButtonId)]
    public bool ShowSaveAndQuitSplitButton { get; set; } = false;

    [SettingName(DialogIds.SaveAndQuitAndRetryId), SettingSubText(DialogIds.SaveAndQuitAndRetryDescId)]
    public bool SaveAndQuitAndRetry { get; set; } = false;
    
    #region Hotkeys

    [SettingName(DialogIds.ToggleSaveQuitKeyId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonToggleSaveQuit { get; set; } = new(0, Keys.None);

    [SettingName(DialogIds.ToggleSkipCutsceneKeyId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonToggleSkipCutscene { get; set; }  = new(0, Keys.None);

    #endregion
}