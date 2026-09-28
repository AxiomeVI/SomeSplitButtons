using Celeste.Mod.SomeSplitButtons.Integration;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     Keeps SpeedrunTool from reading the checkpoint load as a room change.
/// </summary>
// SpeedrunTool splits whenever the room it last timed differs from the level's, and the load changes
// rooms after the split at the button already ended the room. Forgetting that room during the load
// raises no split at all. Swallowing the split in this mod's detour of UpdateTimerState is not
// enough: a mod whose detour of that method runs outside this one's still sees the call, and records
// a room one frame long.
//
// ⚠️ Nothing in SplitFeatures.ResetAll may reach this. Level_OnLoadingThread calls ResetAll during
// the load this exists to cover.
internal static class CheckpointArrival {
    private static bool expected;

    internal static bool Expected => expected;

    /// <summary>Marks the next level load as the checkpoint the picker chose.</summary>
    internal static void Expect() => expected = true;

    /// <summary>Forgets an expected load that will not come, as when the player quits on the way.</summary>
    internal static void Cancel() => expected = false;

    /// <summary>Forgets SpeedrunTool's last room if the load is the expected one. Runs on the loader's thread.</summary>
    // During the load and not at Expect: the outgoing level still updates once after the picker's
    // callback, and SpeedrunTool would record its room again.
    internal static void OnLoadingThread() {
        if (!expected) return;
        expected = false;
        SpeedrunToolHooks.ForgetPreviousRoom();
    }
}
