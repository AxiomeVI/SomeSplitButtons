#nullable enable
using System;
using System.Globalization;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.Utils;

// Everest deprecates touching Engine.TimeRate, to keep mods from fighting over it. Reading it is
// the only way to tell a seeker or Oshiro slowdown apart from the Assist Mode game speed, which
// rides on Engine.TimeRateB and is not transient.
#pragma warning disable CS0618

/// <summary>
/// A carried red berry is only secured after BERRY_COLLECT_TIMER seconds. Leaving the room before
/// that loses it, so a split button must refuse to fire and report the missing frames instead.
/// </summary>
public static class BerryCheck {
    private const float BERRY_COLLECT_TIMER = 0.15f;

    // CelesteTAS info hud function https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L546
    //
    // Private and not an extension. It was `internal static int ToCeilingFrames(this float ...)`,
    // which put a generically named method on every float in the assembly to serve one call site
    // twenty lines below.
    private static int ToCeilingFrames(float timer, float deltaTime) {
        if (timer <= 0.0f) {
            return 0;
        }

        float frames = MathF.Ceiling(timer / deltaTime);
        return float.IsInfinity(frames) || float.IsNaN(frames) ? int.MaxValue : (int) frames;
    }

    /// <summary>
    /// True while a seeker attack or a close Oshiro is slowing the game down. Both drive
    /// Engine.TimeRate and nothing else, and both bottom out at 0.5.
    /// </summary>
    private static bool Slowed => Engine.TimeRate > 0f && Engine.TimeRate < 1f;

    /// <summary>
    /// A frame as long as it would be without that slowdown. Only Engine.TimeRate is divided back
    /// out: the Assist Mode game speed rides on Engine.TimeRateB, and a player using it wants their
    /// own frames, not someone else's.
    /// </summary>
    private static float NormalDeltaTime =>
        Engine.TimeRate > 0f ? Engine.DeltaTime / Engine.TimeRate : Engine.RawDeltaTime;

    /// <summary>
    /// How many frames the carried red berries still need, or null when nothing is carried. This is
    /// the headline figure: measured at normal speed, so it stays comparable between attempts.
    /// </summary>
    internal static int? CurrentRemainingFrames => RemainingFrames(Engine.Scene as Level, NormalDeltaTime);

    /// <summary>
    /// What that same wait costs at the rate the game is running right now, or null when nothing is
    /// slowing it down.
    /// </summary>
    internal static int? CurrentSlowdownFrames =>
        Slowed ? RemainingFrames(Engine.Scene as Level, Engine.DeltaTime) : null;

    /// <summary>
    /// Returns true when the player carries a red berry, meaning the caller must not split.
    /// A popup reports how many frames are still missing before the berry is secured.
    /// </summary>
    public static bool BlocksSplit() {
        if (CurrentRemainingFrames is not int frames) return false;

        // Always say something. The refusal is otherwise invisible: the button does nothing and the
        // split silently does not happen.
        string message = string.Format(Dialog.Get(DialogIds.BerryBlocksSplitId), frames);

        // The rate is formatted invariant so it reads 0.5x and never 0,5x.
        if (CurrentSlowdownFrames is int slowedFrames) {
            message += " " + string.Format(Dialog.Get(DialogIds.BerrySlowdownId), slowedFrames,
                                           Engine.TimeRate.ToString("0.##", CultureInfo.InvariantCulture));
        }

        SomeSplitButtonsModule.PopupMessage(message);
        return true;
    }

    /// <summary>
    /// How many frames of <paramref name="deltaTime"/> the carried red berries still need, or null
    /// when none is carried.
    /// </summary>
    private static int? RemainingFrames(Level? level, float deltaTime) {
        Player? player = level?.Tracker.GetEntity<Player>();
        if (player == null) return null;

        // Every red berry, not just the first one. StrawberryRegistry.IsFirstStrawberry gates the
        // countdown, so berries bank one at a time and the wait is their sum. Reading the first
        // follower alone reports the moment the player loses one berry fewer than they are carrying.
        int redBerries = 0;
        int collectFrames = 0;
        foreach (Follower follower in player.Leader.Followers) {
            if (follower.Entity is not Strawberry {Golden: false} redBerry) continue;

            // CelesteTAS info hud function https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L307
            // A negative collectTimer means the berry first has to climb back out of it: either the
            // player is off safe ground, or the berry is queued behind another and pinned at -0.15.
            // A queued berry therefore costs a full 18 frames here, which is what it really costs.
            ++redBerries;
            collectFrames += ToCeilingFrames(BERRY_COLLECT_TIMER - redBerry.collectTimer, deltaTime);
        }
        return redBerries == 0 ? null : collectFrames;
    }
}

#pragma warning restore CS0618
