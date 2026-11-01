# Biomes and scene effects

A `ModBiome` is a place the player can be in; a `ModSceneEffect` is anything
that can take over the scene (music now; backgrounds, water and the map
background come in the next steps). Both are `ModType`s: declare the class
and it registers itself.

```csharp
public class ExampleSurfaceBiome : ModBiome
{
    public override bool IsBiomeActive(Player player)
    {
        bool b1 = ModContent.GetInstance<ExampleBiomeTileCount>().exampleBlockCount >= 40;
        bool b2 = Math.Abs(player.position.ToTileCoordinates().X - Main.maxTilesX / 2) < Main.maxTilesX / 6;
        bool b3 = player.ZoneSkyHeight || player.ZoneOverworldHeight;
        return b1 && b2 && b3;
    }

    public override SceneEffectPriority Priority => SceneEffectPriority.BiomeLow;
}
```

The tile count comes from `ModSystem.TileCountsAvailable` (see
[Systems](11-sistemas.md#time-and-nearby-tiles)).

## When the hooks run

After the game's `Player.UpdateBiomes`, on every update of the local player
(the game only calls it for the local player, also while dead):

1. `IsBiomeActive(player)` for every biome. A biome that throws counts as
   inactive this update.
2. `OnEnter(player)` when it turns on, `OnLeave(player)` when it turns off,
   `OnInBiome(player)` on every update inside (the entering one too).
3. The scene: `IsSceneEffectActive(player)` of every effect (for a biome, the
   result of step 1) and `SpecialVisuals(player, isActive)` for every effect,
   active or not. That is where a screen filter goes on and off.

`player.InModBiome<T>()` and `player.InModBiome(ModContent.GetInstance<T>())`
read the result. Leaving the world calls `OnLeave` for the biomes the local
player was in and clears every flag.

## Choosing the scene

Active effects are ranked by `Priority` plus `GetWeight(player)` (0 to 1,
default 0.5). Each channel comes from the first effect in that order that
sets it. Today the channels are:

- `Music`: a music id; `-1` leaves the choice to others. A `ModBiome` defaults
  to `0`, silence, as in tModLoader; return `-1` to keep the game's music.
- `BiomeTorchItemType` and `BiomeCampfireItemType` (biomes only).
- `MapBackground`: a texture path, kept for the map step.

`player.CurrentSceneEffect` (a `SceneEffectLoader.SceneEffectInstance`) holds
the result: `anyActive`, `music.value`, `music.priority` and the others.

Music is applied (next section). Water styles, surface and underground
backgrounds and the map background are the next steps of phase 6:
`ModWaterStyle`, `ModSurfaceBackgroundStyle` and
`ModUndergroundBackgroundStyle` overrides do not exist yet.

## Music

Put the file in the mod (`.ogg`, `.wav` or `.mp3`) and ask for its slot:

```csharp
public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Music/MysteriousMystery");
```

The path is relative to the mod's folder, with or without the extension;
`MusicLoader.GetMusicSlot("ExampleMod/Assets/Music/MysteriousMystery")` works
too. Mod slots start at `MusicID.Count` (105 on the phone); a missing file
gives 0 and a warning in the log. The slot is looked up once, so the getter
can call it every frame. `MusicLoader.MusicExists`, `AddMusic`, `MusicCount`
and `IsMusicPlaying(slot)` are there as well.

A `ModNPC` can set `Music` and `SceneEffectPriority` (default `BossLow`) in
`SetDefaults`: the NPC with the highest priority within 5000 px of the screen
is a candidate, and the scene replaces it only with a higher priority, as in
tModLoader.

Who wins: a music box on screen wins over everything. Otherwise the candidate
plays when its priority reaches the tier of the music the game picked (bosses
`BossLow` to `BossHigh`, events `Event`, eclipse, town and underworld
`Environment`, dungeon or corruption `BiomeHigh`, jungle or snow
`BiomeMedium`, the rest `BiomeLow`); `None` never plays. The choice is made
every 10 frames.

The game's own tracks are played by the game. Mod tracks are streamed by
Android's media player next to the game: the new one fades in, and the game's
music fades out once the mod track is heard, then comes back the same way.
Mod music boxes and the title menu's music are not there yet.

## Names and town NPCs

`DisplayName` and `TownNPCDialogueName` come from the `Biomes` section of the
mod's hjson; without them, tModLoader's defaults ("Example Surface Biome" and
"the Example Surface Biome"):

```hjson
Biomes: {
	ExampleSurfaceBiome: {
		DisplayName: Example Surface
		TownNPCDialogueName: the Example Surface Biome
	}
}
```

A town NPC can like or hate a mod biome as any other:

```csharp
NPC.Happiness.SetBiomeAffection<ExampleSurfaceBiome>(AffectionLevel.Love);
```

The phone's happiness table only holds the game's biome classes, so each mod
biome gets a stand-in shopping biome that answers with the player's flag; the
town NPC dialogue names the biome with `TownNPCDialogueName`.
`BestiaryIcon`, `BackgroundPath` and `BackgroundColor` exist (by default the
class path plus `_Icon` and `_Background`); the Bestiary entry comes later.

## Multiplayer

As in tModLoader, each device computes its own player's biomes. When they
change, the client sends the active ones (by mod and class name) to the
server, which keeps them (NPC spawning runs there and reads `InModBiome`)
and passes them on; a player joining gets everyone's. Receivers only store
the flags: `OnEnter`/`OnLeave` run on the player's own device. Two devices
have not been tested yet.

The `csbiome` pack checks all of the above on one device.
