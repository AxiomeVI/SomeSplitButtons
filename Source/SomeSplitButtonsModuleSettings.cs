using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SomeSplitButtons;
[SettingName(DialogIds.SomeSplitButtonsId)]

public class SomeSplitButtonsModuleSettings : EverestModuleSettings {
    public static SomeSplitButtonsModuleSettings Instance { get; private set; }
    public SomeSplitButtonsModuleSettings(){
        Instance = this;
    }

    [SettingName(DialogIds.EnableSaveAndQuitSplitButtonId)]
    public bool ShowSaveAndQuitSplitButton { get; set; } = true;

    [SettingName(DialogIds.EnableSkipCutsceneSplitButtonId)]
    public bool ShowSkipCutsceneSplitButton { get; set; } = true;

    #region Hotkeys

    [SettingName(DialogIds.ToggleSaveQuitKeyId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonToggleSaveQuit { get; set; } = new(0, Keys.None);

    [SettingName(DialogIds.ToggleSkipCutsceneKeyId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonToggleSkipCutscene { get; set; }  = new(0, Keys.None);

    #endregion
}