namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     How many frames each split waits between its button being pressed and the room timer being
///     told, and where each figure comes from.
/// </summary>
// These are also what the pause-menu descriptions in Dialog/English.txt say out loud. Those strings
// take the number as {0} instead of spelling it, because they used to spell it: "after 31 frames"
// was literal text in five entries, and changing a constant here left the button telling the player
// something that was no longer true, with nothing anywhere to catch it.
//
// The derivations below are the whole reason a fork should not nudge these to fix a split that looks
// a frame off. The frame the split fires on is a consequence of vanilla's wipe, not a tuning knob.
//
// One copy is still by hand: README.md quotes all three figures in prose, where a format string
// would be worse for the person reading it. If you change a number here, change it there — that
// reminder lives on this side because this is the side that changes.
internal static class SplitTimings {
    /// <summary>
    ///     A pause-menu exit's screen wipe: from the press to <c>Engine.Scene = new LevelExit(...)</c>.
    /// </summary>
    // One constant for two buttons, because it is one fact rather than two that happen to agree:
    // Return to Map's GiveUp(restartArea: false) runs the same DoScreenWipe as Save and Quit. Split
    // them again only if vanilla gives them different wipes.
    //
    // Duration = 0.5f, so Percent advances RawDeltaTime / Duration = 1/30 per frame and reaches 1 on
    // update 30; one more update sets Completed, one more fires OnComplete. Press on frame N puts
    // the scene change on N+31. All 13 per-area AreaData.Wipe delegates keep the inherited 0.5 s.
    internal const int WIPE_FADEOUT_FRAMES = 31;

    /// <summary>
    ///     A skipped cutscene's fade, from the press to the chapter being marked complete.
    /// </summary>
    // SkipCutsceneRoutine builds a FadeWipe with Duration = 0.25f — 15 frames — plus about 3 frames
    // of coroutine and RendererList scheduling.
    internal const int END_CS_FADEOUT_FRAMES = 18;

    /// <summary>
    ///     The same, for the Prologue, whose ending is not a wipe but a scripted sequence.
    /// </summary>
    internal const int PROLOGUE_END_CS_FADEOUT_FRAMES = 232;

    /// <summary>
    ///     A frame count as whole seconds, for the descriptions that quote both.
    /// </summary>
    // Celeste's update rate is fixed at 60, so this is exact rather than an approximation of the
    // current frame rate. Rounded, because the description says "~4s" and not "3.87s".
    internal static int ToSeconds(int frames) => (int) System.Math.Round(frames / 60f);
}
