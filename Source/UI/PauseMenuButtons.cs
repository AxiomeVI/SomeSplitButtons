using System.Collections.Generic;
using Celeste.Mod.SomeSplitButtons.Interop;
using Celeste.Mod.SomeSplitButtons.Splits;
using static Celeste.TextMenuExt;

namespace Celeste.Mod.SomeSplitButtons.UI;

/// <summary>Where the split buttons go in the pause menu, and how they get there.</summary>
// What each button *is* lives in SplitFeatures. This file is only the placement rules, and they are
// the same three for every feature.
internal static class PauseMenuButtons {
    private static readonly HashSet<string> warnedMissingAnchors = new();

    /// <summary>Adds every enabled split button to a pause menu being built.</summary>
    // Fixed slots, not anchors: every vanilla lookup below decides whether this menu should carry a
    // split button, never where it goes. Anchoring cannot express "two Down presses" —
    // ExtendedVariantMode inserts above Options and pushes anything measured from it down one.
    internal static void Create(Level level, TextMenu menu, bool minimal) {
        if (!SomeSplitButtonsModule.Settings.Enabled) return;

        foreach (SplitFeature feature in SplitFeatures.InMenuOrder) {
            if (!feature.Enabled()) continue;
            if (feature.Available != null && !feature.Available(level)) continue;

            bool warn = feature.WarnsWhenAnchorMissingInMinimal || !minimal;
            if (VanillaButtonIndex(menu, feature.AnchorDialogId, warn) < 0) continue;

            SplitButton button = new(feature, Dialog.Clean(feature.ButtonLabelId));
            button.Pressed(() => {
                SplitEvents.Emit(feature.InteropAction, SplitStages.Pressed);
                string terminal = feature.Press(level, menu);
                if (terminal != null) SplitEvents.Emit(feature.InteropAction, terminal);
            });

            Insert(menu, SlotIndex(menu, feature.Slot), button,
                Description(feature.DescriptionId(), feature.DescriptionFrames()));
        }
    }

    /// <summary>
    ///     Index of the vanilla pause-menu button carrying <paramref name="dialogId"/>, or -1 when
    ///     this menu does not have it.
    /// </summary>
    private static int VanillaButtonIndex(TextMenu menu, string dialogId, bool warnIfMissing) {
        string label = Dialog.Clean(dialogId);
        int index = menu.Items.FindIndex(item => item is TextMenu.Button button && button.Label == label);

        // Once per anchor per session: the menu is rebuilt on every pause. Without the warning at
        // all, the only symptom is a button that quietly never appears.
        if (index < 0 && warnIfMissing && warnedMissingAnchors.Add(dialogId)) {
            Logger.Warn(nameof(SomeSplitButtonsModule),
                $"no '{dialogId}' button in the pause menu — the split button anchored on it is being skipped. " +
                "Another mod has probably replaced or removed it.");
        }
        return index;
    }

    /// <summary>
    ///     A split button's description, with the number of frames it will actually wait filled in.
    /// </summary>
    // Dialog.Get and not Dialog.Clean. Language.LoadTxt builds the Cleaned dictionary by running
    // `\{(.*?)\}` over every value and replacing each match with "" unless it is {n} or {break} — so
    // Clean deletes the placeholder along with the dialogue markup it shares its braces with, and
    // the sentence reaches the player as "after  frames". Every parameterised string in this mod is
    // read this way. Both arguments are always supplied; entries quoting only frames ignore {1}.
    private static string Description(string dialogId, int frames)
        => string.Format(Dialog.Get(dialogId), frames, SplitTimings.ToSeconds(frames));

    /// <summary>
    ///     The insertion index that makes a new entry the <paramref name="slot"/>-th one the cursor
    ///     can land on.
    /// </summary>
    // Hoverable, not Selectable: MoveCursor loops `while (!Current.Hoverable)`, so a greyed-out
    // Retry is stepped over and costs no press. That is why the Save and Quit button lands below
    // Options during a wake-up and above it during normal play.
    //
    // ⚠️ The returned index is always *before a reachable entry*, never before an unreachable one.
    // A button and its description occupy one reachable slot and two items, so returning the index
    // of a description would cut that pair in half and leave each button showing its neighbour's
    // text. Falling through to the end is how SplitFeature.LAST asks for last place.
    private static int SlotIndex(TextMenu menu, int slot) {
        int reachable = 0;
        for (int i = 0; i < menu.Items.Count; i++) {
            if (!menu.Items[i].Hoverable) continue;
            if (reachable == slot) return i;
            reachable++;
        }
        return menu.Items.Count;
    }

    private static void Insert(TextMenu menu, int index, TextMenu.Button button, string description) {
        EaseInSubHeaderExt descriptionText = new(description, false, menu, null) {
            HeightExtra = 0f
        };
        // Description first, same index: the button displaces it and ends up above it. Swapping
        // these two lines inverts the pair.
        menu.Insert(index, descriptionText);
        menu.Insert(index, button);
        button.OnEnter = () => descriptionText.FadeVisible = true;
        button.OnLeave = () => descriptionText.FadeVisible = false;
    }
}
