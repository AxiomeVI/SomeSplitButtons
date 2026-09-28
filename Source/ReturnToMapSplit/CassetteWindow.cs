using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     Whether the checkpoint picker offers the B-side: from a cassette pickup until the room changes.
/// </summary>
// The route this serves takes the cassette and returns to map from the same room, so a later RTM does
// not get the row. A death keeps it: Level.Reload builds no loader and raises no transition.
internal static class CassetteWindow {
    /// <summary>The row key that means "the B-side". A room name cannot contain a NUL.</summary>
    internal const string BSideKey = "\0bside";

    private static bool armed;

    internal static bool Armed => armed;

    // Hook: SaveData.RegisterCassette arms the window, as Cassette.CollectRoutine calls it at pickup.
    // Hook: Level.OnTransitionTo disarms it.
    internal static void Load() {
        On.Celeste.SaveData.RegisterCassette += SaveData_RegisterCassette;
        Everest.Events.Level.OnTransitionTo += Level_OnTransitionTo;
    }

    internal static void Unload() {
        On.Celeste.SaveData.RegisterCassette -= SaveData_RegisterCassette;
        Everest.Events.Level.OnTransitionTo -= Level_OnTransitionTo;
    }

    // ⚠️ Reached from Level_OnLoadingThread through SplitFeatures.ResetAll, on the loader's thread.
    internal static void Disarm() => armed = false;

    internal static void Restore(bool wasArmed) => armed = wasArmed;

    /// <summary>The picker row for this level's B-side, or null when it is not on offer.</summary>
    internal static (string Key, string Label)? Row(Session session) {
        if (!armed || session.Area.Mode != AreaMode.Normal) return null;
        if (AreaData.Get(session.Area)?.HasMode(AreaMode.BSide) != true) return null;
        return (BSideKey, Dialog.Clean("OVERWORLD_REMIX"));
    }

    private static void SaveData_RegisterCassette(On.Celeste.SaveData.orig_RegisterCassette orig, SaveData self,
        AreaKey area) {
        orig(self, area);
        if (Engine.Scene is Level) armed = true;
    }

    private static void Level_OnTransitionTo(Level level, LevelData next, Vector2 direction)
        => armed = false;
}
