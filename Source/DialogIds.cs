namespace Celeste.Mod.SomeSplitButtons;
internal static class DialogIds {
    // Menu
    internal const string SomeSplitButtonsId = "SSB_SOME_SPLIT_BUTTONS";
    internal const string EnabledId = "SSB_ENABLED";
    internal const string EnableSaveAndQuitSplitButtonId = "SSB_ENABLE_SAVE_AND_QUIT_SPLIT_BUTTON";
    internal const string EnableSaveAndQuitSplitButtonDescId = "SSB_ENABLE_SAVE_AND_QUIT_SPLIT_BUTTON_DESC";
    internal const string SaveAndQuitSplitButtonId = "SSB_SAVE_AND_QUIT_SPLIT_BUTTON";
    internal const string EnableSkipCutsceneSplitButtonId = "SSB_ENABLE_SKIP_CUTSCENE_SPLIT_BUTTON";
    internal const string EnableSkipCutsceneSplitButtonDescId = "SSB_ENABLE_SKIP_CUTSCENE_SPLIT_BUTTON_DESC";
    internal const string SkipCutsceneSplitButtonId = "SSB_SKIP_CUTSCENE_SPLIT_BUTTON";
    internal const string ToggleSaveQuitKeyId = "SSB_TOGGLE_SAVE_QUIT_KEY";
    internal const string ToggleSkipCutsceneKeyId = "SSB_TOGGLE_SKIP_CUTSCENE_KEY";
    internal const string SQButtonDesc = "SSB_SQ_BUTTON_DESC";
    internal const string SQButtonReenterDesc = "SSB_SQ_REENTER_BUTTON_DESC";
    internal const string SCSButtonDesc = "SSB_SCS_BUTTON_DESC";
    internal const string SCSPrologueButtonDesc = "SSB_SCS_PROLOGUE_BUTTON_DESC";
    internal const string SaveAndQuitAndReenterId = "SSB_SQ_REENTER_OPTIONS";
    internal const string SaveAndQuitAndReenterDescId = "SSB_SQ_REENTER_OPTIONS_DESC";
    internal const string EnableReturnToMapSplitButtonId = "SSB_ENABLE_RETURN_TO_MAP_SPLIT_BUTTON";
    internal const string EnableReturnToMapSplitButtonDescId = "SSB_ENABLE_RETURN_TO_MAP_SPLIT_BUTTON_DESC";
    internal const string ReturnToMapSplitButtonId = "SSB_RETURN_TO_MAP_SPLIT_BUTTON";
    internal const string ToggleReturnToMapKeyId = "SSB_TOGGLE_RETURN_TO_MAP_KEY";
    internal const string RTMButtonDesc = "SSB_RTM_BUTTON_DESC";
    internal const string ReturnToMapSplitMenuHeaderId = "SSB_RTM_SPLIT_MENU_HEADER";
    internal const string ReturnToMapCheckpointMenuId = "SSB_RTM_CHECKPOINT_MENU";
    internal const string ReturnToMapCheckpointMenuDescId = "SSB_RTM_CHECKPOINT_MENU_DESC";
    internal const string CheckpointMenuHeaderId = "SSB_RTM_CHECKPOINT_MENU_HEADER";
    internal const string BerryCollectProtectionId = "SSB_BERRY_COLLECT_PROTECTION";
    internal const string BerryCollectProtectionDescId = "SSB_BERRY_COLLECT_PROTECTION_DESC";
    internal const string BerryBlocksSplitId = "SSB_BERRY_BLOCKS_SPLIT";
    internal const string BerrySlowdownId = "SSB_BERRY_SLOWDOWN";
    internal const string HeartBlocksSplitId = "SSB_HEART_BLOCKS_SPLIT";
    internal const string ButtonEnabledId = "SSB_BUTTON_ENABLED";
    internal const string ButtonDisabledId = "SSB_BUTTON_DISABLED";

    // Vanilla Celeste ids, reused so the confirmation prompt reads like the real Return to Map one
    internal const string VanillaReturnContinueId = "MENU_RETURN_CONTINUE";
    internal const string VanillaReturnCancelId = "MENU_RETURN_CANCEL";

    // The vanilla pause-menu buttons each split button is gated on. Their presence says this menu is
    // one where the split belongs; it no longer says anything about where the split button goes.
    internal const string VanillaPauseSkipCutsceneId = "menu_pause_skip_cutscene";
    internal const string VanillaPauseSaveQuitId = "menu_pause_savequit";
    internal const string VanillaPauseReturnId = "menu_pause_return";
    internal const string VanillaPauseOptionsId = "menu_pause_options";

    // Keybind config UI
    internal const string KeybindConfigId = "SSB_KEYBIND_CONFIG";
    internal const string KeybindComboSubId = "SSB_KEYBIND_COMBO_SUB";
    internal const string KeybindClearSubId = "SSB_KEYBIND_CLEAR_SUB";
    internal const string KeybindTimeoutId = "SSB_KEYBIND_TIMEOUT";
}
