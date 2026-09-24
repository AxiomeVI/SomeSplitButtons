namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     Suppresses the room-change split SpeedrunTool fires on the first frame of a checkpoint load.
/// </summary>
// ⚠️ Nothing in SplitFeatures.ResetAll may reach this. Everest.Events.LevelLoader.OnLoadingThread
// fires during the load and before the new Level's first Update, and its handler calls ResetAll —
// so a flag reachable from any feature's Reset is wiped during the load it exists to cover. That is
// why this is its own class and not a field on ReturnToMapTimer or SkipCutsceneRoomTimer.
//
// ⚠️ The stamp is only comparable to another read taken inside an update. Under a TAS,
// Engine.FrameCounter inside an update is CelesteTAS's own counter, restored afterwards; between
// updates it is the real one. Arm from inside the picker's callback and clear from inside
// Level_OnUpdate, never from a console command or a DebugRC probe.
internal static class ArrivalSplitSwallow {
    private static bool armed;
    private static ulong armedAtFrame;

    internal static bool Armed => armed;

    internal static void Arm(ulong frame) {
        armed = true;
        armedAtFrame = frame;
    }

    /// <summary>Swallows one split, and returns whether it did.</summary>
    internal static bool Consume() {
        if (!armed) return false;
        armed = false;
        return true;
    }

    /// <summary>
    ///     Disarms on the first update after the one that armed it, whether or not it was used.
    /// </summary>
    // On use, not on the frame, is the tempting version and it is wrong: picking the checkpoint for
    // the room you already stand in leaves previousRoom == Session.Level, so no room-change split
    // fires, nothing consumes the flag, and it eats the next genuine transition instead.
    internal static void ClearIfFrameDiffers(ulong frame) {
        if (armed && frame != armedAtFrame) armed = false;
    }

    internal static void Reset() {
        armed = false;
        armedAtFrame = 0;
    }
}
