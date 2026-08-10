using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.Utils;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

public static class ReturnToMapTimer {
    private static int counter = 0;
    private static bool pressed = false;

    public static void Reset() {
        pressed = false;
        counter = 0;
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

    public static void Update() {
        if (!pressed) {
            counter = 0;
            return;
        }

        counter++;
        if (counter > SplitTimings.WIPE_FADEOUT_FRAMES) {
            pressed = false;
            counter = 0;
            RoomTimerManager.UpdateTimerState();
        }
    }
}
