using static Celeste.TextMenu;
using Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
public class SaveAndQuitSplitButton : Button {
    public SaveAndQuitSplitButton(string label) : base(label) {
        ConfirmSfx = SFX.ui_main_message_confirm;
    }

    /// <summary>
    ///     Closes the pause menu and arms the split. False when it was refused.
    /// </summary>
    public static bool PressedHandler(Level level) {
        if (level == null) return false;
        bool armed = SaveAndQuitTimer.HandleButtonPressed();
        level.Unpause();
        // Never fade out unarmed: nothing would ever end the wipe. See HandleButtonPressed.
        if (armed && SomeSplitButtonsModule.Settings.SaveAndQuitAndReenter) BeginFadeOut(level);
        return armed;
    }

    /// <summary>
    ///     The three things vanilla's menu_pause_savequit handler settles before its wipe, minus the
    ///     exit itself. It also raises the death counters and fires LevelEndingHook; this does not,
    ///     since the chapter is not ending.
    /// </summary>
    private static void BeginFadeOut(Level level) {
        // One-shot reset of a rate the mod never owned, not the sustained modification
        // TimeRateModifier arbitrates. It mirrors vanilla's own savequit handler, which resets the
        // rate at the press for the same reason: the fade-out that follows should run at normal
        // speed rather than at a seeker's.
        //
        // It is not what keeps the re-entered room at normal speed, against what this comment used
        // to claim. Monocle.Engine.OnSceneTransition assigns TimeRate = 1f on every scene swap, so
        // even a slowdown still writing the field when its scene dies — the heart's collect routine
        // is the one that does — cannot follow the player into the new Level. Measured 2026-08-29 by
        // test/Heart/rate_is_normal_after_the_reenter.
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
