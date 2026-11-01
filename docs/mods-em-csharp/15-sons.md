# Sounds

Sounds are written as in tModLoader: a `SoundStyle` with the path of the file
(with the mod's name in front, without the extension) and its options.

```csharp
Item.UseSound = new SoundStyle($"{nameof(ExampleMod)}/Assets/Sounds/Items/Guns/ExampleGun")
{
    Volume = 0.9f,
    PitchVariance = 0.2f,
    MaxInstances = 3,
};
```

The file can be `.ogg`, `.wav` or `.mp3`. A missing file plays nothing and
leaves a warning in the log; nothing throws.

## SoundStyle

- `Volume` (0 to 1, default 1), `Pitch` (in octaves, -1 to 1), `PitchVariance`
  (a random spread around `Pitch`), or `PitchRange = (min, max)` for both.
- `MaxInstances` (default 1; 0 for no limit) and `SoundLimitBehavior`:
  `ReplaceOldest` (default) stops the oldest one, `IgnoreNew` skips the new one.
- `Type`: `SoundType.Sound` follows the game's sound volume,
  `SoundType.Ambient` the ambient volume and `SoundType.Music` the music volume.
- Variants: `new SoundStyle("ExampleMod/Assets/Sounds/Hit_", 3)` plays
  `Hit_1`, `Hit_2` or `Hit_3` at random; `(path, start, count)` and
  `(path, ReadOnlySpan<int> variants)` choose the suffixes.
- `WithVolumeScale(scale)` and `WithPitchOffset(offset)` give a copy, and so
  does `with { ... }`.

The game's sounds stay as they are: `Item.UseSound = SoundID.Item1;`,
`NPC.HitSound = SoundID.NPCHit1;`.

## Playing

`Item.UseSound`, `NPC.HitSound`, `NPC.DeathSound` and `ModTile.HitSound` take a
mod `SoundStyle` and the game plays it at the right moment. To play one
yourself:

```csharp
SoundEngine.PlaySound(style, player.Center);
SoundEngine.PlaySound(style);
```

With a position the sound fades with the distance to the middle of the
screen and pans left or right, as the game's own; beyond the game's hearing
distance it is not played. Without a position it plays at full volume.
`PlaySound` returns a `SlotId` whose `Value` is 0 when the sound was not
played.

## Under the hood

This build of Unity cannot create new audio clips, so mod sounds are played
by Android's sound pool next to the game. A mod `SoundStyle` given to the game
(in `Item.UseSound` and the others) is a marker the game passes back when it
plays a sound, and the loader plays the file then. Not there yet: tracking a
playing sound (`ActiveSound`, `TryGetActiveSound`, update callbacks) and
looped sounds (`IsLooped`).

The `cssound` pack checks all of the above.
