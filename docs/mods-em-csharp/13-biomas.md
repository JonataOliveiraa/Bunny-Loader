# Biomes and scene effects

A `ModBiome` is a place the player can be in; a `ModSceneEffect` is anything
that can take over the scene: music, water, surface and underground
backgrounds and the map background. Both are `ModType`s: declare the class
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
- `WaterStyle`, `SurfaceBackgroundStyle` and `UndergroundBackgroundStyle`:
  the instance of a `ModWaterStyle`, `ModSurfaceBackgroundStyle` or
  `ModUndergroundBackgroundStyle` (`null` leaves them to others).
- `BiomeTorchItemType` and `BiomeCampfireItemType` (biomes only).
- `MapBackground`: a texture path for the fullscreen map.

`player.CurrentSceneEffect` (a `SceneEffectLoader.SceneEffectInstance`) holds
the result: `anyActive`, `music.value`, `music.priority`, `waterStyle`,
`surfaceBackground`, `undergroundBackground`, `mapBackground` and the others.

```csharp
public class ExampleSurfaceBiome : ModBiome
{
    public override ModWaterStyle WaterStyle => ModContent.GetInstance<ExampleWaterStyle>();
    public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle => ModContent.GetInstance<ExampleSurfaceBackgroundStyle>();
    public override string? MapBackground => BackgroundPath;
}
```

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

## Water and waterfalls

A `ModWaterStyle` takes the slot after the game's 15 (`WaterStylesLoader`);
its textures are `Texture` (the liquid), `BlockTexture` (`Texture + "_Block"`)
and `SlopeTexture` (`Texture + "_Slope"`). A missing one falls back to the
forest water.

```csharp
public class ExampleWaterStyle : ModWaterStyle
{
    public override int ChooseWaterfallStyle() => ModContent.GetInstance<ExampleWaterfallStyle>().Slot;
    public override int GetSplashDust() => ModContent.DustType<ExampleSolutionDust>();
    public override int GetDropletGore() => ModContent.GoreType<ExampleDroplet>();
    public override Asset<Texture2D> GetRainTexture() => rainTexture;
}
```

The scene's water replaces the game's (`Main.CalculateWaterStyle`) with
priority `BiomeLow` or higher, as in tModLoader: a water fountain, a Blood
Moon night and the game's own biome backgrounds (corruption, desert, jungle,
ocean, hallow, snow, crimson and the desert variants) keep theirs. The game
fades the water in and out itself (its loops in `Main.DrawWaters` are made
to reach the mod styles). With a mod water:

- `GetSplashDust` is the splash (`Dust.dustWater`);
- `GetDropletGore` is the drip of water drip tiles; a mod gore is spawned as
  the game's drip and switched, so give it `UpdateType = GoreID.WaterDrip`;
- `GetRainTexture` and `GetRainVariant` draw the rain (the variant is the
  column of the texture, 4 px wide);
- `LightColorMultiplier` is how light goes through the water (times 0.91, as
  in tModLoader);
- `BiomeHairColor` is the color of the Biome Hair Dye.

A `ModWaterfallStyle` (slot after the game's 28, `WaterFallStylesLoader`)
is drawn by the game for every visible mod water, through
`ChooseWaterfallStyle`. `ColorMultiplier(ref r, ref g, ref b, a)` gets the
color of each piece (0 to 255) and `AddLight(i, j)` runs every 3 tiles of the
water waterfalls on screen.

## Backgrounds

Every PNG in a `Backgrounds/` folder of the mod gets a background slot after
the game's (`Main.maxBackgrounds`); `BackgroundTextureLoader.AddBackgroundTexture`
adds one from elsewhere while the mod loads, and `BackgroundAutoloadingEnabled
= false` on the `Mod` turns the folder off. The key is the path without the
extension, with the mod's name in front:

```csharp
BackgroundTextureLoader.GetBackgroundSlot(Mod, "Assets/Textures/Backgrounds/ExampleBiomeSurfaceFar");
BackgroundTextureLoader.GetBackgroundSlot("ExampleMod/Assets/Textures/Backgrounds/ExampleBiomeSurfaceMid3");
```

`GetBackgroundSlot` throws for a missing one; `TryGetBackgroundSlot` does not.
`TextureAssets.Background[slot]`, `Main.backgroundWidth` and
`Main.backgroundHeight` cover the mod slots.

A `ModSurfaceBackgroundStyle` (slot after the game's 16) wins over the game's
by priority, as in tModLoader: `BiomeHigh` over everything, `BiomeMedium`
over the forest, the jungle and the snow, `BiomeLow` over the forest only.
`ModifyFarFades(fades, transitionSpeed)` moves the layers' alphas (your
`Slot` up, the others down); `ChooseFarTexture` and `ChooseMiddleTexture` are
drawn with the position and scale of the game's mountains, and
`ChooseCloseTexture(ref scale, ref parallax, ref a, ref b)` right after the
game's background trees; `PreDrawCloseBackground` returning false skips it.

A `ModUndergroundBackgroundStyle` (slot after the game's 22) fills four
textures in `FillTextureArray`: 0 the border of sky and ground, 1 the ground
layer, 2 the border of ground and rock, 3 the rock layer. `BiomeLow` replaces
the plain cave only, `BiomeMedium` the snow and the jungle as well,
`BiomeHigh` everything. The phone's old background drawing (backgrounds off
in the settings) does not use the textures.

A `GlobalBackgroundStyle` can change either choice
(`ChooseSurfaceBackgroundStyle`, `ChooseUndergroundBackgroundStyle`), the
underground textures of any style (`FillUndergroundTextureArray`) and the
surface fades (`ModifyFarSurfaceFades`).

## Map background

`MapBackground` is a texture path with the mod's name in front (`BackgroundPath`
fits). It covers the fullscreen map's background with the sky color on the
surface and white below; `MapBackgroundFullbright` makes it always white and
`MapBackgroundColor(ref color)` sets the color.

## Clouds

Every PNG in a `Clouds/` folder of the mod becomes a common cloud with spawn
chance 1 (`CloudAutoloadingEnabled = false` on the `Mod` turns it off). A
`ModCloud` class gives a cloud its own behavior, and
`CloudLoader.AddCloudFromTexture(mod, "ExampleMod/Content/Clouds/ExampleCloud", spawnChance: 0.1f, rareCloud: false)`
sets the chance and the pool; call it from an `ICustomAutoload`, which runs
before the folder is scanned, so the same PNG is not added twice:

```csharp
public class DefaultCloudsLoader : ICustomAutoload
{
    public static void Autoload(Mod mod)
    {
        CloudLoader.AddCloudFromTexture(mod, "ExampleMod/Content/Clouds/ExampleCloud", spawnChance: 0.1f, rareCloud: false);
    }
}
```

Types come after the game's (`CloudID.Count`). `SpawnChance()` is the weight
against the game's 22 common clouds (each weight 1), or against its rare ones
(18, 15 in a tenth anniversary world) when `RareCloud` is true; `OnSpawn` runs
when the cloud appears. `Draw(drawDataCache, cloud, cloudIndex, ref drawData)`
gets the cloud's draw data: change it, add more to the list (drawn before the
cloud) or return false to hide it. An unchanged cloud is drawn by the game as
usual; a changed one and the extra ones go to the game's cloud batch.

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

The `csbiome` pack checks biomes and scenes on one device, `csmusic` the music
`csbackground` the water, waterfalls, backgrounds and map background, and
`csclouds` the clouds.
