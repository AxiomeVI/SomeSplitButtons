using Celeste.Mod.SomeSplitButtons.Utils;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
public static class SaveAndQuitTimer {
    private static int counter = 0;
    private static bool pressed = false;
    private const int SQ_FADEOUT_FRAMES = 31;
    private static bool keepTimerStopped = false;

    public static void OnBeforeSaveState(Level level) {
        level.TimerStopped = false;
    }

    /// <summary>
    ///     Disarms the timer, releasing the chapter clock first if this manager is what is holding
    ///     it stopped.
    /// </summary>
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
        if (BerryCheck.BlocksSplit()) return;

        pressed = true;
        counter = 0;
    }

    public static void Update(Level level) {
        if (keepTimerStopped)
        {
            level.TimerStopped = true;
            if (ClockWouldRestart(level))
            {
                keepTimerStopped = false;
                level.TimerStopped = false;
            }
        }
        if (pressed) {
            counter++;
            if (counter > SQ_FADEOUT_FRAMES) {
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