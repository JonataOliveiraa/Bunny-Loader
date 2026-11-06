# Final plan: closing the ExampleMod port, town attack drawing and ModInvasion

Written 2026-11-05 from `eb917489` plus the uncommitted music fix. Runs on the
user's phone and on MuMu, without subagents. This plan runs first. Multiplayer is
in its own plan, [plano-multijogador.md](plano-multijogador.md), which runs after
it, over the finished content.

This replaces the file list in `examplemod-tml-split.md`, whose "268 missing
files" and some of the missing-hook notes in `plano-csharp.md` are out of date.

## 1. Where the port stands

Measured, not estimated:

| | Files |
|---|---|
| tModLoader `ExampleMod`, `.cs` files | 563 |
| In `Old/`, excluded from tML's own build (`Compile Remove="Old\**"`) | 112 |
| **Active** | **451** |
| Already in `samples/ExampleModCSharp` | 393 (87%) |
| **Missing** | **58** |

Each missing file was compiled against the current `BunnyLoader.dll`, removing
files with errors and repeating until none were left (5 passes, needed because
Roslyn stops at declaration errors). 11 compile as they are, 47 show a gap.
Compiling proves the API exists, not that it works on the phone. Fixing a file's
declaration errors can reveal more errors in its body.

The 58 split like this:

| Group | Files | Decision |
|---|---|---|
| E1, compiles today | 10 | Port |
| E2, needs the small API fixes of block A | 16 | Port |
| E3, needs native or new game work | 19 | Port |
| `ExampleAccessorySlot` | 1 | Not for now (D2) |
| Out | 12 | Listed in section 7 |

## 2. Order of work

1. Close the Music Box fix, committed and shipped in 0.5.1-beta: run `csmusicbox`
   and `csmusic` on the phone with that APK.
2. Block A, API fixes.
3. Block B, missing hooks.
4. Block C, town attack drawing.
5. Block E1 and E2, examples.
6. Block D, ModInvasion.
7. Block E3, examples that need native work.
8. Block F, audit of API that exists without effect.
9. Block G, closing documents.

The multiplayer plan runs after this one, so a network bug can come back as rework
in a finished block.

Every block ends with a `csex*` or `cs*` pack passing on MuMu and on the phone, and
the example in the sample with textures and en-US/pt-BR text. A block with network
code also runs on two MuMus with `tools/tests/run-mp.sh` (`BL_PKG=com.bunnyloader.beta`,
checking the ports first), mainly `ModInvasion`, tile entities and anything that
sends packets. A DLL-only test ends with an APK equal to the tested code before the
phone is called ready.

## 3. Block A: API fixes

Small, and each one blocks real tModLoader code from compiling.

| Item | Error today | Work |
|---|---|---|
| `Color == Color`, `!=` | CS0019 | Operators on the proxy struct |
| `ItemCreationContext` | CS0104, in both `Terraria.DataStructures` and `Terraria.ModLoader` | Keep tML's location, remove the other |
| `Tile.HasTile` setter | CS0200 | Add it |
| `Tile.BottomSlope` | CS1061 | Add it |
| `ItemHoldStyleID` | CS0103, exists in the game | Add to `types.txt` |
| `ItemUseStyleID.Search` | CS0117 | Check against the dump, then add or leave out |
| `PreDrawInWorld`, `PostDrawInWorld` with `WorldItem` | CS0115 | Match tML's signature (breaking for our sample) |
| `[ReinitializeDuringResizeArrays]` | CS0246 | Honour it with the existing set factory |
| `Recipe.AddCustomShimmerResult` returning `Recipe` | CS1061, the game's method returns `Item`, so chaining fails | Wrap it, as tML does; the phone's shimmer already reads `customShimmerResults` |
| `PosData<T>` | CS0246 | Port as plain C# |
| `ItemDefinition` and family (`NPC`, `Projectile`, `Buff`, `Tile`, `Wall`, `Prefix`) | CS0246 | Port `EntityDefinition`, with a row in the config menu |
| `DefaultDictionaryKeyValue`, `ToFromStringConverter<T>` | CS0246 | Port |
| `Newtonsoft.Json` attributes | CS0246, 6 ExampleMod files | Attributes only, honoured by `ConfigManager` (D1) |

### `netMode` on the phone's host

On the phone the host's `Main.netMode` is 3, so `Main.netMode == NetmodeID.Server`
is false there (D6 in the multiplayer plan). It comes here, ahead of that plan,
because every block below writes code that depends on it:

1. Warn at compile time in `ModCompiler`, suggesting
   `(Main.netMode & NetmodeID.Server) != 0`.
2. Review the 14 such tests in `samples/ExampleModCSharp` and fix the ones meant as
   "I am the server".
3. Write the rule in the guide next to `NetmodeID`.

Measuring what really breaks on a host stays in group M12.

## 4. Block B: missing hooks

`virtual` members in tML with no equivalent anywhere in our API, by name:

| Hooks | Decision |
|---|---|
| `ModItem.OnCraft`; `ModItem`/`GlobalItem`: `CanReforge`, `PreReforge`, `PostReforge`, `ReforgePrice`, `HoldoutOrigin`, `CaughtFishStack`, `ApplyPrefix`; `ModItem.AutoDefaults` | Done. tML has no `GlobalItem.OnCraft`. Reforging on the phone resets the item with `netDefaults`, which also lost mod instance data; `ItemReforgeHooks` keeps it |
| `ModPlayer.SetControls` | Done, right after `ProcessTriggers`, because the method body cannot be hooked in the middle |
| `ModSystem.PostWorldLoad` | Done |
| `GlobalWall` (whole class), `ModWall.OnWallConverted` | Done. `PreDraw`, `PostDraw`, `CanPlace` and `CanExplode` of `GlobalWall` exist for tML source compatibility but have no effect yet, see block F |
| `ModItem`/`GlobalItem`: `ModifyItemDraw`, `PreModifyItemDraw`, `PostModifyItemDraw` | Done, by reading back what `DrawPlayer_27_HeldItem` added |
| `ModPylon`: `ModifyTeleportationPosition`, `ValidTeleportCheck_DestinationPostCheck`, `ValidTeleportCheck_NearbyPostCheck`; `GlobalPylon` | Done, through a C# port of `HandleTeleportRequest`, as the phone inlines the checks |
| `ModNPC`/`GlobalNPC.ModifyNPCHappiness` | Done on `AllPersonalitiesModifier.ModifyShopPrice`. The call is commented out in tML itself. `primaryPlayerBiome` is always 0: the phone's `HelperInfo` has no such field |
| `ReplaceTile` | Done |
| `FlailStats` | Does not exist in tML, dropped from this table |
| `FlailSpinCollisionRange`, `WingGlidingSpeeds`, `AutoSelect`, `PreDrawPlacementPreview`, `PostDrawPlacementPreview`, `PreDrawInEmoteMenu`, `PostDrawInEmoteMenu` | Out: all are in the middle of game methods (`AI_015_Flails`, `Player.Update`, `SmartSelectLookup`, `TileObject.DrawPreview`), or in a UI the phone does not have |
| `CanResearch`, `OnResearched` | Out, with `ExampleResearchPresent` |
| `ModifyFrameMerge`, `PostTileFrame`, `PostSetupTileMerge`, `ChangeWaterfallStyle`, `IsTileBiomeSightable`, `RequiresScreenTarget` | Out: mid-method in `WorldGen.TileFrame` or tied to PC rendering |

## 5. Block C: `DrawTownAttackGun` and `DrawTownAttackSwing`

These are not missing. They exist since `39f90c17`:

- `ModNPC`, `GlobalNPC` and `NPCLoader` have both, with tML's signatures.
- `content/npcs/NpcTownHooks.cpp` hooks the middle of `Main.DrawNPCExtras` at
  `+0xf94` (gun) and `+0x1ccc` (swing), checking the instruction bytes first.
- `ExampleBoneMerchant` overrides the gun, `ExampleTravelingMerchant` the swing.

Done in `cstownattack`, with the phone's captures as proof (decision D4):

1. A mod NPC of attack type 1 and of type 3, and a `GlobalNPC` on the vanilla Arms
   Dealer and Dye Trader, each replacing the texture and the scale. The plan said
   Nurse, but the Nurse throws: the vanilla swing types are 207, 353 and 441.
2. The suspicion that `GlobalNPC` never runs on a vanilla NPC was wrong. Its flags
   reach the native hook as "all types", and the test measures it. No change needed.
3. `ExampleBoneMerchant` and `ExampleTravelingMerchant` are captured in the same run.

On MuMu the callbacks run but the new NPCs do not show in the captures, unlike on the
phone. The cause was not found, so the phone is the reference for this block.

## 6. Block D: `ModInvasion`

tModLoader has no such class. Animatak_ wrote one for the JS API, in
`C:\Users\nadek\Downloads\ExampleInvasion` (820 lines). It is the reference for
the design, rewritten in C#.

### API

Lives in `Terraria.ModLoader`, next to `ModKeybind` and `ModTouchButton`, which
are also ours.

```csharp
public abstract class ModInvasion : ModType
{
    public int Type { get; }
    public virtual LocalizedText DisplayName { get; }
    public virtual string IconTexture { get; }
    public virtual Color MessageColor { get; }
    public virtual int Music { get; }

    public abstract IList<InvasionWave> Waves { get; }
    public virtual int SpawnRate { get; }
    public virtual int MaxSpawns { get; }

    public virtual bool CanStart();
    public virtual bool CanSpawn(Player player);
    public virtual bool ShouldFail();
    public virtual int PointsFor(NPC npc, InvasionWave wave);
    public virtual int ScalePoints(int points, int players);

    public virtual void OnStart();
    public virtual void OnWaveStart(int wave);
    public virtual void OnWaveComplete(int wave);
    public virtual void OnComplete();
    public virtual void OnFail();
    public virtual void Update();
}

public sealed record InvasionWave(int Points, params InvasionEnemy[] Enemies);
public readonly record struct InvasionEnemy(int Type, float Weight = 1, int Points = 1);

public static class InvasionLoader
{
    public static ModInvasion Active { get; }
    public static int Wave { get; }
    public static int Progress { get; }
    public static int ProgressMax { get; }
    public static bool CanStart(ModInvasion invasion);
    public static bool Start(ModInvasion invasion);
    public static void Stop(bool success);
    public static void AddPoints(int points);
    public static bool IsDowned(ModInvasion invasion);
}
```

`Start` from a client sends a request and returns `true`; the server decides, as
in the JS version. `Register` is automatic, through `ModType`. The JS
`DataInvasion` and the `Mod.Call` table are not ported: C# mods reference
the type directly.

### Taken from the JS version

- Waves of points, scaled +50% per extra player, with kills counted in
  `GlobalNPC.OnKill` on the server, ignoring statues and friendly NPCs.
- Spawning through `GlobalNPC.EditSpawnRate` and `EditSpawnPool`.
- The game's progress bar: `Main.invasionProgress`, `invasionProgressMax`,
  `invasionProgressWave`, `invasionWaveMax`, `invasionProgressIcon`,
  `invasionProgressDisplayLeft`, and a hook on `GUIEvents.DrawInvasionInfo` for
  the title and icon. All of these are in the phone's dump.
- `(netMode & 2) != 0` for server and `netMode == 1` for client, so the host,
  whose `netMode` is 3, counts as both.
- Saving the active invasion and the downed list in `SaveWorldData`.

### Changed from the JS version

| JS | C# | Why |
|---|---|---|
| State sent every 120 ticks | `ModSystem.NetSend`/`NetReceive` on join, plus a packet on each change | A late joiner has the state at once |
| Own chat packet | `ChatHelper.BroadcastChatMessage` with `NetworkText.FromKey` | The game's way, translated on each client |
| Only refuses to start during a vanilla invasion | Also blocks `Main.StartInvasion`, moons and the Old One's Army starting during a mod invasion | The two bars would fight |
| Texture loaded by hand | `ModContent.Request<Texture2D>` | API like the rest |
| No music | `Music`, through `MusicHooks` | Invasions usually have it |

### Example and tests

- Port `PaladinCrusade`, `GraveLegion`, `ConsecratedAxe` and `GraveBell` to
  `samples/ExampleModCSharp`, with Animatak_'s icons and text and credit to
  Animatak_, who asked the user to use them (D5).
- `csinvasion` alone: start refused by `CanStart`, spawn pool replaced, points,
  wave change, bar values, success, failure at dawn, save and reload mid-wave,
  downed flag, block against a vanilla invasion.
- `csinvasionmp` on two MuMus with `run-mp.sh`, written so that group M9 of the
  multiplayer plan can reuse it on the phone.
- New guide page, as tML has no documentation for this.

## 7. Examples

### E1, compile today (10)

`ActiveSoundShowcase`, `ExampleMultiUseItem`, `PlanteraItem`, `ExampleBasicFish`,
`ExampleDataItem`, `ExamplePaperAirplane` (and drop the lookup by name in
`ExamplePaperAirplaneProjectile`), `ComplexData`, `Pair`, `SimpleData`,
`ModConfigShowcaseLabels`.

### E2, after block A (16)

`ExampleCanStackItem`, `ExampleStackableDurabilityItem`, `ExampleFlask`,
`CustomItemSets`, `CustomItemDrawingShowcase`, `HoldStyleShowcase`,
`UseStyleShowcase`, `ShimmerShowcase`, `ExampleVine`, `ClassUsedAsKey`,
`Gradient`, `SimpleDataAtParticularLocations`, and the showcases
`ModConfigShowcaseAccessibility`, `DataTypes`, `DefaultValues`, `Subpages`.

### E3, native or game work (19)

| Files | Risk to check first |
|---|---|
| Cactus: `ExampleCactus` and `ModCactus` (D3) | The largest risk in this table: tML's cactus relies on checks in the middle of `WorldGen`, and the phone only takes hooks at the start and end of those methods. Read tML's `ModCactus` and the phone's growth, placement and drawing code first. If a check can't be reached, use a native stage hook or a C# port of the method |
| Sand: `ExampleSand`, `ExampleSandBlock`, `ExampleSandBallProjectile`, `ExampleSandRod` | `TileID.Sets.FallingBlockProjectile`, `ProjectileID.Sets.FallingBlockTileItem` and `ItemID.Sets.SandgunAmmoProjectileData` are not in the phone's game. They need our own tables and a way into the falling block code |
| Solutions: `ExampleSolution`, `ExampleHellSolution` | Depend on sand |
| Dyes: `ExampleDye`, `ExampleHairDye` | `BindShader` is a generic method in the dump, and `ExampleDye` loads its own shader. Decide whether a mod `Effect` loads on Unity, or fall back to a vanilla pass |
| Pressure plate: tile and item | `PressurePlateHelper` exists; `TileID.Sets.PressurePlate` does not |
| Advanced pylon: `ExampleGlobalPylon`, `ExamplePylonTileAdvanced`, `ExamplePylonItemAdvanced`, `AdvancedPylonTileEntity` | Block B hooks; the phone's teleport walks `_pylons` |
| Conversion: `ExampleVanillaConversionTiles`, `ExampleVanillaConversionWalls` | Port without `ModifyFrameMerge`, accepting plain merging |
| `ExampleGlobalWall` | None |
| `ExampleWorldHeaderSystem` | Data only: no `UIWorldListItem` on the phone |

### Not for now: `ExampleAccessorySlot` (D2)

`ModAccessorySlot` is not implemented for now: the phone's inventory screen is
complex and compact, so where extra slots would go is an open design question.

### Out (12), with the reason written in the sample's README

| Files | Why |
|---|---|
| `ExamplePalmTree` | Checks in the middle of `WorldGen`. Revisit if a biome mod asks |
| `ExampleWorldGenHookingSystem` | `ILContext`: IL edits cannot exist on IL2CPP. `ModifyWorldGenTasks` covers replacing a pass |
| `ExampleCustomFramingTile` | `ModifyFrameMerge`, `PostTileFrame` |
| `CornerElement`, `CustomFloatElement`, `GradientElement` | `ConfigElement`: the phone's config menu is its own UI |
| `ExampleReversedBarsDisplay` | The phone's HUD is another UI |
| `ExampleFullscreenUI`, `ShowFullscreenUICommand` | Depends on `UICycleImage` and PC layout |
| `ExampleInGameNotificationPlayer`, `ExampleJoinWorldInGameNotification` | Rarely used, no equivalent on the phone |
| `ExampleResearchPresent` | Research hooks, rarely used |

Also out from earlier blocks: frog statue and `CritterPrototypes`,
`PartyZombie`'s bestiary background, `SortAfterFirstRecipesOf`, `Item.master`.

## 8. Block F: API that exists without effect

A mod that uses these compiles and silently does nothing, which is worse than a
compile error. For each one: make it work, or mark it `[Obsolete]` with the
reason so the modder sees a warning.

- `ActsLikeTownNPC`, `TownNPCStayingHomeless` and related, known from earlier
  blocks.
- Nothing in our code calls these, from a name search that can miss indirect
  calls, so read each one: `ModProjectile.GlowTexture`,
  `ModTile.ModifySmartInteractCoords`, `ModHair.AltTexture`,
  `ModHair.RandomizedCharacterCreationGender`.

## 9. Block G: closing

- Bring `plano-csharp.md` up to date: its phase 8 checkboxes are behind the code.
- Guides: 05 (LINQ), 19 (joystick), a new one for `ModInvasion`.
- The remaining phase 8 items stay separate from this plan: the other samples in
  C# (`DobroDeDano`, `HelloMod`, `SemQueda`, `TemploPiramide`, `VidaCheia`) and
  the catalog, which stays off until mods are ported.

## 10. Decisions

All answered on 2026-11-05.

| # | Question | Answer |
|---|---|---|
| D1 | `Newtonsoft.Json`: ship the library, or only the attributes? | Attributes in the `Newtonsoft.Json` namespace, honoured by `ConfigManager`. Left to the implementer by the user. Ship the library when a real mod needs `JObject` |
| D2 | `ModAccessorySlot` (21 members, tied to the phone's inventory UI): now or later? | Not for now: the phone's screen is complex and compact |
| D3 | Cactus and palm tree? | Port the cactus. The palm tree stays out |
| D4 | `DrawTownAttack*`: did the user see a specific failure? | The user cannot check it, so the plan's tests decide |
| D5 | Can Animatak_'s icons and text be reused? | Yes: Animatak_ asked the user to use them. Keep the credit |
