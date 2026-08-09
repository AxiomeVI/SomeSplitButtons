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

    public static void OnSaveState() {
        Reset();
    }

    public static void OnLoadState() {
        Reset();
    }

    public static void OnClearState() {
        Reset();
    }

    public static void Reset()
    {
        pressed = false;
        keepTimerStopped = false;
        counter = 0;
    }

    public static void HandleButtonPressed() {
        if (Engine.Scene is not Level level) return;
        if (BerryCheck.BlocksSplit(level)) return;

        pressed = true;
        counter = 0;
    }

    public static void Update(Level level) {
        if (keepTimerStopped)
        {
            level.TimerStopped = true;
            bool InControl = level.Tracker.GetEntity<Player>()?.InControl ?? false;
            if (InControl)
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