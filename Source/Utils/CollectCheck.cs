#nullable enable
namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>Every reason a collectible has for a split button to refuse, asked in one place.</summary>
// The heart goes first because it is the refusal a player cannot switch off, so it always applies.
internal static class CollectCheck {
    internal static string? BlockedMessage() => HeartCheck.BlockedMessage() ?? BerryCheck.BlockedMessage();
}
