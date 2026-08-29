#nullable enable
namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>
/// Every reason a collectible has for a split button to refuse, asked in one place.
/// </summary>
// Two buttons ask this, and each used to ask BerryCheck directly. A third reason wired into one of
// them and forgotten in the other is the defect this shape makes hard to write — the same accident
// SplitFeatures exists for, one list further down.
//
// The heart is asked first because a player who is both carrying a berry and mid-collect has one
// wait to serve, the longer one: 124 updates against at most 18 per berry. Showing both on one line
// would not shorten it.
public static class CollectCheck {
    public static string? BlockedMessage() => HeartCheck.BlockedMessage() ?? BerryCheck.BlockedMessage();
}
