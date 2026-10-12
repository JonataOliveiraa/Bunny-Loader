# 10. Sounds and music

A mod can bring its own sounds and music: a weapon's shot, an NPC's scream, a
boss's music. The approach is tModLoader's: `SoundStyle` for sounds,
`SoundEngine.PlaySound` to play one on the spot and `MusicLoader` with
`ModNPC.Music` for music.

Prerequisite: the [items](05-itens.md) and [NPCs](07-npcs.md) guides.

## The files

Sound effects live in `content/Assets/Sounds/` and music in
`content/Assets/Music/`, as in tModLoader:

```
content/
  Assets/Sounds/Items/Guns/ExampleGun.ogg
  Assets/Music/Ropocalypse2.ogg
```

Formats: **OGG** (recommended), MP3 and WAV. The path goes **without the
extension**, starting from `Assets/`: `'Sounds/Items/Guns/ExampleGun'` is
`Assets/Sounds/Items/Guns/ExampleGun.ogg` (`'Items/Guns/ExampleGun'` and the
full path, with `Assets/`, also work).

## Item and NPC sounds

```js
SetDefaults() {
    // ...
    this.Item.UseSound = new SoundStyle('Sounds/Items/Guns/ExampleGun', {
        Volume: 0.9,
        PitchVariance: 0.2,
        MaxInstances: 3,
    });
}
```

The same goes for `this.NPC.HitSound` and `this.NPC.DeathSound`. The game
plays the sound when it would play its own: on item use, on hit, on death. The
volume drops with distance from the center of the screen and follows the
game's sound effects volume.

| Option | |
|---|---|
| `Volume` | 0 to 1. Default 1. |
| `Pitch` | Pitch, in octaves: 1 = one up, -1 = one down. Default 0. |
| `PitchVariance` | Random variation on each play, in octaves: 0.2 = up to 0.1 either way. |
| `MaxInstances` | How many of this one play together. Default 1; 0 = unlimited. |
| `SoundLimitBehavior` | Over the limit: `SoundLimitBehavior.ReplaceOldest` (default) cuts the oldest; `IgnoreNew` does not play the new one. |

The keys also work in lowercase (`volume`, `pitchVariance`...).

Creating the `SoundStyle` inside `SetDefaults` costs nothing: the same file
with the same options returns **the same** object, and the file is read only
once.

If the file does not exist, `SoundStyle` does not throw (that would leave the
item half-built): it stays silent and the log says which file was missing.

## Playing a sound on the spot

```js
SoundEngine.PlaySound(style);                  // no position: full volume
SoundEngine.PlaySound(style, npc.Center);      // fades with distance
SoundEngine.PlaySound(Terraria.ID.SoundID.Item1, player.Center);   // a vanilla one
```

With a mod sound, `PlaySound` returns the identifier of an **asynchronous
request**, or 0 if it was refused: file still loading, too far away, volume at
0, app paused, `IgnoreNew` limit or full queue. Android starts the effect off
the game thread. A positive identifier confirms it was accepted; playback can
still fail or the request can expire. With that identifier:

```js
const request = SoundEngine.PlaySound(style, player.Center);
SoundEngine.GetSoundState(request);   // check on a later update
SoundEngine.FindActiveSound(style);   // the newest one started, or 0
SoundEngine.StopSound(request);       // also cancels before it starts
```

| `GetSoundState(request)` | State |
|---|---|
| -1 | The backend failed to start it. |
| 0 | Finished, was replaced by the global limit or the identifier is no longer in the history. Also applies to 0. |
| 1 | Pending, including an Android call still in progress. |
| 2 | Started, with an estimated remaining duration. |
| 3 | Paused by the app lifecycle. |
| 4 | Cancelled by `StopSound`, `ReplaceOldest` or a pause before it started. |
| 5 | Dropped because it waited more than 50 ms before dispatch. |

Check the state during the mod's updates. Do not wait in a busy loop: the game
has to keep going. `FindActiveSound` returns 0 while the request is pending,
has failed or is paused. `MaxInstances` counts pending requests and started
sounds; `ReplaceOldest` cancels the previous one before dispatching the next.
Paused sounds keep their identifier and remaining duration.

The controller keeps up to 256 recent requests, preserving the ones still in
progress. SoundPool offers no position query and no per-stream end
notification: the end is estimated from the file's duration and the pitch.
The started state confirms Android's positive return, without measuring the
speaker's physical output. Identifiers belong to the current game session.

## Music

An NPC's music plays while it is near the screen, like a boss's:

```js
SetDefaults() {
    // ...
    this.Music = MusicLoader.GetMusicSlot('Music/Ropocalypse2');
}
```

- `MusicLoader.GetMusicSlot(path)` returns the track's number (after the
  game's), or 0 if the file does not exist. `GetMusicSlot(bl.mod, path)`,
  tModLoader's way, also works.
- `this.Music` also accepts a vanilla track: `Terraria.ID.MusicID.Boss2`. -1
  (the default) lets the game choose.
- It can change mid-fight, in `AI`: phase 2 with another track. Give it a
  value already in `SetDefaults`: that is where Bunny Loader sees that the NPC
  has music (an NPC that spawns with -1 and only gets music in `AI` stays
  without it, unless another mod NPC has already turned mod music on).
- With two music NPCs nearby, the one with the higher `SceneEffectPriority`
  wins (`SceneEffectPriority.BossLow` is the default; they range from `None`
  to `BossHigh`).
- "Near" is the same as in tModLoader: up to 5000 px from the screen.
- A biome (`ModBiome`) or scene effect (`ModSceneEffect`) also has `Music` and
  `Priority`: the local player's applies, and it only replaces the NPC's if
  its priority is higher.
- The mod's wins over the game's if its priority reaches the game track's
  tier, as in tModLoader: `BossLow` beats a regular vanilla boss, but not the
  Moon Lord (`BossHigh`); `BiomeLow` beats the surface music, but not the slime
  rain (`Environment`). The full table is in the
  [reference](../referencia/classes.md#sound-and-music). A music box, vanilla or
  mod, beats everything.

The transition is the same as the game's between two of its own tracks: the
new one comes in quietly and rises, and the one that was playing only starts
fading out once the new one is audible — about 4 seconds, with no cut. That
applies when the boss arrives and when the fight ends (or when you leave the
world), when the game's track comes back the same way. The mod's track loops,
at the game's music volume.

`MusicLoader.MusicExists(path)` tells whether the file exists, and
`MusicLoader.IsMusicPlaying(slot)` whether the track is playing right now.

## How it works, and the limit

Terraria Mobile is Unity underneath, and **this build cannot create new
sounds**: `AudioClip.Create` and everything it uses were stripped from the
game at build time. So mod audio plays through **Android** (SoundPool for
sounds, MediaPlayer for music), and Bunny Loader makes it behave like the
game's: effects and music volume, distance and screen side, pausing when the
game goes to the background.

In practice:

- **Short sounds.** Effects are meant to be short, up to a few seconds. Long
  audio is music.
- Music is read bit by bit (streaming), so the file size does not weigh on
  memory.
- In multiplayer, each device plays its own audio, as the game does.
- There are up to 32 simultaneous custom streams, as before. The queue holds
  up to 32 pending requests, including the call in progress. Under overload,
  it refuses new requests and drops the ones that waited more than 50 ms
  before calling Android. This bounds memory and avoids a long string of late
  effects; it does not guarantee every sound in a burst plays.

## Differences from tModLoader

| tModLoader | Bunny Loader |
|---|---|
| `new SoundStyle("ExampleMod/Assets/Sounds/X") { Volume = 0.9f }` | `new SoundStyle('Sounds/X', { Volume: 0.9 })`, starting from `Assets/`, without the mod name |
| `SoundStyle` is a struct | `new` returns a game `LegacySoundStyle` (the `UseSound` type): `instanceof SoundStyle` is `false` |
| `SoundEngine.PlaySound` returns a `SlotId` | returns the asynchronous request's identifier (0 = refused); check `GetSoundState` |
| `Variants`, `IsLooped`, update callback | not yet |
| `MusicLoader.GetMusicSlot(Mod, path)` | also `GetMusicSlot(path)`; there is no automatic loading of `Assets/Music/` |
| `ModBiome.Music`, `ModSceneEffect.Music` | the same: the local player's scene, by `Priority` |

## Quick reference

| | |
|---|---|
| `new SoundStyle(path, { Volume, Pitch, PitchVariance, MaxInstances, SoundLimitBehavior })` | a mod sound; goes in `UseSound`, `HitSound`, `DeathSound` |
| `SoundEngine.PlaySound(style, position?)` | requests an effect; a mod sound returns the identifier (0 = refused) |
| `SoundEngine.GetSoundState(request)` | checks whether it is pending, started, paused, cancelled, expired or failed |
| `SoundEngine.FindActiveSound(style)`, `SoundEngine.StopSound(n)` | what is still playing; stop |
| `MusicLoader.GetMusicSlot(path)` | a mod track's number (0 = does not exist) |
| `this.Music`, `this.SceneEffectPriority` (on `ModNPC`) | the music while it is nearby; who wins |
| `MusicLoader.IsMusicPlaying(slot)`, `MusicLoader.MusicExists(path)` | is it playing? does the file exist? |

The `tools/tests/sounds` and `tools/tests/music` tests cover every case in
this guide; the second one uses the Example Mod's boss.
