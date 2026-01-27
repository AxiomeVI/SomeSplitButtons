using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SomeSplitButtons;
[SettingName(DialogIds.SomeSplitButtonsId)]

public class SomeSplitButtonsModuleSettings : EverestModuleSettings {


    public bool Enabled { get; set; } = true;
    public bool ShowSkipCutsceneSplitButton { get; set; } = false;
    public bool ShowSaveAndQuitSplitButton { get; set; } = false;
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