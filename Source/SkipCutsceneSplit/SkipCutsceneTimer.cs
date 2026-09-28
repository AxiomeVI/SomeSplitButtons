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

    /// <summary>The destinations the list offers after this area's ending; empty where it does not open.</summary>
    internal static List<(string Key, string Label)> ListRows(AreaKey area) {
        List<(string Key, string Label)> rows = new();
        foreach ((int id, AreaMode mode) in Destinations.For(WindowTrigger.Ending, area.ID, area.Mode,
                     area.GetLevelSet() == "Celeste", hasBSide: false)) {
            rows.Add((Destinations.Key(id, mode), Destinations.Label(id, mode)));
        }
        return rows;
    }

    internal static void Update(Level level) {
        if (!countdown.Tick()) return;

        Logger.Info(nameof(SomeSplitButtonsModule), $"SkipCutscene split in {level.Session.Level} on frame {Engine.FrameCounter}");

        // Alive, not the list's setting alone: the hotkey turns the button off without disarming the
        // split, which still fires, and a list opened then would be torn down again the next frame.
        List<(string Key, string Label)> rows = Reentry.SkipCutscene.Alive() ? ListRows(level.Session.Area) : new();
        // Only where the list can run: the wait is unpaused, so by the split frame the player may have
        // re-opened the pause menu, started vanilla's skip, or the ending's own wipe may be running —
        // SkippingCutscene's branch in Level.Update runs before the paused one, and Wipe.Update runs
        // while paused. Otherwise the split stands on its own, as without the setting.
        if (rows.Count > 0 && !level.Paused && !level.SkippingCutscene && level.Wipe == null) {
            // The run may go on: an ordinary room, the chapter end kept from SpeedrunTool until the list
            // closes without a pick.
            SkipCutsceneRoomTimer.SplitMarkAsRoom();
            Reentry.Begin(level, rows, Reentry.SkipCutscene);
            return;
        }

        // Chapters 1 to 7 split on the next frame, when SpeedrunTool sees the completion.
        SkipCutsceneRoomTimer.Release();
        // Chapters 1 to 7 are already complete here — their ending registers it in OnBegin — and
        // SpeedrunTool splits on the next frame by itself. The Prologue registers only at the very
        // end, so SpeedrunTool alone is told now. Writing level.Completed instead makes the real
        // RegisterAreaComplete return early: no completion in the save, no Level.Complete event.
        if (!level.Completed) SkipCutsceneRoomTimer.SplitAsCompleted();
    }
}
