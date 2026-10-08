using Celeste.Mod.SomeSplitButtons.Integration;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>
///     Follows a load from the list to its level, and releases what the load holds if the player leaves
///     on the way.
/// </summary>
// The way in is LevelEnter, then a vignette for a start of 7a or 8a, then LevelLoader, then the Level
// built from the target session. Anything else — a quit from CoreVignette to the overworld — leaves the
// load. The release runs in OnSceneTransition, before the next scene's Begin, which is where
// SpeedrunTool checks whether to clear its states.
internal static class LoadChain {
    private static Session target;

    // Hook: Celeste.OnSceneTransition releases the load when the scene leaves it.
    // Hook: Level.OnBeforeUpdate ends it on the target level's first update, after both of
    // SpeedrunTool's Begin checks.
    internal static void Load() {
        On.Celeste.Celeste.OnSceneTransition += Celeste_OnSceneTransition;
        Everest.Events.Level.OnBeforeUpdate += Level_OnBeforeUpdate;
    }

    internal static void Unload() {
        On.Celeste.Celeste.OnSceneTransition -= Celeste_OnSceneTransition;
        Everest.Events.Level.OnBeforeUpdate -= Level_OnBeforeUpdate;
        Release();
    }

    /// <summary>Starts following a load into <paramref name="next"/>.</summary>
    // Every pick, not only one into another area: a state kept from an earlier chapter is still from
    // another area when the player then picks a checkpoint of this one.
    internal static void Start(Session next) {
        target = next;
        SceneSwitchClear.Suspend();
    }

    private static bool InChain(Scene scene) => scene switch {
        LevelEnter or SummitVignette or CoreVignette or LevelLoader => true,
        Level level => level.Session == target,
        _ => false,
    };

    // global:: because inside Celeste.Mod.* the bare name resolves to the namespace.
    private static void Celeste_OnSceneTransition(On.Celeste.Celeste.orig_OnSceneTransition orig,
        global::Celeste.Celeste self, Scene last, Scene next) {
        if (target != null && !InChain(next)) {
            CheckpointArrival.Cancel();
            Release();
        }
        orig(self, last, next);
    }

    // The outgoing level still updates after the pick, so the session tells the two apart.
    private static void Level_OnBeforeUpdate(Level level) {
        if (target != null && level.Session == target) Release();
    }

    private static void Release() {
        target = null;
        SceneSwitchClear.Resume();
    }
}
