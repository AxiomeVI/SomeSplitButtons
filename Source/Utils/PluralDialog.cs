#nullable enable
using System;

namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>
///     The form of a counted dialog entry that fits the count, in the language that will show it.
/// </summary>
// Everest has no plural support: an entry is one string whatever number fills it, and the refusals
// can say "1 more frames". So an entry that takes a count comes in forms, told apart by a suffix:
//
//   SSB_HEART_BLOCKS_SPLIT        the general form, and the one every language must have
//   SSB_HEART_BLOCKS_SPLIT_ONE    English 1; French and Portuguese 0 and 1; Russian 1, 21, 31...
//   SSB_HEART_BLOCKS_SPLIT_FEW    Russian 2-4, 22-24... Nothing else uses it.
//
// A missing suffixed form falls back to the general one, so a language with a single form (Chinese,
// Japanese, Korean) adds nothing, and an old translation keeps working as it did.
//
// ⚠️ Rule and entries come from the same file. Everest falls back to English one id at a time, so
// a language that has not translated an entry shows English's text, and must get English's rule
// with it: Russian's FEW picking English's general form for 2 frames would be right by accident,
// but French's ONE picking English's singular for 0 frames would read "0 more frame".
internal static class PluralDialog {
    internal const string One = "_ONE";
    internal const string Few = "_FEW";

    /// <summary>The raw entry for <paramref name="count"/>, still to be formatted.</summary>
    // Raw, not Cleaned, for the reason PauseMenuButtons.Description gives: Clean deletes {0}.
    internal static string Get(string id, int count) {
        Language? language = Dialog.Language;
        if (language == null || !language.Dialog.ContainsKey(id)) {
            language = Dialog.Languages.TryGetValue("english", out Language? english) ? english : language;
        }
        if (language == null) return Dialog.Get(id);

        string? suffix = Suffix(language.Id, count);
        return suffix != null && language.Dialog.TryGetValue(id + suffix, out string? form)
            ? form
            : Dialog.Get(id, language);
    }

    /// <summary>
    ///     Which suffixed form <paramref name="count"/> takes in the language with that id, or null for
    ///     the general form.
    /// </summary>
    // Integer counts only: the CLDR categories reduced to what a whole, non-negative number can hit.
    // Russian "other" is for fractions, so its "many" is the general form here.
    //
    // A language not listed gets English's rule. For Chinese, Japanese and Korean that is harmless,
    // since they define no _ONE for it to find; for a modded language it is the likeliest guess.
    internal static string? Suffix(string? languageId, int count) {
        int n = Math.Abs(count);
        switch (languageId?.ToLowerInvariant()) {
            case "french":
            case "brazilian":
                return n <= 1 ? One : null;
            case "russian": {
                int mod10 = n % 10, mod100 = n % 100;
                if (mod10 == 1 && mod100 != 11) return One;
                if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14) return Few;
                return null;
            }
            default:
                return n == 1 ? One : null;
        }
    }
}
