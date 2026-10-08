using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Splits;

/// <summary>
///     Keeps the level paused from an accepted press to its split, as vanilla keeps it paused through
///     the wipe of the exit the split stands for.
/// </summary>
// So the wait runs no gameplay: nothing in it can die, move or collect, which vanilla's wipe does not
// allow either. The wait still costs what it did: Level.UpdateTime and SpeedrunTool's Timing do not
// read Paused, and the countdown ticks in Level.Update's body, which runs while paused.
internal static class PausedWait {
    /// <summary>Pauses the level for the wait. The caller takes down the menu it was pressed in.</summary>
    internal static void Begin(Level level) {
        level.PauseMainMenuOpen = false;
        level.Paused = true;
    }

    /// <summary>Hands the level back, at a split that leaves the player in it.</summary>
    // No unpauseTimer, unlike a menu's exit: it guards the press that closed the menu from bleeding
    // into a jump or a dash, and that press was 31 frames ago. Its nine frames would be charged to
    // the next room.
    internal static void End(Level level) {
        if (!level.Paused) return;
        level.Paused = false;
        Audio.Play(SFX.ui_game_unpause);
    }

    /// <summary>What vanilla's exits settle before leaving the level: game speed, music and gameplay sounds.</summary>
    // A one-shot TimeRate reset, not a sustained modification: LoadLevel does not put it back, so a
    // split during a seeker or Oshiro slowdown would start the next level slowed. SetMusic(null)
    // because SetMusic returns early on the track already playing, so the next level's
    // Session.Audio.Apply would leave the music running instead of restarting it.
    internal static void ResetForExit() {
#pragma warning disable CS0618
        Engine.TimeRate = 1f;
#pragma warning restore CS0618
        Audio.SetMusic(null);
        Audio.BusStopAll(Buses.GAMEPLAY, immediate: true);
    }
}
