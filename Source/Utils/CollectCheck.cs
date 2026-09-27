#nullable enable
namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>Every reason a collectible has for a split button to refuse, asked in one place.</summary>
// The heart goes first because it is the refusal a player cannot switch off, so it always applies.
internal static class CollectCheck {
    internal static string? BlockedMessage() => HeartCheck.BlockedMessage() ?? BerryCheck.BlockedMessage();

    /// <summary>Frames until a split would be accepted: 0 exactly when BlockedMessage is null.</summary>
    // At least 1 while anything blocks, whatever the estimate says: a berry only collects on safe
    // ground, so its figure is a floor, and a consumer must read 0 as "allowed" and nothing else.
    internal static int FramesUntilSplitAllowed() {
        int? heart = HeartCheck.RemainingFrames();
        int? berry = BerryCheck.ProtectedRemainingFrames();
        if (heart == null && berry == null) return 0;
        return System.Math.Max(1, System.Math.Max(heart ?? 0, berry ?? 0));
    }
}
