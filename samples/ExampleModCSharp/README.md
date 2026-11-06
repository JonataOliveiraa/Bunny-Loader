# Example Mod C#

The tModLoader `ExampleMod`, ported to Bunny Loader. Of its 451 active files, 438
are here or have their API covered. This file lists what is not here and why.

`ModInvasion` and its two invasions (Paladin Crusade and Grave Legion, by
Animatak_) and the touch buttons are ours: tModLoader has neither.

## Not for now

| Files | Why |
|---|---|
| `ExampleAccessorySlot` | `ModAccessorySlot` is not implemented: the phone's inventory screen is compact, and where extra slots would go is still an open design question |

## Out

| Files | Why |
|---|---|
| `ExamplePalmTree` | Depends on checks in the middle of `WorldGen`. Revisit if a biome mod asks |
| `ExampleWorldGenHookingSystem` | Uses `ILContext`: IL edits cannot exist on IL2CPP. `ModifyWorldGenTasks` covers the common case |
| `ExampleCustomFramingTile` | Needs `ModifyFrameMerge` and `PostTileFrame`, which run in the middle of `WorldGen` |
| `CornerElement`, `CustomFloatElement`, `GradientElement` | Use `ConfigElement`: the phone's config menu is its own UI |
| `ExampleReversedBarsDisplay` | The phone's HUD is another UI |
| `ExampleFullscreenUI`, `ShowFullscreenUICommand` | Depend on `UICycleImage` and on the PC layout |
| `ExampleInGameNotificationPlayer`, `ExampleJoinWorldInGameNotification` | Rarely used, with no equivalent on the phone |
| `ExampleResearchPresent` | Research hooks, rarely used |

Parts left out of files that are here: the frog statue and `CritterPrototypes`,
`PartyZombie`'s bestiary background, `SortAfterFirstRecipesOf`, `Item.master`,
`ModifyFrameMerge` in the conversion tiles, and the world list UI of
`ExampleWorldHeaderSystem` (the phone has no `UIWorldListItem`, so the system
only writes the header data).

`ExampleDye` uses one of the game's armor passes instead of its own `Effect`:
compiled `.fx` files from the PC do not load on Unity.
