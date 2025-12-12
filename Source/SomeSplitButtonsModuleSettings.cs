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

    [SettingName(DialogIds.CutscenesRequiredId), SettingRange(min: 0, max: 10)]
    public int CutscenesRequired { get; set; } = 0;
}