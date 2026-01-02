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
}