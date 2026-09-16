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

    /// <summary>
    ///     Arms the split. False when it was refused.
    /// </summary>
    public static bool HandleButtonPressed() {
        if (Engine.Scene is not Level) return false;
        if (CollectCheck.BlockedMessage() is string blocked) {
            SomeSplitButtonsModule.PopupMessage(blocked);
            return false;
        }

        pressed = true;
        counter = 0;
        return true;
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
