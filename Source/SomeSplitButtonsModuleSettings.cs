namespace Celeste.Mod.SomeSplitButtons;

[SettingName(DialogIds.SomeSplitButtonsId)]
public class SomeSplitButtonsModuleSettings : EverestModuleSettings {
    public bool Enabled { get; set; } = true;
    public bool ShowSkipCutsceneSplitButton { get; set; } = false;
    public bool ShowSaveAndQuitSplitButton { get; set; } = false;
    public bool SaveAndQuitAndReenter { get; set; } = true;
    public bool ShowReturnToMapSplitButton { get; set; } = false;

    // Default off: it reloads the level, which is a much larger effect than the split alone, and a
    // player who enabled the Return to Map button did not ask for that.
    public bool ReturnToMapCheckpointMenu { get; set; } = false;

    // Default on: a berry lost to a split is gone for the run, and turning the refusal off is one
    // menu away for a player who wants the split regardless.
    public bool BerryCollectProtection { get; set; } = true;

    #region Hotkeys

    // Unbound is `new()`, never `new(0, Keys.None)`. The latter reads as "no key" but actually
    // seeds the list with Keys.None, and Keys.None is not a key: FNA returns it from ToXNAKey for
    // anything missing from its SDL→XNA table, then reports it held like a real key. A binding
    // holding it therefore fires on every unmappable key the layout has.

    [SettingIgnore]
    public ButtonBinding ButtonToggleSkipCutscene { get; set; } = new();

    [SettingIgnore]
    public ButtonBinding ButtonToggleSaveQuit { get; set; } = new();

    [SettingIgnore]
    public ButtonBinding ButtonToggleReturnToMap { get; set; } = new();

    #endregion
}