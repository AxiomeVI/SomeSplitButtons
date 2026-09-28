using System.Collections.Generic;
using Celeste.Mod.SomeSplitButtons.Integration;
using Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;
using Celeste.Mod.SomeSplitButtons.Splits;
using Monocle;


namespace Celeste.Mod.SomeSplitButtons.SkipCutsceneSplit;

internal static class SkipCutsceneTimer {
    // Vanilla's area IDs, hard-coded the way Level.UpdateTime hard-codes the Epilogue's.
    private const int PROLOGUE_AREA_ID = 0;
    internal const int EPILOGUE_AREA_ID = 8;

    private static readonly SplitCountdown countdown = new(() => FadeoutFrames);
    private static bool inPrologue = false;
    private static bool hidden = false; // Hide the button after the first press
    internal static bool Hidden => hidden;
    internal static bool Armed => countdown.Armed;

    private sealed record Saved((bool Armed, int Counter) Countdown, bool Hidden, object RoomTimer);

    internal static object Snapshot() => new Saved(countdown.State, hidden, SkipCutsceneRoomTimer.Snapshot());

    internal static void Restore(object snapshot) {
        Saved saved = (Saved) snapshot;
        countdown.State = saved.Countdown;
        hidden = saved.Hidden;
        SkipCutsceneRoomTimer.Restore(saved.RoomTimer);
    }

    // Arm, not TryArm: this split stays in the level, so there is no collectible for it to lose and
    // nothing for a collect check to refuse.
    internal static void HandleButtonPressed() {
        hidden = true;
        countdown.Arm();
    }

    /// <summary>What the pause-menu button does: leave the pause and arm the split.</summary>
    // This split never refuses — there is no collectible to lose on a cutscene skip — so it has no
    // return value and its interop sequence ends on the press.
    internal static void Press(Level level) {
        HandleButtonPressed();
        level.Unpause();
    }

    internal static void Reset() {
        Reentry.Reset(Reentry.SkipCutscene);
        hidden = false;
        countdown.Reset();
        SkipCutsceneRoomTimer.Reset();
    }

    // By area, not by `ChapterIndex == -1`: every interlude has that index, the Epilogue and modded
    // maps marked Interlude included, and the 232 frames were measured on the Prologue alone.
    internal static void PrologueCheck(AreaKey area) {
        inPrologue = area.ID == PROLOGUE_AREA_ID;
    }

    /// <summary>How long this split will wait, for the chapter it was last refreshed for.</summary>
    // Exposed so the pause-menu description quotes the wait rather than deciding it a second time
    // from a different source — the live session instead of the flag set on level load.
    internal static int FadeoutFrames =>
        inPrologue ? SplitTimings.PROLOGUE_END_CS_FADEOUT_FRAMES : SplitTimings.END_CS_FADEOUT_FRAMES;

    internal static bool InPrologue => inPrologue;

    internal static void Update(Level level) {
        if (!countdown.Tick()) return;

        // Chapters 1 to 7 split on the next frame, when SpeedrunTool sees the completion.
        Logger.Info(nameof(SomeSplitButtonsModule), $"SkipCutscene split in {level.Session.Level} on frame {Engine.FrameCounter}");
        SkipCutsceneRoomTimer.Release();
        // Chapters 1 to 7 are already complete here — their ending registers it in OnBegin — and
        // SpeedrunTool splits on the next frame by itself. The Prologue registers only at the very
        // end, so SpeedrunTool alone is told now. Writing level.Completed instead makes the real
        // RegisterAreaComplete return early: no completion in the save, no Level.Complete event.
        if (!level.Completed) SkipCutsceneRoomTimer.SplitAsCompleted();

        if (!SomeSplitButtonsModule.Settings.SkipCutsceneLoadMenu) return;
        AreaKey area = level.Session.Area;
        List<(string Key, string Label)> rows = new();
        foreach ((int id, AreaMode mode) in Destinations.For(WindowTrigger.Ending, area.ID, area.Mode,
                     area.GetLevelSet() == "Celeste", hasBSide: false)) {
            rows.Add((Destinations.Key(id, mode), Destinations.Label(id, mode)));
        }
        if (rows.Count > 0) Reentry.Begin(level, rows, Reentry.SkipCutscene);
    }
}
