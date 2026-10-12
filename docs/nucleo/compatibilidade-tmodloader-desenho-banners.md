# Item drawing, CloneDefaults and banners: tModLoader's contracts

Analysis and implementation from 2026-10-08. Pinned reference: tModLoader,
branch `1.4.5`, commit `39e7995fa519a94468ebaf4699508d25903e143e`. The
mobile binary checked is Terraria 1.4.5.8, Android version `301720`, ARM64.
Matching names does not guarantee matching behavior: you have to check who
calls each callback, the arguments, the order, the vetoes, the instances'
state and the packets' path.

## Analysis: inputs, outputs and invariants

| Flow | Input | Required result | Cases that need attention |
|---|---|---|---|
| Drawing in the world | `WorldItem`, light, alpha, rotation, scale and index | Callbacks at the right point of the routine; refs reach the native drawing | Several globals, veto, mod failure, recursive drawing, inactive item |
| Drawing in the inventory | `Item`, position, frame, colors, origin and scale computed by the game | A veto skips the vanilla body and keeps `PostDraw` and the returned scale | Animated frames, recomputed colors, per-entity filters, recursion |
| Copying defaults | Target NPC and source type | Copy classic stats, keep identity and apply difficulty once | Source from another mod, per-entity globals, reentry, circular source, no globals |
| Banner | NPC, representative, item and tile style | Kills, claiming and bonus use the same association | NPCs sharing a banner, missing mod, different ID order, saving versus sending over the network |

Struct layouts, signatures and the location of the native points were checked
in `refs/dump.cs` and `refs/libil2cpp.so`. Before installing each drawing
step, the code compares the expected instructions. The Node test simulates the
native calls: the stack/register contracts were verified separately on MuMu,
inside the game.

## Drawing: alternatives and choice

| Approach | Time | Memory | Maintenance and behavior |
|---|---|---|---|
| Rewrite the whole routine in JS | O(effects + subscribers), crossing the bridge on every operation | State and objects for every step | Would duplicate Terraria's special cases and change along with them |
| Intercept only `SpriteBatch.Draw` | O(draws), including uninteresting calls before the filter | Context to identify which draw belongs to the item | A veto would still run the particles, glowmasks and notifications before/after |
| Insert Pre/Post at the native routine's boundaries | O(subscribers) on marked items; vanilla body preserved | A 768-byte ARM64 frame per step and the callbacks' arguments | Requires checking instructions and register positions for each game version |

The implementation uses the third option. The per-version maintenance cost is
explicit, and the veto contract matches tModLoader's flow. The hooks are only
installed when there is an override; per-type marks avoid entering JS for
items with no callbacks. A GlobalItem widens the marks, and `AppliesToEntity`
selects the subscribers in the global registry.

Sources: [ItemLoader.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/ItemLoader.cs),
[Main.cs.patch](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/Main.cs.patch),
[ItemSlot.cs.patch](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/UI/ItemSlot.cs.patch).

| Callback | Order |
|---|---|
| `PreDrawInWorld` | ModItem, then every applicable GlobalItem |
| `PostDrawInWorld` | ModItem, then every applicable GlobalItem |
| `PreDrawInInventory` | Every applicable GlobalItem, then ModItem |
| `PostDrawInInventory` | ModItem, then every applicable GlobalItem |

Every applicable Pre is called, even after a veto. A `false` skips the
vanilla body up to the Post step. `PostDraw` still runs. The official names
are `PreDrawInWorld`, `PostDrawInWorld`, `PreDrawInInventory` and
`PostDrawInInventory`, without `Item` in the name.

In the world, Pre receives `Ref`s for rotation and scale; Post receives the
numbers. In the inventory, the callbacks' colors come from
`GetAlpha(itemLight)` and `GetColor(environmentColor)`. The scale returned by
`DrawItemIcon` remains the scale computed by the game, even with a veto.
Bunny keeps its convention of passing `item` explicitly to the ModItem in the
inventory; tModLoader makes it available through `this.Item`.

Implementation: `ItemDrawHooks.cpp`, `Arm64Frame.h` and `ItemDrawLoader.js`.
The steps are `Main.DrawItem + 0x418/+0x20a8` and
`ItemSlot.DrawItemIcon + 0x380/+0x1198`. The engine lock, the reentry guard
during callbacks and `finally` protect context and flags. Using refs does not
accept non-finite values when returning floats to native code.

## CloneDefaults: identity and difficulty

Source: [NPC.TML.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/NPC.TML.cs).

tModLoader saves `type`, `netID`, `ModNPC` and `_globals`, copies with classic
difficulty and scale 1 and restores the identity. The
`SetDefaultsKeepPlayerInteraction` helper also keeps the players'
participation in the kills. It is not just a call to `SetDefaults`.

In Bunny, `ModNPC.CloneDefaults(type)` does that preservation. The bridge
avoids reentering the hook that is already running; so, inside the target's
SetDefaults, the mod source and its globals need their defaults called
explicitly. The target's instance and globals are restored in `finally`, even
when the source fails. Circular sources are refused.

Difficulty uses `Terraria.DataStructures.GameDifficultyLevel.Classic`. In the
binary checked it is `1`; `0` does not mean classic. The native test caught
that distinction by comparing life, damage, defense and aiStyle with a
BlueSlime created by the game. The mod NPC's outer finalization applies the
world difficulty after the copy.

With no GlobalNPC registered, the auxiliary `__globalNPCs` field does not yet
exist in the bridge. Preserving it is conditional on the global registry being
created. That condition has its own test and was verified on the device.

## Banners: four relations, persistence and networking

Sources: [BannerSystem.cs.patch](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/GameContent/BannerSystem.cs.patch),
[NPCLoader.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/NPCLoader.cs),
[ModBannerTile.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/ModBannerTile.cs),
[TileObjectData.TML.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ObjectData/TileObjectData.TML.cs).

1. The NPC points to the banner ID in `Banner`. For mod content, that ID is
   the representative NPC's type.
2. The representative points to `BannerItem`. BannerSystem uses that item both
   for claiming and to get the bonus threshold and strength.
3. The item points to the type and style of the tile it places.
4. `ModBannerTile.NearbyEffects` maps style → item → banner and sets
   `SceneMetrics.NPCBannerBuff[banner]` when the strength is enabled.

Vanilla banner IDs are not NPCIDs. Bunny intercepts BannerSystem's
conversions, grows its three arrays and SceneMetrics', and keeps 1.4.5's
native flow for kills, notifications and claiming. New items get
`BannerStrength` enabled and `KillsToBanner = 50` before SetStaticDefaults;
the mod can change those values. NPCs of the same family can share the
representative and its counter.

The `GetTileStyle` helper was added by tModLoader and does not exist on
mobile. `TileLoader.StyleAt` uses the native metadata to compute the style,
including orientation, wrap, multiplier and rows reserved for alternates. The
previous path that called the missing method was replaced by the calculation
already used for framing objects.

For persistence there were two options: write dynamic indexes into the
vanilla file, or keep a stable identity in the sidecar. The second was chosen:
`BunnyLoader/Banners` is an internal ModSystem that stores count, claimable
amount and notification by mod UID/class name. Data from missing mods is kept.
The vanilla save receives arrays at their original size, and the live arrays
are restored in `finally`.

In the native `NetBannersModule.WriteFullState` packet, the arrays stay
extended. The ModSystem also takes part in the world sync. The native test
reads the serialized size in the packet; `NetPacket.Length` alone does not
prove the payload, since it initially represents capacity. The two-process
test confirmed the initial state, the 50th kill, claiming from the client,
refusal of a duplicate claim and reconnecting with 51 kills and zero pending
banners. Evidence: [PvP and networked banners](../../tools/tests/pendingnetwork/README.md).

`ModBannerTile.PreDraw` queues only the top-left cell as a `MultiTileVine` and
suppresses the normal drawing. Mobile already has `DrawMultiTileVinesInWind`,
but the vanilla size selection does not know mod types. Bunny's pass calls
that renderer with the TileObjectData's Width/Height. The native
`GetTileDrawData(Tile)` overload receives the drawing data through a C++ hook,
without trying to carry its 16 parameters across the JS bridge. The special
drawing does not reuse PreDraw's suppression cache. The alternate's offset is
read from the placed tile, and SetDrawPositions compensates for the
renderer's two pixels, as in tModLoader.

The in-game test turned on the sway option with wind 0.8, observed 120
renderer calls and 360 sprites with positive dimensions and varying rotation.
ExampleMod includes `ExampleEnemyBannerTile` and `ExampleSlimeBanner`, linked
to `ExampleSlimeNPC`.

## Assessment of the other requested methods

`GlobalNPC.SetupShop` already exists in Bunny as compatibility, along with
`ModifyShop` and `ModifyActiveShop`. In tModLoader 1.4.5, the central
configuration is done through `NPCShop`; carrying over only the legacy name
does not reproduce the active items' conditions and modifications.

The modern `HitModifiers/HitInfo` and `HurtModifiers/HurtInfo` contracts are
already present in CombatLoader, ModNPC, ModProjectile and the global classes.
The reference point is
[CombinedHooks.cs](https://github.com/tModLoader/tModLoader/blob/39e7995fa519a94468ebaf4699508d25903e143e/patches/tModLoader/Terraria/ModLoader/CombinedHooks.cs):
`ModNPC/GlobalNPC.ModifyIncomingHit` sets up the modifier first; then the
attacker, the target and ModPlayer modify it. In ModPlayer, the general
callback comes before the item/projectile-specific one, for both Modify and
OnHit. That order was fixed and has a composition test with operations that
produce different results if swapped. OnHit uses the damage returned by the
native routine.

For projectiles, the official contract is `CanHitPvp` followed by
`ModifyHitPlayer/OnHitPlayer`; there is no separate
`ModifyHitPvp/OnHitPvp` family on ModProjectile. For items, that family
exists.

Item PvP was fixed in a step of its own. The veto runs in
`Player.CanHit(Entity)` inside `ItemCheck_MeleeHitPVP`, before applying
status and sending damage, with the same `Main.player[i]` the loop passes to
`CanHit` as the target: the `whoAmI` of a player activated before their
`Update` is still 0 and cannot be used to find them. ModifyHitPvp/OnHitPvp run
in the victim's process. Mobile's PlayerDeathReason provides type and prefix;
an envelope on the same TCP stream, before message 117, carries the weapon's
stable identity and NetSend/NetReceive data. The server keeps that envelope
when relaying. The victim rebuilds the item even if the attacker has already
switched weapons. Bounded queues and context restored in finally avoid keeping
the origin of another hit. The [network fixture](../../tools/tests/pendingnetwork/README.md)
confirmed the veto, 7 damage and single callbacks in both directions, plus the
banner scenarios. The old failures remain in the historical reports.

## Reproducible validation

```powershell
node tools/tests/apiexpansion/check.mjs
python tools/tests/apiexpansion/verify_native.py
node tools/tests/npcaitype/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/moditemdata/check.mjs
node tools/tests/moditemhooks/check.mjs
node tools/tests/modnpchooks/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/pendingnetwork/check.mjs
```

The native fixture is in `tools/tests/apiexpansion/content/main.js`.
`device.py prepare/run/collect/restore` uses copies of the character and the
world, disables the other mods during the run and restores settings, mods and
powers with a byte-by-byte comparison. The consolidated result is in the
[test report](../../tools/tests/apiexpansion/README.md).

No comparable FPS measurement was made on the physical device the user
mentioned. This analysis does not prove that the EternalLaser problem was
solved nor that the cost is the same as TL Pro's.
