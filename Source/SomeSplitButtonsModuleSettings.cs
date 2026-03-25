using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SomeSplitButtons;
[SettingName(DialogIds.SomeSplitButtonsId)]

public class SomeSplitButtonsModuleSettings : EverestModuleSettings {


    public bool Enabled { get; set; } = true;
    public bool ShowSkipCutsceneSplitButton { get; set; } = false;
    public bool ShowSaveAndQuitSplitButton { get; set; } = false;
    public bool SaveAndQuitAndRetry { get; set; } = false;
    
    #region Hotkeys

    [SettingIgnore]
    public ButtonBinding ButtonToggleSkipCutscene { get; set; } = new(0, Keys.None);

    [SettingIgnore]
    public ButtonBinding ButtonToggleSaveQuit { get; set; } = new(0, Keys.None);

    #endregion
}