using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Mod.SpeedrunTool.RoomTimer;

namespace Celeste.Mod.SomeSplitButtons.Integration;

/// <summary>What one key held in one of SpeedrunTool's record tables before a write.</summary>
internal sealed class RecordSnapshot {
    internal string Key { get; init; }
    internal bool Present { get; init; }
    internal object Value { get; init; }
}

/// <summary>
///     Takes back SpeedrunTool's end-of-run record when a pick carries the run on, and says whether
///     the arrival starts a new room.
/// </summary>
// On a level it is shown completed before NumberOfRooms is reached, RoomTimerData.UpdateTimerState
// treats the run as finished and writes the time under pbTimeKey (prefix + NumberOfRooms) in
// ThisRunTimes, PbTimes and BestSegments. A pick after a chapter's end carries the run on, so that
// record would stand as the time and PB of a room not yet reached. Leaving the level any other way
// keeps it: the run ended where SpeedrunTool says.
//
// Reflection, for SpeedrunToolHooks' reason; a miss makes every call here a no-op.
internal static class SpeedrunToolRecords {
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly string[] Tables = {"ThisRunTimes", "PbTimes", "BestSegments"};

    // Per room timer (SpeedrunTool keeps a current-room and a next-room one), the tables as they were
    // before the first end-of-run write since the last Forget.
    private static readonly Dictionary<object, RecordSnapshot[]> pending = new();

    internal static RecordSnapshot Capture(string key, IDictionary table) =>
        new() {Key = key, Present = table.Contains(key), Value = table.Contains(key) ? table[key] : null};

    internal static void Restore(RecordSnapshot snapshot, IDictionary table) {
        if (snapshot.Present) table[snapshot.Key] = snapshot.Value;
        else table.Remove(snapshot.Key);
    }

    /// <summary>Whether the record under the current room is a segment of its own, not a repeat of the one before.</summary>
    // A heart split by SpeedrunTool's TimeHeartCassette is followed by the completion's split at the same
    // time: that second record is a repeat, and the next room may overwrite it.
    internal static bool LastRecordIsASegment(IDictionary thisRun, string currentKey, string previousKey) =>
        currentKey != null && thisRun.Contains(currentKey)
                           && (previousKey == null || !thisRun.Contains(previousKey)
                               || !Equals(thisRun[currentKey], thisRun[previousKey]));

    /// <summary>Forgets what was taken; called with every level load.</summary>
    // ⚠️ Reached from Level_OnLoadingThread, on the loader's thread, after any pick has used it.
    internal static void Forget() => pending.Clear();

    /// <summary>Runs one UpdateTimerState call, noting the tables if the call writes the end of the run.</summary>
    internal static void Around(Action call) {
        List<(object Data, IDictionary ThisRun, RecordSnapshot[] Before)> watched = new();
        foreach (object data in RoomTimers()) {
            if (pending.ContainsKey(data)) continue;
            string thisKey = Read(data, "thisRunTimeKey") as string;
            string pbKey = Read(data, "pbTimeKey") as string;
            // The same key: the run's real last room, not an early end.
            if (pbKey == null || pbKey == thisKey) continue;
            RecordSnapshot[] before = new RecordSnapshot[Tables.Length];
            bool readable = true;
            for (int i = 0; i < Tables.Length; i++) {
                if (Read(data, Tables[i]) is not IDictionary table) { readable = false; break; }
                before[i] = Capture(pbKey, table);
            }
            if (readable) watched.Add((data, (IDictionary) Read(data, Tables[0]), before));
        }

        call();

        foreach ((object data, IDictionary thisRun, RecordSnapshot[] before) in watched) {
            RecordSnapshot after = Capture(before[0].Key, thisRun);
            if (after.Present != before[0].Present || !Equals(after.Value, before[0].Value)) pending[data] = before;
        }
    }

    /// <summary>
    ///     Takes back any end-of-run record written in this level, and says whether the arrival should
    ///     advance SpeedrunTool's room.
    /// </summary>
    internal static bool KeepRunGoing() {
        foreach ((object data, RecordSnapshot[] before) in pending) {
            for (int i = 0; i < Tables.Length; i++) {
                if (Read(data, Tables[i]) is IDictionary table) Restore(before[i], table);
            }
            ResumeTiming(data);
        }
        pending.Clear();

        object current = typeof(RoomTimerManager)
            .GetField("CurrentRoomTimerData", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        return current != null && Read(current, "ThisRunTimes") is IDictionary thisRun
                               && LastRecordIsASegment(thisRun, Read(current, "thisRunTimeKey") as string,
                                   Read(current, "thisRunPrevRoomTimeKey") as string);
    }

    // The run goes on, so the timer is no longer Completed. While it is, its display shows the end-of-run
    // record, just taken back: 0 through the destination's intro. Timing adds nothing before the new
    // level's clock starts.
    private static void ResumeTiming(object data) {
        FieldInfo state = data.GetType().GetField("timerState", AnyInstance);
        if (state == null || state.GetValue(data)?.ToString() != "Completed") return;
        if (Enum.TryParse(state.FieldType, "Timing", out object timing)) state.SetValue(data, timing);
    }

    /// <summary>Takes one room back from each room timer, as a split recorded as an ordinary room is revealed as the end.</summary>
    // The keys too: SpeedrunTool recomputes them only after it calls UpdateTimerState on a frame, so the
    // split that follows would otherwise write under the next room's key.
    internal static void UndoAdvance(Level level) {
        foreach (object data in RoomTimers()) {
            FieldInfo room = data.GetType().GetField("roomNumber", AnyInstance);
            if (room?.GetValue(data) is not int number || number <= 1) continue;
            room.SetValue(data, number - 1);
            data.GetType().GetMethod("UpdateTimeKeys", AnyInstance, null, new[] {typeof(Level)}, null)
                ?.Invoke(data, new object[] {level});
        }
    }

    /// <summary>Whether the current room timer holds no time since its last record.</summary>
    internal static bool NoTimeSinceLastRecord() {
        object current = typeof(RoomTimerManager)
            .GetField("CurrentRoomTimerData", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        if (current == null || Read(current, "ThisRunTimes") is not IDictionary thisRun
            || Read(current, "thisRunPrevRoomTimeKey") is not string previousKey || !thisRun.Contains(previousKey)) {
            return false;
        }
        return Equals(Read(current, "Time"), thisRun[previousKey]);
    }

    private static IEnumerable<object> RoomTimers() {
        foreach (string name in new[] {"CurrentRoomTimerData", "NextRoomTimerData"}) {
            object data = typeof(RoomTimerManager)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (data != null) yield return data;
        }
    }

    private static object Read(object data, string name) =>
        data.GetType().GetField(name, AnyInstance)?.GetValue(data)
        ?? data.GetType().GetProperty(name, AnyInstance)?.GetValue(data);
}
