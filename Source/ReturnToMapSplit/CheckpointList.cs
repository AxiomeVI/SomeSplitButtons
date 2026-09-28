using System;
using System.Collections.Generic;

namespace Celeste.Mod.SomeSplitButtons.ReturnToMapSplit;

/// <summary>Every checkpoint of the current chapter and side, in map order.</summary>
// Locked ones included, by decision: the list is for practice files. The start is never listed; a
// chapter restart reloads it, and SpeedrunTool times that natively.
internal static class CheckpointList {
    /// <summary>The rows the list shows before the destinations and Cancel.</summary>
    internal static List<(string Key, string Label)> ForArea(AreaKey area) {
        AreaData data = AreaData.Get(area);
        CheckpointData[] checkpoints = data?.HasMode(area.Mode) == true ? data.Mode[(int) area.Mode].Checkpoints : null;
        return Rows(checkpoints, room => AreaData.GetCheckpointName(area, room));
    }

    /// <summary>One row per checkpoint, keyed by its bare room name.</summary>
    internal static List<(string Key, string Label)> Rows(CheckpointData[] checkpoints, Func<string, string> label) {
        List<(string, string)> rows = new();
        if (checkpoints == null) return rows;
        foreach (CheckpointData checkpoint in checkpoints) rows.Add((checkpoint.Level, label(checkpoint.Level)));
        return rows;
    }
}
