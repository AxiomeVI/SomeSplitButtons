using Celeste.Mod.SpeedrunTool.RoomTimer;
using Monocle;
using System;

namespace Celeste.Mod.SomeSplitButtons.SaveAndQuitSplitManager;
public static class SaveAndQuitTimer {
    private static int counter = 0;
    private static bool pressed = false;
    private const int SQ_FADEOUT_FRAMES = 31;

    // CelesteTAS info hud function https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L546
    internal static int ToCeilingFrames(this float timer) {
        if (timer <= 0.0f) {
            return 0;
        }

        float frames = MathF.Ceiling(timer / Engine.DeltaTime);
        return float.IsInfinity(frames) || float.IsNaN(frames) ? int.MaxValue : (int) frames;
    }
    
    public static void HandleTimerButtonPressed() {
        Player player = (Engine.Scene as Level).Tracker.GetEntity<Player>();
        // CelesteTAS info hud format https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/ae25bf3f2fa931d362c3a321c2cf8dae58d2eb28/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L307
        Follower? firstRedBerryFollower = player.Leader.Followers.Find(follower => follower.Entity is Strawberry {Golden: false});
        if (firstRedBerryFollower?.Entity is Strawberry firstRedBerry) {
            float collectTimer = firstRedBerry.collectTimer;
            if (collectTimer <= 0.15f) {
                int collectFrames = (0.15f - collectTimer).ToCeilingFrames();
                if (collectTimer >= 0f) {
                    SomeSplitButtonsModule.PopupMessage($"Berry({collectFrames}) ");
                } else {
                    int additionalFrames = Math.Abs(collectTimer).ToCeilingFrames();
                    SomeSplitButtonsModule.PopupMessage($"Berry({collectFrames - additionalFrames}+{additionalFrames}) ");
                }
            }
            return;
        }
        pressed = true;
        counter = 0;
    }

    public static void Update() {
        if (pressed) {
            counter++;
            if (counter >= SQ_FADEOUT_FRAMES) {
                pressed = false;
                counter = 0;
                RoomTimerManager.UpdateTimerState();
            }
        }
        else {
            counter = 0;
        }
    }
}