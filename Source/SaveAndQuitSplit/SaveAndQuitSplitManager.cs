using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.Utils;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplit;
public static class SaveAndQuitTimer {
    private static int counter = 0;
    private static bool pressed = false;
    private static bool keepTimerStopped = false;

    public static void OnBeforeSaveState(Level level) {
        level.TimerStopped = false;
    }

    /// <summary>
    ///     Disarms the timer, releasing the chapter clock first if this manager is what is holding
    ///     it stopped.
    /// </summary>
    // Kept as well as UpdateHold, not instead of it: this releases at the moment of the reset, where
    // UpdateHold would wait for the next frame — and on a level exit there is no next frame.
    //
    // The `Engine.Scene is Level` test abandons the flag when the scene is not a level, which is
    // safe rather than lucky: the only way to leave a Level is to replace it, and the next one
    // starts with TimerStopped false. There is no live object left to release.
    public static void Reset()
    {
        if (keepTimerStopped && Engine.Scene is Level level) {
            level.TimerStopped = false;
        }
        pressed = false;
        keepTimerStopped = false;
        counter = 0;
    }

    /// <summary>
    ///     Vanilla's own test for "the chapter clock may run again", copied from the
    ///     <c>TimerStarted</c> latch in <c>Level.UpdateTime</c>.
    /// </summary>
    private static bool ClockWouldRestart(Level level) {
        if (level.InCutscene) return false;
        Player player = level.Tracker.GetEntity<Player>();
        return player != null && !player.TimePaused;
    }

    public static void HandleButtonPressed() {
        if (Engine.Scene is not Level) return;
        if (BerryCheck.BlockedMessage() is string blocked) {
            SomeSplitButtonsModule.PopupMessage(blocked);
            return;
        }

        pressed = true;
        counter = 0;
    }

    /// <summary>
    ///     Maintains the chapter-clock hold, and releases it once vanilla would have restarted the
    ///     clock on its own.
    /// </summary>
    // ⚠️ Split out of Update, and called from outside the settings gates, because TimerStopped is
    // vanilla's flag and not the mod's. While this manager holds it, the mod is the only thing that
    // will ever put it back — so if Update stopped being called mid-hold, the chapter clock stayed
    // frozen for the rest of the Level, and with it Session.Time and SaveData.AddTime.
    //
    // That used to be prevented by every path that clears either setting also calling Reset(). True
    // in all five, but true by convention repeated five times rather than by construction, and the
    // sixth path would have been silent. Declaring the hold in the SplitFeatures table is what makes
    // it structural: the loop runs this whether or not anything is enabled.
    public static void UpdateHold(Level level) {
        if (!keepTimerStopped) return;

        level.TimerStopped = true;
        if (ClockWouldRestart(level)) {
            keepTimerStopped = false;
            level.TimerStopped = false;
        }
    }

    public static void Update(Level level) {
        if (pressed) {
            counter++;
            if (counter > SplitTimings.WIPE_FADEOUT_FRAMES) {
                pressed = false;
                counter = 0;
                RoomTimerManager.UpdateTimerState();
                level.TimerStopped = true;
                keepTimerStopped = true;
            }
        }
        else {
            counter = 0;
        }
    }
}