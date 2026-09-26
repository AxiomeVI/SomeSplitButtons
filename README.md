# Some Split Buttons

Pause-menu buttons that split [SpeedrunTool](https://gamebanana.com/mods/53687)'s room timer at the
moment a real Save and Quit, Return to Map or cutscene skip would — so a segment that ends in one
can be practised and compared like any other.

Inspired by [WonderMods](https://github.com/WonderGinger/WonderMods)' Return to Menu split button.

## Requirements

- **Everest** 1.5935.0 or newer.
- **SpeedrunTool** 3.26.4 or newer. It is a hard dependency, not an optional one: "room timer"
  throughout this README means SpeedrunTool's room timer, and every split this mod makes is a call
  into it.

## Getting started

**All three buttons are off by default.** Installing the mod and pausing shows nothing until you
turn one on, in **Mod Options → Some Split Buttons**.

## The buttons

Each one unpauses, waits the number of frames the real action would have taken, and then splits.
The wait is what makes the split land where it would in a run rather than where you pressed.

- **Skip Cutscene Split** only appears during an end-of-chapter cutscene, and disappears after one
  use until the level is reloaded.
- **Save and Quit Split** reloads the room afterwards by default, exactly as the game does when a
  chapter is resumed after a real Save and Quit. Turn *Then Reload the Room* off to split without
  reloading; the chapter clock is then held stopped until you have control again,
  which is what a real Save and Quit would have done to it.
- **Return to Map Split** asks for confirmation first, the way vanilla Return to Map does. Turn on
  *Then Load a Checkpoint* in Mod Options to open a list of the chapter's unlocked
  checkpoints right after the split instead of leaving the level; loading into one holds the chapter
  clock stopped from the split until you have control again, the same way *Then Reload the Room*
  holds it for Save and Quit. Cancelling the list resumes where you stood, split and
  all. The checkpoint loads the way a real Return to Map leaves it: collected berries, hearts and
  cassettes come back as ghosts, keys go back to their spot and opened doors close. Only the
  chapter clock, deaths and dashes carry over.

## Collect protections

Leaving a chapter while a collectible is still being written to the save loses it, so the Save and
Quit and Return to Map splits refuse to fire and say how many frames are still needed.

- **Crystal hearts are always protected**, and this cannot be switched off. A split before the heart
  is banked is not a faster version of the strat — it is one the game does not allow.
- **Red berries are protected by default**, and *Berry Collect Protection* turns that off for
  practice where the berry is not the point. **Vanilla red berries only**: golden berries are
  ignored on purpose, and modded collectibles that are not `Strawberry` — CollabUtils silver and
  speed berries, for instance — are not seen at all.
- **With *Then Load a Checkpoint* on, the protection is asked at the press
  only.** A refused press means no split and no checkpoint list. The 31 frames after an accepted
  press stand in for vanilla's fade-out, so anything picked up in them is not a real collect:
  loading a checkpoint discards it, and Cancel keeps it.

⚠️ **While the Skip Cutscene button is enabled, SpeedrunTool's room timer keeps running through
every chapter ending**, including ones you finish by watching the cutscene or by using vanilla Skip
Cutscene. That is what lets the button split at the mark instead of at the cutscene trigger. Turn
the button off if you want SpeedrunTool's own end-of-chapter behaviour back.

## License

MIT — see [LICENSE](LICENSE). The berry timing helpers in `Source/Utils/BerryCheck.cs` are adapted
from [CelesteTAS](https://github.com/EverestAPI/CelesteTAS-EverestInterop), which is MIT too; the
attribution is in the file.
