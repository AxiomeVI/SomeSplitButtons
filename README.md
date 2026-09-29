# Some Split Buttons

Pause-menu buttons that split [SpeedrunTool](https://gamebanana.com/mods/53687)'s room timer where a
real Save and Quit, Return to Map or cutscene skip would. Segments that end in one of those can then
be practised like any other.

Inspired by [WonderMods](https://github.com/WonderGinger/WonderMods)' Return to Menu split button.

## Requirements

- Everest 1.5935.0 or newer
- SpeedrunTool 3.26.4 or newer

## Setup

All three buttons are off by default. Turn them on in **Mod Options → Some Split Buttons**. Each one
can also be toggled with a hotkey, bound from the same page.

## The buttons

A button waits as long as the real action would take, then splits: 31 frames for Save and Quit and
Return to Map, 18 for Skip Cutscene (232 in the Prologue). Save and Quit and Return to Map keep the
game paused during the wait, so nothing can move, die or be collected. Skip Cutscene unpauses and the
cutscene plays on.

### Skip Cutscene Split

Shown during end-of-chapter cutscenes, once per level load. Not shown in the Epilogue or in
Farewell's ending, where the timer doesn't run.

The room timer keeps running into the cutscene until the split, the way file time does in a run.
The chapter still completes when the cutscene ends, as usual.

**Then Load the Next Chapter** opens a list after the split with the next chapter and this chapter's
B-side. Picking one loads it from the start. Picking from the Prologue marks the Prologue complete.

### Save and Quit Split

Reloads the room afterwards, as the game does when you resume after a real Save and Quit. Turn off
**Then Reload the Room** to split without reloading. Either way, the room timer stays stopped until
you have control again.

### Return to Map Split

Asks for confirmation first, like vanilla. **Then Load a Checkpoint** opens a list after the split:

- every checkpoint of the chapter, locked or not, except the start (restart the chapter for that);
- after a cassette collected in this attempt: this chapter's B-side and C-side;
- after a chapter's last heart (B-side, C-side, Core A-side): the next chapter's A-side and the same
  side of the next chapter.

The cassette and heart rows stay until you change rooms, and survive a death. Rows are named the
speedrun way (`5b`, `8a` for Core, `Farewell`). Cancel resumes where you stood, with the split
kept.

### Loading from a list

- The game pauses, sound included, while a list is open.
- The room timer stays stopped from the split until you have control again.
- SpeedrunTool's save state and room timer survive the load, even into another chapter.
- A chapter row starts the chapter fresh, B-side title card included.
- While a list could still carry the run on, SpeedrunTool doesn't treat the chapter as finished, so
  it shows no end-of-run comparison. Cancel and the end is recorded as usual.

### Details

- A pressed button finishes its split even if you turn it off during the wait.
- A button is greyed out wherever the game greys out its action.
- SpeedrunTool's save and load hotkeys do nothing during a paused wait.
- With a SpeedrunTool end point set, the Save and Quit and Return to Map splits record nothing. The
  pause menu says so.
- While Skip Cutscene Split is enabled, the room timer keeps running through every chapter ending,
  including ones you watch or skip normally. Turn the button off to get SpeedrunTool's usual
  end-of-chapter behaviour back.

## Collect protection

Leaving a chapter while a collectible is still being saved loses it. Save and Quit and Return to Map
refuse to split in that window, and say how many frames are left.

- Crystal hearts are always protected.
- Red berries are protected unless **Berry Collect Protection** is off.
- With Then Load a Checkpoint on, the check happens at the press only. The game stays paused until
  the list opens.

## For mod authors

The mod exports a ModInterop surface named `SomeSplitButtons`, so another mod can watch the buttons
without referencing this assembly.

```csharp
[ModImportName("SomeSplitButtons")]
public static class SomeSplitButtons {
    public static Action<Action<string, string, ulong>> AddSplitObserver;
    public static Action<Action<string, string, ulong>> RemoveSplitObserver;
    public static Func<int> InteropVersion;
    public static Func<int> FramesUntilSplitAllowed;   // version 2
}

// In Load(), after typeof(SomeSplitButtons).ModInterop():
SomeSplitButtons.AddSplitObserver?.Invoke((action, stage, frame) =>
    Logger.Log("MyMod", $"{action} {stage} on frame {frame}"));
```

- `action` is `SkipCutscene`, `SaveAndQuit` or `ReturnToMap`.
- `stage` is `Pressed`, `Opened`, `Confirmed`, `Cancelled` or `Refused`.
- `frame` is `Engine.FrameCounter` at that stage.

Every `Pressed` ends in exactly one `Confirmed`, `Cancelled` or `Refused`:

- Skip Cutscene: `Pressed` → `Confirmed`.
- Save and Quit: `Pressed` → `Confirmed` or `Refused`.
- Return to Map: `Pressed` → `Opened` → `Confirmed`, `Refused` or `Cancelled`, or `Pressed` →
  `Cancelled` if the confirmation never opened.

`Confirmed` is the frame the press is accepted, not the split; the split follows after the wait
above. A button isn't offered again until its split lands. Loading a SpeedrunTool save state emits
nothing: a state saved during the wait brings the pending split back without a new `Pressed`, and an
earlier state drops it silently. `Refused` means a collect protection turned the press down.

`FramesUntilSplitAllowed()` (version 2) returns how many frames until a Save and Quit or Return to Map
split would be accepted: `0` if now, at least `1` while a protection would refuse. Berries count only
while Berry Collect Protection is on. A berry only collects on safe ground, so treat any non-zero
value as "not yet".

`InteropVersion()` returns `2`. It goes up when an action, a stage or a signature changes. An observer
that throws is logged once and can't break a split or other observers.

## License

MIT, see [LICENSE](LICENSE). The berry timing helpers in `Source/Utils/BerryCheck.cs` are adapted
from [CelesteTAS](https://github.com/EverestAPI/CelesteTAS-EverestInterop) (MIT); attribution is in
the file.
