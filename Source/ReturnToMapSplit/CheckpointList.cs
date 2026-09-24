using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>The checkpoints of the current chapter and side, in map order, unlocked ones only.</summary>
internal static class CheckpointList {
    /// <summary>
    ///     Drops the "{SID}|" a sub-area checkpoint key carries, leaving the bare room name.
    /// </summary>
    // ⚠️ AreaData.GetCheckpointName strips this itself; AreaData.GetCheckpoint matches on
    // checkpointData.Level.Equals(level) with no pipe handling. So a prefixed key is right for
    // lookup and labelling and wrong for new Session(area, checkpoint), where it would set
    // Session.Level to a room that does not exist and leave Session.LevelData null.
    internal static string StripAreaPrefix(string checkpoint) {
        if (checkpoint == null) return null;
        int pipe = checkpoint.IndexOf('|');
        return pipe < 0 ? checkpoint : checkpoint.Substring(pipe + 1);
    }

    /// <summary>
    ///     The rows the picker shows, excluding start of chapter and Cancel: the map's ordered
    ///     checkpoints filtered to the ones this save has reached.
    /// </summary>
    // ⚠️ Order comes from the array, never from the unlocked set. That set is a HashSet<string>, and
    // iterating it lists checkpoints in hash order — which reads as a shuffle rather than a bug.
    // OuiChapterPanel is not a precedent for doing it the right way: it iterates the set.
    //
    // ⚠️ SaveData.GetCheckpoints returns the live Areas_Safe[...].Checkpoints set and calls
    // RemoveWhere on it. It is not a pure read; do not call it more often than needed.
    internal static List<(string Key, string Label)> ForArea(AreaKey area) {
        List<(string, string)> rows = new();
        HashSet<string> unlocked = SaveData.Instance?.GetCheckpoints(area);
        if (unlocked == null) return rows;

        AreaData data = AreaData.Get(area);
        CheckpointData[] checkpoints = data?.Mode[(int) area.Mode]?.Checkpoints;
        if (checkpoints == null) return rows;

        foreach (CheckpointData checkpoint in checkpoints) {
            // Sub-area maps store the key prefixed. Test both spellings rather than guessing which
            // one this map uses.
            string prefixed = $"{data.SID}|{checkpoint.Level}";
            string key = unlocked.Contains(checkpoint.Level) ? checkpoint.Level
                : unlocked.Contains(prefixed) ? prefixed
                : null;
            if (key == null) continue;

            rows.Add((key, AreaData.GetCheckpointName(area, key)));
        }
        return rows;
    }
}
