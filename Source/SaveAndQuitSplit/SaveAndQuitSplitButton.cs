using static Celeste.TextMenu;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
public class SaveAndQuitSplitButton : Button {
    public SaveAndQuitSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }

    public static void PressedHandler(Level level) {
        if (level == null) return;
        bool armed = SaveAndQuitTimer.HandleButtonPressed();
        level.Unpause();
        // Never fade out unarmed: nothing would ever end the wipe. See HandleButtonPressed.
        if (armed && SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter) BeginFadeOut(level);
    }

    /// <summary>
    ///     The three things vanilla's menu_pause_savequit handler settles before its wipe, minus the
    ///     exit itself. It also raises the death counters and fires LevelEndingHook; this does not,
    ///     since the chapter is not ending.
    /// </summary>
    private static void BeginFadeOut(Level level) {
        // One-shot reset of a rate the mod never owned, not the sustained modification
        // TimeRateModifier arbitrates. Nothing on the LevelLoader path puts TimeRate back — only
        // Level.Reload does — so a press during a slowdown would re-enter the room still slowed.
#pragma warning disable CS0618
        Engine.TimeRate = 1f;
#pragma warning restore CS0618
        // ⚠️ Load-bearing: Audio.SetMusic returns early when the requested track is already playing,
        // so without this the re-entry's Session.Audio.Apply leaves the music running instead of
        // restarting it from zero.
        Audio.SetMusic(null);
        Audio.BusStopAll(Buses.GAMEPLAY, immediate: true);
        // No OnComplete: SaveAndQuitTimer owns the frame the scene changes on. See Reenter.
        level.DoScreenWipe(wipeIn: false);
    }
}
