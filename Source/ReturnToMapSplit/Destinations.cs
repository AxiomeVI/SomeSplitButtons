using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>What opened the destination window, or, for Ending, the split that opens the list directly.</summary>
internal enum WindowTrigger { None, Cassette, Heart, Ending }

/// <summary>
///     Which areas the list offers after a collect or a chapter's ending, and the label and row key of
///     each.
/// </summary>
// Pure, so it runs off-engine. Game IDs, not runners' numbers: ID 8 is the Epilogue, an interlude with
// no sides, Core is ID 9 and Farewell ID 10, A-side only.
internal static class Destinations {
    internal const int Epilogue = 8;
    internal const int Core = 9;
    internal const int Farewell = 10;

    // ⚠️ A room name cannot contain a NUL, which is what keeps a key from colliding with a checkpoint.
    private const string KeyPrefix = "\0area:";

    /// <summary>The chapter after <paramref name="id"/>, skipping the Epilogue; null after Farewell.</summary>
    internal static int? NextChapter(int id) => id switch {
        >= 0 and <= 6 => id + 1,
        7 => Core,
        Core => Farewell,
        _ => null,
    };

    /// <summary>The areas to offer, in list order.</summary>
    /// <param name="vanilla">The area is in the <c>Celeste</c> level set.</param>
    /// <param name="hasBSide">The area's map has a B-side. Read only off vanilla.</param>
    internal static List<(int Id, AreaMode Mode)> For(WindowTrigger trigger, int id, AreaMode mode,
        bool vanilla, bool hasBSide) {
        List<(int, AreaMode)> areas = new();
        switch (trigger) {
            case WindowTrigger.Cassette when mode == AreaMode.Normal:
                if (vanilla) {
                    areas.Add((id, AreaMode.BSide));
                    areas.Add((id, AreaMode.CSide));
                }
                else if (hasBSide) {
                    areas.Add((id, AreaMode.BSide));
                }
                break;
            case WindowTrigger.Heart when vanilla && mode != AreaMode.Normal:
                if (NextChapter(id) is not int next) break;
                areas.Add((next, AreaMode.Normal));
                if (next != Farewell) areas.Add((next, mode));
                break;
            case WindowTrigger.Heart when vanilla && id == Core:
                areas.Add((Farewell, AreaMode.Normal));
                areas.Add((Core, AreaMode.BSide));
                break;
            // IDs 0 to 7 by name: NextChapter(9) is Farewell, which no ending leads to.
            case WindowTrigger.Ending when vanilla && mode == AreaMode.Normal && id >= 0 && id <= 7:
                areas.Add((NextChapter(id).Value, AreaMode.Normal));
                if (id != 0) areas.Add((id, AreaMode.BSide));
                break;
        }
        return areas;
    }

    /// <summary>Runners' name for a vanilla area: <c>5b</c>, <c>8a</c> for Core, <c>Farewell</c>.</summary>
    internal static string Label(int id, AreaMode mode) {
        if (id == Farewell) return "Farewell";
        int chapter = id == Core ? 8 : id;
        return $"{chapter}{"abc"[(int) mode]}";
    }

    internal static string Key(int id, AreaMode mode) => $"{KeyPrefix}{id}:{(int) mode}";

    internal static bool IsKey(string key) => key != null && key.StartsWith(KeyPrefix);

    internal static bool TryParseKey(string key, out int id, out AreaMode mode) {
        id = 0;
        mode = AreaMode.Normal;
        if (!IsKey(key)) return false;
        string[] parts = key.Substring(KeyPrefix.Length).Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out id) || !int.TryParse(parts[1], out int m)) return false;
        mode = (AreaMode) m;
        return true;
    }
}
