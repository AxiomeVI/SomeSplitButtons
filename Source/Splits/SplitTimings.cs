namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     How many frames each split waits between its button being pressed and the room timer being
///     told, and where each figure comes from.
/// </summary>
// Derived from vanilla's wipes, not tuned: a split that looks a frame off is a wrong derivation and
// not a number to nudge. Nothing outside this file spells them out — the pause-menu descriptions
// read them through a {0}, and the README describes the waits without quoting them.
internal static class SplitTimings {
    /// <summary>A pause-menu exit's screen wipe, press to <c>Engine.Scene = new LevelExit(...)</c>.</summary>
    // Duration = 0.5f, so Percent advances 1/30 per frame and reaches 1 on update 30; one more sets
    // Completed, one more fires OnComplete. All 13 per-area AreaData.Wipe delegates keep that 0.5 s.
    // One constant for two buttons: Return to Map's GiveUp(restartArea: false) runs the same wipe.
    internal const int WIPE_FADEOUT_FRAMES = 31;

    /// <summary>A skipped cutscene's fade, press to the chapter being marked complete.</summary>
    // SkipCutsceneRoutine's FadeWipe is Duration = 0.25f — 15 frames — plus about 3 of coroutine and
    // RendererList scheduling.
    internal const int END_CS_FADEOUT_FRAMES = 18;

    /// <summary>The same for the Prologue, whose ending is a scripted sequence and not a wipe.</summary>
    internal const int PROLOGUE_END_CS_FADEOUT_FRAMES = 232;

    /// <summary>A frame count as whole seconds, for the descriptions that quote both.</summary>
    // Exact, not an approximation: Celeste's update rate is fixed at 60.
    internal static int ToSeconds(int frames) => (int) System.Math.Round(frames / 60f);
}
