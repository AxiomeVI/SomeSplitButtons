using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     Which collect the list's destinations follow: open from the collect until the room changes or a
///     level loads.
/// </summary>
// A death keeps it open: Level.Reload builds no loader and raises no transition. A save state carries
// it, through ReturnToMapTimer's snapshot, so a state saved before the collect loads it back closed.
internal static class DestinationWindow {
    private static WindowTrigger trigger;

    internal static WindowTrigger Trigger => trigger;

    // Hook: SaveData.RegisterCassette opens it, as Cassette.CollectRoutine calls it at pickup.
    // Hook: Level.OnTransitionTo closes it.
    internal static void Load() {
        On.Celeste.SaveData.RegisterCassette += SaveData_RegisterCassette;
        Everest.Events.Level.OnTransitionTo += Level_OnTransitionTo;
    }

    internal static void Unload() {
        On.Celeste.SaveData.RegisterCassette -= SaveData_RegisterCassette;
        Everest.Events.Level.OnTransitionTo -= Level_OnTransitionTo;
    }

    // ⚠️ Reached from Level_OnLoadingThread through SplitFeatures.ResetAll, on the loader's thread.
    internal static void Close() => trigger = WindowTrigger.None;

    internal static void Restore(WindowTrigger saved) => trigger = saved;

    /// <summary>The destination rows for this session, in list order.</summary>
    internal static List<(string Key, string Label)> Rows(Session session) {
        List<(string, string)> rows = new();
        if (trigger == WindowTrigger.None) return rows;
        AreaKey area = session.Area;
        bool vanilla = area.GetLevelSet() == "Celeste";
        bool hasBSide = AreaData.Get(area)?.HasMode(AreaMode.BSide) == true;
        foreach ((int id, AreaMode mode) in Destinations.For(trigger, area.ID, area.Mode, vanilla, hasBSide)) {
            string label = vanilla ? Destinations.Label(id, mode) : Dialog.Clean("OVERWORLD_REMIX");
            rows.Add((Destinations.Key(id, mode), label));
        }
        return rows;
    }

    private static void SaveData_RegisterCassette(On.Celeste.SaveData.orig_RegisterCassette orig, SaveData self,
        AreaKey area) {
        orig(self, area);
        if (Engine.Scene is Level) trigger = WindowTrigger.Cassette;
    }

    private static void Level_OnTransitionTo(Level level, LevelData next, Vector2 direction) => Close();
}
