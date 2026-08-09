using System;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>
/// A carried red berry is only secured after BERRY_COLLECT_TIMER seconds. Leaving the room before
/// that loses it, so a split button must refuse to fire and report the missing frames instead.
/// </summary>
public static class BerryCheck {
    private const float BERRY_COLLECT_TIMER = 0.15f;

    // CelesteTAS info hud function https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L546
    internal static int ToCeilingFrames(this float timer) {
        if (timer <= 0.0f) {
            return 0;
        }

        float frames = MathF.Ceiling(timer / Engine.DeltaTime);
        return float.IsInfinity(frames) || float.IsNaN(frames) ? int.MaxValue : (int) frames;
    }

    /// <summary>
    /// Returns true when the player carries a red berry, meaning the caller must not split.
    /// A popup reports how many frames are still missing before the berry is secured.
    /// </summary>
    public static bool BlocksSplit(Level level) {
        Player player = level.Tracker.GetEntity<Player>();
        Follower? firstRedBerryFollower = player?.Leader.Followers.Find(follower => follower.Entity is Strawberry {Golden: false});
        if (firstRedBerryFollower?.Entity is not Strawberry firstRedBerry) return false;

        // CelesteTAS info hud format https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L307
        float collectTimer = firstRedBerry.collectTimer;
        if (collectTimer <= BERRY_COLLECT_TIMER) {
            int collectFrames = (BERRY_COLLECT_TIMER - collectTimer).ToCeilingFrames();
            if (collectTimer >= 0f) {
                SomeSplitButtonsModule.PopupMessage($"Berry({collectFrames}) ");
            } else {
                int additionalFrames = Math.Abs(collectTimer).ToCeilingFrames();
                SomeSplitButtonsModule.PopupMessage($"Berry({collectFrames - additionalFrames}+{additionalFrames}) ");
            }
        }
        return true;
    }
}
