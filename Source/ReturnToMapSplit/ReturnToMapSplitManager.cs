using Celeste.Mod.SomeSplitButtons.Utils;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplitManager;

public static class ReturnToMapTimer {
    private const int RTM_FADEOUT_FRAMES = 31;

    private static int counter = 0;
    private static bool pressed = false;

    public static void OnSaveState() => Reset();
    public static void OnLoadState() => Reset();
    public static void OnClearState() => Reset();

    public static void Reset() {
        pressed = false;
        counter = 0;
    }

    public static void HandleButtonPressed() {
        if (Engine.Scene is not Level) return;
        if (BerryCheck.BlocksSplit()) return;

        pressed = true;
        counter = 0;
    }

    public static void Update() {
        if (!pressed) {
            counter = 0;
            return;
        }

        counter++;
        if (counter > RTM_FADEOUT_FRAMES) {
            pressed = false;
            counter = 0;
            RoomTimerManager.UpdateTimerState();
        }
    }
}
