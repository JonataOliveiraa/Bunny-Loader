# 9. Tiles

A new block (dirt, stone, ore) is a class that extends `ModTile`, like
tModLoader's `ModTile`. This guide shows how to create the tile, the item that
places it, and how Bunny Loader saves the world so it keeps opening without
the mod.

Multi-cell objects (furniture) too: the shape comes from `TileObjectData`, as
in tModLoader ([Furniture](#furniture-tileobjectdata)), with what is specific to
each one: doors that open, chairs you sit on, chests, crafting stations
([Furniture with behavior](#furniture-with-behavior)).

Read the [ideas in guide 4](04-conteudo-novo.md) first. The full list is in
the [reference](../referencia/classes.md#modtile).

## The example ore

```js
export class ExampleOre extends ModTile {
    constructor() {
        super();
        this.DustType = Terraria.ID.DustID.Platinum;
        this.HitSound = Terraria.ID.SoundID.Tink;
        this.MineResist = 4;     // 4 times as many hits
        this.MinPick = 200;      // a pickaxe below 200 cannot break it
    }

    SetStaticDefaults() {
        Terraria.Main.tileSolid[this.Type] = true;
        Terraria.Main.tileBlockLight[this.Type] = true;
        Terraria.Main.tileMergeDirt[this.Type] = true;
        Terraria.Main.tileMerge[this.Type][this.Type] = true;
        Terraria.Main.tileSpelunker[this.Type] = true;
        Terraria.ID.TileID.Sets.Ore[this.Type] = true;
        this.AddMapEntry(Color.new(152, 171, 198), this.constructor.name);
    }
}
```

`SetStaticDefaults` holds the game tables that say what the tile **is**:
solid, whether it blocks light, whether it merges with dirt, whether the
Spelunker highlights it.

### The texture

The tile's frame sheet, in the same format as the game's: **288x270 px**,
16x16 frames with a 2 px margin. The game picks the frame from the neighbors
(and from dirt, with `tileMergeDirt`): a lone block, a corner, an edge.
Copying the sheet of a similar vanilla tile and repainting it is the shortest
path.

### The item that places it

It is a plain `ModItem`:

```js
SetDefaults() {
    this.DefaultToPlaceableTile(ModContent.TileType('ExampleOre'));
}
```

When broken, the mod item that places that tile drops. For another item, use
`this.ItemDrop = type` on the `ModTile`.

## Fields

| Field | What for |
|---|---|
| `Texture` | The path in the mod, without `.png`. Default: the class's file (`Content/Tiles/X.js` -> `Content/Tiles/X.png`). |
| `HighlightTexture` | The `TileID.Sets.HasOutlines` outline (default: `Texture + '_Highlight'`). Without the file, there is no outline. |
| `DustType` | The dust when hitting and breaking (`Terraria.ID.DustID`); `-1`, none. |
| `HitSound` | The sound when hitting (`Terraria.ID.SoundID`). Without it, the game's sound. |
| `MinPick` | Minimum pickaxe power. |
| `MineResist` | How much the tile resists: each hit's damage is divided by it. |
| `ItemDrop` | The item that drops (default: the item that places the tile, by style). |
| `AdjTiles` | Counts as these crafting stations: `[TileID.WorkBenches]`. |
| `AnimationFrameHeight` | The height of one animation frame in the texture (see `AnimateTile`). |
| `CacheDrawData` | `false`: `SetDrawPositions`, `AnimateIndividualTile` and `SetSpriteEffects` run on every draw, without the cache (see below). Default `true`. |

A mod tile required by a recipe (`AddTile`) shows up in the crafting guide
with its map name (`AddMapEntry`) and the icon of the item that places it
(`TileID.Sets.CraftingStationItemId`, a mobile-only table; Bunny Loader fills
it in at the end of the recipes).

## Methods

tModLoader's `ref` parameters arrive as `Ref`s: read and write `.value`
(`num.value = 3`), as in [guide 2](02-ref-e-out.md).

| Method | When |
|---|---|
| `SetStaticDefaults()` | Once, with the type already in the tables (`Main.tileSolid[this.Type]`...). |
| `PostSetDefaults()` | Right after (the solid tile's `tileNoSunLight` has already been set). |
| `AddMapEntry(color, name)` | The color and the name on the map (see below). Without it, the tile does not show on the map. |
| `RegisterItemDrop(item, ...styles)` | The item that drops, for every style or only the listed ones. |
| `CanKillTile(i, j, blockDamaged)` | `false`: the pickaxe does not break it. |
| `KillTile(i, j, fail, effectOnly, noItem)` | Before the tile goes away (`fail`: just the hit). All three are `Ref`s. |
| `KillMultiTile(i, j, frameX, frameY)` | A piece of furniture was removed whole; `(i, j)` is the top-left corner. |
| `NumDust(i, j, fail, num)` | How many dust particles (at most the game's 10). |
| `CreateDust(i, j, type)` | `false`: no dust; `type.value` swaps the dust. |
| `KillSound(i, j, fail)` | `false`: no sound. |
| `CanDrop(i, j)` / `GetItemDrops(i, j)` | Whether an item drops / which ones (types or `{ type, stack }`, up to two). |
| `PlaceInWorld(i, j, item)` | The player just placed it. |
| `TileFrame(i, j, resetFrame, noBreak)` | `false`: the game does not frame it (you did). |
| `RandomUpdate(i, j)` | The world's random update (herbs grow here). |
| `NearbyEffects(i, j, closer)` | The tile is near the player (`closer`: on screen). |
| `ModifyLight(i, j, r, g, b)` | The light, with `Main.tileLighted[type]`. Recalculated every 3 frames (the last one applies in between). |
| `AnimateTile(frame, frameCounter)` | Once per frame, for the whole type (`Main.tileFrame`). |
| `AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset)` | One cell's frame. |
| `SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY)` | Where and what to draw for the cell. |
| `SetSpriteEffects(i, j, spriteEffects)` | Mirroring (`1` = horizontal). |
| `PreDraw(i, j, spriteBatch)` | You draw; `false` = not even the default drawing. |
| `PostDraw(i, j, spriteBatch)` | On top of the tile (flames). |
| `DrawEffects(i, j, spriteBatch, drawData)` | Requests `SpecialDraw` with `Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j)`. |
| `SpecialDraw(i, j, spriteBatch)` | The drawing requested above (the floating relic). |
| `EmitParticles(i, j, tile, frameX, frameY, light, visible)` | The cell's dust and particles on screen. Declare only the parameters you use: without `tile` and `light` in the signature, they are not even fetched. |
| `RightClick(i, j)` | The player tapped the tile. `true`: handled. |
| `MouseOver(i, j)` / `MouseOverFar(i, j)` | The finger (or the cursor) over the tile. |
| `HitWire(i, j)` | A powered wire went through the tile. |
| `Slope(i, j)` | The hammer; `false` leaves the tile as it is. |
| `ModifySittingTargetInfo(i, j, info)` | Where and which way you sit (`TileID.Sets.CanBeSatOnForPlayers`). |
| `ModifySleepingTargetInfo(i, j, info)` | Where you lie down (`TileID.Sets.CanBeSleptIn`). |
| `IsLockedChest`, `UnlockChest`, `LockChest` | The locked chest. |

`i` and `j` are the position in **tiles** (pixels / 16). `bl.tiles.typeAt(x, y)`
gives the type of the active tile at a position (-1 if there is none).

**Touch on mobile.** The game only treats a tap on a tile as an interaction
(and calls `RightClick`) when the cursor icon is on, which on PC `MouseOver`
does just for show. A mod tile with `RightClick` whose `MouseOver` does not
turn on the icon gets the icon of the item that places it, automatically. For
another icon, turn it on in `MouseOver`: `player.cursorItemIconEnabled = true`,
`player.cursorItemIconID = item` and `player.noThrow = 2`, like the Example
Mod's dresser. `RightClick` runs once per tap.

A mod dresser (`TileID.Sets.BasicDresser`) works like the game's: the chest
stays open and the clothing window opens from the bottom drawer. The mobile
game only accepted its own dresser (type 88) for both.

`ModTile` hooks only enter JS for **mod tiles** (a native filter that reads
the type straight from the world), and the per-frame ones (light, drawing,
animation) only for the types that **override** that method: the
[core](../nucleo/hooks.md#native-filters) markers.

## Furniture (TileObjectData)

A piece of furniture is a tile with `tileFrameImportant` and a
`TileObjectData`: the size, what it rests on and the height of each row of the
texture. The Example Sink (2x2), from the Example Mod:

```js
const { TileObjectData } = Terraria.ObjectData;

export class ExampleSink extends ModTile {
    SetStaticDefaults() {
        Terraria.Main.tileSolid[this.Type] = false;
        Terraria.Main.tileFrameImportant[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(100, 100, 100), this.constructor.name);
    }
}
```

Same as tModLoader: copy a vanilla template (`Style2x2`, `Style3x2`,
`Style1x2`...), change what you need **between** `CopyFrom` and `addTile`, and
register. The item is a `ModItem` with `DefaultToPlaceableTile`, like a
block's.

The texture has a cell 16 wide per column, with a 2 px margin, and the height
of each row from `CoordinateHeights` (`[16, 18]`: the bottom one is 18, and
the extra 2 px go down over the floor, as with the game's furniture).

What Bunny Loader does for you:

- **Placing**: through the item, or `WorldGen.PlaceObject(x, y, type)`.
- **Breaking**: break one cell and the whole object comes out, with **a
  single** item. Remove the block below (or the wall, depending on the
  anchor), same thing. `KillMultiTile(i, j, frameX, frameY)` reports it, with
  the top-left corner.
- **Saving**: each cell's frame is stored in `.tiles.bl`, and the furniture
  comes back as it was.

### Furniture with behavior

The Example Mod brings tModLoader's, in `Content/Tiles/Furniture/`. What each
one uses:

| Furniture | How |
|---|---|
| Workbench, table | `AdjTiles = [TileID.WorkBenches]`: the game's recipes accept the mod one. `TileID.Sets.RoomNeeds.CountsAsTable` counts for housing (Bunny Loader also adds the type to the `CountsAsTableTypes` list, which is the one the game's housing check walks; same for chairs, torches and doors). |
| Chair, toilet | `TileID.Sets.CanBeSatOnForPlayers` and, in `RightClick`, `player.sitting.SitDown(player, i, j)`. `ModifySittingTargetInfo` says which way it faces and which cell is the bottom one. |
| Bed | `TileID.Sets.CanBeSleptIn` (lying down: `player.sleeping.StartSleeping`) and `TileID.Sets.IsValidSpawnPoint` (`Player.CheckSpawn` accepts the mod bed as a spawn point). |
| Door | Two tiles: the closed one with `TileID.Sets.OpenDoorID` and the open one with `CloseDoorID`. Tapping opens or closes it; like the game's, it opens by itself for a player who walks into it and closes after they pass; town NPCs open and close it as they pass, goblins break it down, and wires open and close it (without a custom `HitWire`). The game syncs it in multiplayer. |
| Chest, dresser | `TileID.Sets.BasicChest` (or `BasicDresser`) and `Main.tileContainer`; the game chest's placement hooks (`HookPostPlaceMyPlayer`) create the `Main.chest`. `IsLockedChest`/`UnlockChest` for the locked one. |
| Torch | `TileID.Sets.Torches` and the game torch's shape (`GetTileData(TileID.Torches, 0, 0)`): the game attaches it to walls and sides. |
| Campfire | `TileID.Sets.Campfires`; `NearbyEffects` turns on `Main.SceneMetrics.HasCampfire` (the buff). |
| Music box | `MusicLoader.AddMusicBox(mod, slot, item, tile)`: it starts off, like the game's; a tap (or a wire) turns it on, and when on and on screen, or equipped as an accessory, it plays the track. |
| Lamp, chandelier, statue, trap | `HitWire`: the wire turns it on, off, drops a coin or shoots. |

`TileID.Sets.OpenDoorID`, `CloseDoorID`, `IsValidSpawnPoint`,
`MultiTileSway` and `TileObjectData.GetTileStyle`/`IsTopLeft`/`TopLeft` are
tModLoader's: the mobile game does not have them, and Bunny Loader puts them
in place (same names). `Point16.new(x, y)` and `AnchorData.new(type, count, start)`
build the `TileObjectData` structs.

Not available: wind sway (`MultiTileVine`, `AdjustMultiTileVineParameters`),
mod palm trees and cacti (`ModPalmTree`, `ModCactus`),
`ModifyFrameMerge`/`PostTileFrame`, the pressure plate (`SwitchTiles`) and
`HasSmartInteract` (on mobile you tap the tile). Trees: see
[Trees](#trees-modtree).

### Tile entities (ModTileEntity)

A tile does not store data or run code every frame. For that there is the
tile entity: an object attached to a placed piece of furniture. The
tModLoader Example Mod's barrel that fills up in the rain looks like this:

```js
const { TileObjectData } = Terraria.ObjectData;

export class WaterBarrelEntity extends ModTileEntity {
    Fill = 0;

    // Required: is the block still the barrel? An entity without a barrel is discarded.
    IsTileValidForEntity(x, y) {
        return bl.tiles.typeAt(x, y) === ModContent.TileType(WaterBarrel);
    }

    // On the server (or in single player), on each world update.
    Update() {
        if (!Terraria.Main.raining || this.Fill >= 3600) return;
        this.Fill++;
        // Clients get it through NetSend; here, every 10%.
        if (this.Fill % 360 === 0 && (Terraria.Main.netMode & 2)) {
            Terraria.NetMessage.SendData(86, -1, -1, null, this.ID, 0, 0, 0, 0, 0, 0);
        }
    }

    SaveData(tag) { tag.Fill = this.Fill; }
    LoadData(tag) { this.Fill = tag.Fill ?? 0; }
    NetSend(writer) { writer.Write(this.Fill); }
    NetReceive(reader) { this.Fill = reader.ReadInt32(); }
}

export class WaterBarrel extends ModTile {
    SetStaticDefaults() {
        Terraria.Main.tileFrameImportant[this.Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        // The entity is created when the player places the barrel.
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(WaterBarrelEntity).Generic_HookPostPlaceMyPlayer;
        TileObjectData.addTile(this.Type);
    }

    // And goes away with it.
    KillMultiTile(i, j, frameX, frameY) {
        ModContent.GetInstance(WaterBarrelEntity).Kill(i, j);
    }

    RightClick(i, j) {
        const barrel = ModTileEntity.TryGet(i, j, WaterBarrelEntity);
        if (barrel) Terraria.Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](Math.floor(barrel.Fill / 36) + '%', 255, 255, 255, false);
        return true;
    }
}
```

- The entity lives at the object's top-left corner (`Position`);
  `ModTileEntity.TryGet(i, j, Class)` finds it from any cell.
- `Update` runs on the server or in single player, never on the client: what
  the client needs to know goes through `NetSend`/`NetReceive`. The server
  sends it on placement, to whoever joins the world and when the mod asks
  (`NetMessage.SendData(86, ...)` with the `ID`). On mobile, a player who hosts
  and plays has `Main.netMode` 3: test for the server with `Main.netMode & 2`.
- On the client, placing asks the server, which checks
  `IsTileValidForEntity`, calls `OnNetPlace` and sends the entity back.
- The entity is saved in `<world>.wld.bl.json`. When the world opens, one that
  fails `IsTileValidForEntity` is discarded; one from a missing mod stays in
  the file until the mod returns.
- The game does not know about them (a new C# class cannot be created): the
  `ID`s start at `0x40000000` and the game's `TileEntity.ByID` does not have
  them. Use `ModTileEntity.ByID`.
- The Example Mod has this whole barrel, with drawing that follows the water
  level, a tap that gives a bucket through the server and a pickaxe that drops
  the bucket: `Content/TileEntities/BasicTileEntity.js`.

### Trees (ModTree)

A mod tree grows like the game's (it is the same tile 5), and it is the
**ground** under the trunk that says whose tree it is: converting the ground
changes the tree, as in the game. It needs a mod block for the ground and, to
plant it, a mod sapling:

```js
const { TileObjectData } = Terraria.ObjectData;
const { TileID } = Terraria.ID;

export class FrostSoil extends ModTile {
    SetStaticDefaults() {
        Terraria.Main.tileSolid[this.Type] = true;
        this.AddMapEntry(Color.new(120, 160, 200));
    }
}

// The sapling: 1x2, anchored to the tree's ground.
export class FrostSapling extends ModTile {
    SetStaticDefaults() {
        Terraria.Main.tileFrameImportant[this.Type] = true;
        const d = TileObjectData.newTile;
        d.Width = 1;
        d.Height = 2;
        d.Origin = Point16.new(0, 1);
        d.AnchorBottom = AnchorData.new(1, 1, 0);   // AnchorType.SolidTile
        d.UsesCustomCanPlace = true;
        d.CoordinateHeights = [16, 18];
        d.AnchorValidTiles = [ModContent.TileType(FrostSoil)];
        TileObjectData.addTile(this.Type);
        TileID.Sets.TreeSapling[this.Type] = true;
        TileID.Sets.CommonSapling[this.Type] = true;
    }

    // The sapling grows on its own, like the game's.
    RandomUpdate(i, j) {
        if (Math.random() < 1 / 20) Terraria.WorldGen.AttemptToGrowTreeFromSapling(i, j, false, 0, false);
    }
}

// Textures next to the class: FrostTree.png, FrostTree_Tops.png, FrostTree_Branches.png.
export class FrostTree extends ModTree {
    SetStaticDefaults() {
        this.GrowsOnTileId = [ModContent.TileType(FrostSoil)];
    }

    // An acorn planted on its ground becomes this sapling.
    SaplingGrowthType(style) {
        style.value = 0;
        return ModContent.TileType(FrostSapling);
    }

    DropWood() { return Terraria.ID.ItemID.BorealWood; }
}
```

- The textures follow the game's: the trunk is **one strip** of tile 5's
  sheet (176 wide), the tops are 3 frames of 80x80 with 2 of spacing and the
  branches are 2 columns (left, right) of 3 frames of 40x40. The default names
  are `<Class>.png`, `<Class>_Tops.png` and `<Class>_Branches.png`; for others,
  override `GetTexture`/`GetTopTextures`/`GetBranchTextures` (returning
  `ModContent.Request(path)`).
- `SetStaticDefaults` runs when content loads, on the game thread.
- The mod sapling grows through `WorldGen.AttemptToGrowTreeFromSapling`
  (fertilizer uses it too), which calls its `GrowSapling(i, j, underground,
  treeHeightAddon, ignoreWalls)`: the default makes the tree grow.
- `CountsAsTreeType` (the default is `TreeTypes.Custom`), `CreateDust`,
  `TreeLeaf` (the leaf gore, when growing, shaking and in the wind),
  `CanDropAcorn` and `Shake(x, y, createLeaves)`: `false` removes the game's
  drops when shaking.
- Drawing the trunk and the top is native: no JS cost per frame. There is no
  `SetTreeFoliageSettings` and no paint (a painted mod tree comes out
  uncolored).
- The falling leaf is a mod gore (a PNG in `Gores/`) with
  `GoreID.Sets.SpecialAI[type] = 3`. The game cuts it into 1 column of 8
  frames; tModLoader's leaf, with 32 columns (one per paint), does not work
  as is.
- The Example Mod has `ExampleTree`, which grows on the Example Block, with
  the sapling and the leaf: `Content/Tiles/Plants/`.

### Pylons (ModPylon)

A teleportation pylon is a `ModPylon` (a 3x4 `ModTile`) with a
`TEModdedPylon`. The game's pylon system starts seeing it: it goes into the
list, shows up on the map, counts as "near a pylon" and tapping the icon
teleports. In multiplayer, the list reaches clients through the game's own
message, and the client asks the server to teleport as with a vanilla pylon
(tested with host and client: `tools/tests/mppylon`).

```js
const { TileObjectData } = Terraria.ObjectData;

export class FrostPylonEntity extends TEModdedPylon {}

// Textures: FrostPylon.png (the 3x4), FrostPylon_Crystal.png (8 frames
// stacked vertically) and FrostPylon_MapIcon.png.
export class FrostPylon extends ModPylon {
    SetStaticDefaults() {
        Terraria.Main.tileFrameImportant[this.Type] = true;
        Terraria.Main.tileLighted[this.Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(FrostPylonEntity).PlacementHook(false);
        TileObjectData.addTile(this.Type);
        this.AddMapEntry(Color.White, 'Frost Pylon');
        this.crystal = ModContent.Request(this.Texture + '_Crystal');
    }

    KillMultiTile(i, j, frameX, frameY) {
        ModContent.GetInstance(FrostPylonEntity).Kill(i, j);
    }

    // Only works in the snow (sceneData is the SceneMetrics scanned around the pylon).
    ValidTeleportCheck_BiomeRequirements(pylonInfo, sceneData) {
        return sceneData.EnoughTilesForSnow;
    }

    SpecialDraw(i, j, spriteBatch) {
        this.DefaultDrawPylonCrystal(spriteBatch, i, j, this.crystal, this.crystal,
            Vector2.new(0, -12), Color.new(25, 25, 25, 25), Color.White, 1, 8);
    }
}
```

- `PylonType` is 11, 12... in load order (the game's go up to 10). Only one
  per type in the world, like the game's (`CanPlacePylon`).
- `ValidTeleportCheck_NPCCount` decides for the destination pylon and for the
  one you leave from; `ValidTeleportCheck_BiomeRequirements`, for the
  destination (the game refuses with its own message, "biome requirements").
- Tapping the pylon opens the map like the game's pylon on mobile: with it in
  `Main.MapPylonTile`, every revealed pylon framed and the lines starting from
  it. Only then does a tap teleport: on the regular map, an off-screen pylon is
  pinned to the edge, and tapping it just pans the map. Whoever overrides
  `RightClick` calls `super.RightClick(i, j)`.
- On the map, the icon is `<Texture>_MapIcon.png`; without it, the game
  pylon's. `DrawMapIcon` replaces the drawing (the `DefaultDrawMapIcon` and
  `DefaultMapClickHandle` helpers are tModLoader's).
- Counting a mod block around the destination:
  `sceneData._tileCounts[ModContent.TileType('MyBlock')]`.
  `ModSystem.TileCountsAvailable` only receives the player's scan.
- The Example Mod has `ExamplePylonTile`: it requires 40 Example Blocks
  around it, and town NPCs sell it like the game's pylons, through a
  `GlobalNPC`'s `ModifyShop` (`Common/GlobalNPCs/ExamplePylonShop.js`),
  instead of tModLoader's `GetNPCShopEntry`.

### Per-frame cost

Tiles are the most numerous content on screen (a base has dozens of torches),
and what the mod does per tile, per frame, adds up. What became cheap, and how
to keep it that way:

- **Light**: the game calculates tile light in an internal copy of its code,
  so a mod tile's light goes in through `Lighting.AddLight`, in a native
  batch, with `ModifyLight` recalculated every 3 frames. 150 mod torches cost
  the same frame time as 150 vanilla torches (measured in
  `tools/tests/tileperf`).
- **Flame**: 7 `spriteBatch.Draw` calls per torch in `PostDraw` (tModLoader's
  way) cost ~16 ms per frame with 150 torches. The example torch uses
  `Main.tileFlame[type] = true`: the game draws its own torch flame, with no
  JS. Drawing in `PostDraw` is for tiles that rarely show up (lamp, campfire,
  relic).
- **RNG**: `Rand` is a trip into the game; for "1 in 40, per tile, per frame"
  use `Math.random()`.
- **Parameters**: in `EmitParticles`, declare only what you use.

## The saved world keeps opening without the mod

The `.wld` **never** carries mod tiles. On save, mod tiles are written
separately, to `<world>.wld.tiles.bl`, by name (`<mod>/<Class>`), and in the
`.wld` they stay as air (wall, liquid and wires remain). On load, they come
back.

Without the mod, the world opens normally in the game, with air in their
place, and the side file keeps the tiles for when the mod returns. If someone
builds in that spot in the meantime, what was built wins. A piece of furniture
(a multi-cell object) comes back whole or not at all: plants and grass that
grew in its place (`Main.tileCut`) are removed; anything else built there
keeps the whole piece stored in the file until the spot is free. Furniture
that had been saved half-done comes back with the missing cell. The mod order
can change freely: the tile comes back by name, not by number.

When the world reopens, the game reframes every common block and **rolls the
variant again** (each shape has 3 drawings). That applies to dirt, stone and
mod tiles: a block may show up with another drawing, in the same shape.
tModLoader does the same: it only stores the frame for tiles with
`tileFrameImportant` (furniture and objects).

## The map

Each `AddMapEntry` becomes **its own** map entry, as in tModLoader: the exact
color requested and its name (what shows up when you drag your finger over the
map). Calling it again creates another option for the same tile (open and
locked chest, for example).

Mobile's `.map` stores each point's color **index**, and a mod entry's index
does not exist in the game without the mod. So, on save, the points of mod
tiles are taken out of the `.map` (they stay dark) and go to a side file,
`<map>.map.bl`, by tile name. On load, they come back. Without the mod, the
game sees those points as unexplored, and `.map.bl` keeps them until the mod
returns. This also applies to the part of the map the game compresses outside
of saving, when unloading or recycling: mod points are kept separately and come
back when that part does.

## Walls (ModWall)

A new wall is like tModLoader's `ModWall`: a class that extends `ModWall`,
exported from `Content/`, with the texture next to it (the game's wall frame
sheet, 468x180). The item that places it uses `DefaultToPlaceableWall`. The
Example Mod's ExampleWall:

```js
// Content/Walls/ExampleWall.js (+ ExampleWall.png)
const { DustID, WallID } = Terraria.ID;
const { Main } = Terraria;

export class ExampleWall extends ModWall {
    SetStaticDefaults() {
        Main.wallHouse[this.Type] = true;   // counts as a housing wall
        this.DustType = DustID.Stone;
        this.AddMapEntry(Color.new(150, 150, 150));
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }
}
```

```js
// Content/Items/Placeable/ExampleWall.js (+ ExampleWall.png)
export class ExampleWall extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableWall(ModContent.WallType('ExampleWall'));
    }
}
```

| Field | |
|---|---|
| `Type` | The wall's number (after the game's 367). |
| `DustType` | The dust when hitting and breaking. Without it, the game's. |
| `HitSound` | The sound when breaking (a `SoundID` or style). Without it, the game's. |
| `ItemDrop` | The item that drops when broken. Without it, the mod item that places this wall (`createWall`). |

| Method | When |
|---|---|
| `SetStaticDefaults()` | Once, with the game tables already holding the type: `Main.wallHouse[this.Type]`, `Main.wallLight`, `WallID.Sets...`. |
| `KillWall(i, j, fail)` | On hitting or breaking. `fail` is a `Ref`: `fail.value = true` only makes dust. |
| `NumDust(i, j, fail, num)` | How many dust particles (`num.value`). |
| `CreateDust(i, j, type)` | Before each dust particle; `type.value` changes the type, `false` does not create it. |
| `KillSound(i, j, fail)` | `false` does not play the sound. |
| `Drop(i, j, type)` | The item that drops (`type.value`); `false` leaves nothing. |
| `ModifyLight(i, j, r, g, b)` | The light the wall gives off (`r.value`, `g.value`, `b.value`, from 0 to 1). Runs for walls near the screen, every 3 frames. |
| `AnimateWall(frame, frameCounter)` | Once per frame, per type: `frame.value` picks the texture's 180 px strip (the sheet with two frames is 468x360). |
| `RandomUpdate(i, j)` | On the world's random update, like the block's. |
| `AddMapEntry(color, name)` | The color on the map and the name when tapping it (`MapObject.<Class>` in Localization; without it, the class name). |
| `WallFrame(i, j, randomizeFrame, style, frameNumber)` | When framing the wall: `style.value` and `frameNumber.value` pick the frame of the border (0 to 2 is the variation); `false` leaves the border as it was. |

The Example Mod's `ExampleWallAdvanced` uses animation (two frames), green
light at night and emerald dust.

A mod wall takes part in biome **conversion** through the game's tables, which
grow with the new type: `WallID.Sets.Conversion.Stone[this.Type] = true` in
`SetStaticDefaults` makes the corruption, the crimson and the hallow treat it
as a stone wall.

By name or by class: `ModContent.WallType('ExampleWall')`,
`ModContent.GetModWall(type)`, `this.GetContent(ModWall)`.

The saved world keeps opening without the mod, as with blocks: the `.wld`
never carries mod walls. They go to a side file, `<world>.wld.walls.bl`
(position, paint and `<uid>/<Class>`), and come back on load, with the paint.
On the map it is the same as blocks: mod wall points leave the `.map` and go
to `.map.bl`.

Without the mod, the spot gets Bunny Loader's **unloaded wall** (tModLoader's
`UnloadedWall`, a magenta checkerboard). It holds what is attached to the wall,
like paintings and torches, and the file keeps the original wall until the mod
returns: then the original goes back in its place. Breaking the unloaded wall
loses the original for that spot, as in tModLoader.

`VanillaFallbackOnModDeletion` exists on the class, but does nothing yet: in
tModLoader it only applies when permanently deleting a removed mod's data, a
command Bunny Loader does not have yet.

What it costs (MuMu, 2130 walls on screen, `tools/tests/wallperf`): a mod wall
with only a texture costs the same as the game's. `ModifyLight` is the
expensive part: it runs for each wall near the screen, a third of the rows per
frame, and with 2130 lit walls it comes to ~1.2 to 1.5 ms of JS per frame.
Placing with `WallFrame` costs ~60 µs per wall, only at that moment. The world
save stays the same (~200 ms).

There is no `PreDraw`/`PostDraw`: mobile draws walls in a single loop, split
across threads, with no per-cell call.

## Under the hood

What Bunny Loader does so the new tile does not break the game (details in
[content under the hood](../nucleo/conteudo.md)):

- grows the game's ~220 tile tables (`Main.tile*`, `TileID.Sets` and the
  nested ones, textures, map, recipes, materials), with the new type at each
  one's default value, and `Main.tileMerge` (754x754) row by row;
- grows the biome tile counts (`SceneMetrics`) and the nearby crafting
  stations (`adjTile`): without this, counting a mod tile writes outside the
  array;
- swaps the compiled limits in the code (`PlaceTile`, `KillTile`,
  `TileFrame`...);
- makes room for mod tiles in mobile's internal world tile table (where each
  identical tile is stored only once);
- calls `ModTile` hooks only for mod tiles;
- when drawing, `SetDrawPositions`, `AnimateIndividualTile` and
  `SetSpriteEffects` are called by the native drawing itself (which runs on
  several threads), and only for the types that override them; what the mod
  draws (`PreDraw`, `PostDraw`, `SpecialDraw`) comes out in a pass after the
  tiles, on the game thread;
- the result of those three is cached per cell: a cell the game redraws
  unchanged does not call JS again (each trip cost ~30 to 50 µs on the
  emulator, almost all of it waiting for the JS engine). The key is the
  position, the type, the cell's frame and the type's `Main.tileFrame`; it
  leaves the cache when a nearby block changes (the game reframes) and after
  5 s. Anything that animates by another counter (time,
  `Main.GameUpdateCount`) sets `CacheDrawData = false`;
- doors, beds and spawn points use the game's code: during
  `OpenDoor`/`CloseDoor`/`CheckSpawn`, the mod door or bed becomes the game's
  (10, 11, 79), and what the game built becomes mod content again;
- the same disguise, in C++, for whoever opens doors on their own: the player
  who walks into it (`DoorOpeningHelper.Update`) and the NPC AIs that open
  doors (`AI_007_TownEntities`, `AI_003_Fighters`, `AI_107_ImprovedWalkers`).
  Mod doors a few tiles away become 10/11 during the call; the `KillTile` of a
  goblin breaking the door undoes the disguise first, so the mod door's item
  and dust come out. With no mod door nearby, it costs a scan of ~120 cells
  per walking NPC.
