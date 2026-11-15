#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace Celeste.Mod.SomeSplitButtons.Utils;

/// <summary>Fills the {0}, {1}, … of a dialog entry.</summary>
// Not string.Format: the entry is a translator's text, and Format throws on a stray brace in it —
// on every pause, for a description. Any brace that is not a placeholder with an argument is left
// as written, {n} markup included. One pass, so an argument quoting "{1}" is not filled in turn.
internal static class DialogFormat {
    internal static string Fill(string? template, params object?[] args) {
        if (template == null) return "";

        StringBuilder filled = new(template.Length);
        int at = 0;
        while (at < template.Length) {
            int open = template.IndexOf('{', at);
            if (open < 0) break;
            int close = template.IndexOf('}', open + 1);
            if (close < 0) break;

            filled.Append(template, at, open - at);
            string inside = template.Substring(open + 1, close - open - 1);
            // NumberStyles.None: digits only, so "{ 0}" and "{-1}" are not placeholders.
            if (int.TryParse(inside, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index < args.Length) {
                filled.Append(Convert.ToString(args[index], CultureInfo.InvariantCulture));
                at = close + 1;
            }
            else {
                // Only the brace is copied: "{{0}" still fills its {0}.
                filled.Append('{');
                at = open + 1;
            }
        }
        filled.Append(template, at, template.Length - at);
        return filled.ToString();
    }
}
