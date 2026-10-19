# 12. Globals and the world

Up to here, each mod class created something **new**: an item, an NPC, a
buff. This guide is about changing what **already exists**, about what belongs
to the whole world and about what has to go from one device to another in
multiplayer:

- `GlobalItem`, `GlobalNPC`, `GlobalProjectile`: code that runs for the
  **vanilla** items, NPCs and projectiles (and the mod ones), like making the
  copper sword stronger or making every slime drop extra gel;
- `GlobalTile`, `GlobalBuff`, `GlobalBossBar`, `GlobalRecipe`: the same for
  every tile, buff, boss bar and recipe;
- **drops**: changing a vanilla NPC's table, or adding a rule that applies to
  all of them;
- `ModSystem`: what belongs to the whole mod, and the world cycle (load, save,
  leave) with data stored along with it;
- the **network**: mod data the server sends to clients (and what a client
  sends to the server), with `NetSend`/`NetReceive` and custom packets.

Read the [ideas in guide 4](04-conteudo-novo.md) first. The full list is in
the reference: [Globals](../referencia/classes.md#globalitem-globalnpc-and-globalprojectile),
[`GlobalLoot`](../referencia/classes.md#globalloot) and
[`ModSystem`](../referencia/classes.md#modsystem).

## Global or direct hook?

Everything a Global does can also be done with a direct hook
([guide 1](01-hooks-do-zero.md)). The Global is the same hook, ready-made and
shaped like tModLoader's:

- tModLoader code carries over almost as is (`AppliesToEntity`,
  `InstancePerEntity`, `ModifyNPCLoot`);
- several mods can have Globals on the same method without getting in each
  other's way;
- per-entity state (a counter per NPC, a charge per item) comes for free,
  with no hand-made map.

Use the direct hook when the Global does not have the method you need.

## The cost

A `ModItem` only enters JS for **your** item: the hook has a native type
filter, and vanilla items pass straight through. A Global has no such filter,
because it exists precisely for vanilla items. **Every** item, NPC or
projectile that goes through the method enters JS, and `AppliesToEntity`
decides in there.

In practice:

- a method no Global wrote installs no hook and costs nothing;
- `SetDefaults`, `OnSpawn`, `OnKill`, `HitEffect`, `ModifyNPCLoot` run
  rarely: on creating, hitting or killing;
- `PreAI`, `AI` and `PostAI` run **every frame for every entity**: up to 200
  NPCs or 1000 projectiles. A global `AI` is the most expensive method there
  is. Bail out early and keep `AppliesToEntity` cheap (compare the `type`).

The math from the [cost guide](03-custo-e-desempenho.md#how-often-the-game-calls-each-method)
applies here: a forwarding-only global `AI`, with 150 NPCs, is around 0.1 ms
per frame, plus whatever your code does. A global `SetDefaults` runs thousands
of times when the world loads, and adds up to seconds if it is heavy.

## GlobalItem

A `GlobalItem` runs for vanilla items. `AppliesToEntity` picks which ones;
without it, it applies to all of them.

```js
const { ItemID } = Terraria.ID;

export class ShortswordGlobalItem extends GlobalItem {
    AppliesToEntity(item, lateInstantiation) {
        return item.type === ItemID.CopperShortsword;
    }

    SetDefaults(item) {
        item.damage = 50;
    }
}
```

The methods are `ModItem`'s (`SetDefaults`, `CanUseItem`, `UseItem`,
`HoldItem`, `Shoot`, `ModifyShootStats`, `UpdateAccessory`, `UpdateInventory`,
`OnCraft`, `ModifyTooltips`...), with the item as the first parameter. When a
mod item also has the method, the `ModItem` one runs first and then the
Globals, as in tModLoader. Also:

- damage, crit, knockback and size: `ModifyWeaponDamage`, `ModifyWeaponCrit`,
  `ModifyWeaponKnockback`, `ModifyItemScale`, `UseItemHitbox`, `MeleeEffects`,
  `UseAnimation`, `UseItemFrame`, `HoldItemFrame`;
- ammo: `NeedsAmmo`, `CanChooseAmmo`, `CanBeChosenAsAmmo`, `CanConsumeAmmo`,
  `CanBeConsumedAsAmmo`, `OnConsumeAmmo`, `OnConsumedAsAmmo`,
  `PickAmmo(weapon, ammo, player, type, speed, damage, knockback)`;
- healing: `GetHealLife`, `GetHealMana`, `ModifyPotionDelay`, `ApplyPotionDelay`;
- hits: `CanHitNPC`, `ModifyHitNPC`, `OnHitNPC`, `CanHitPvp`, `ModifyHitPvp`,
  `OnHitPvp` (see [Hits](16-golpes.md)).

In the ammo `Can` methods, a `false` from anyone (the mod item or a Global)
vetoes; in the ones that accept `null`, otherwise a `true` forces it.

## GlobalNPC and GlobalProjectile

Same idea, for NPCs and projectiles:

```js
const { NPCID } = Terraria.ID;

export class SlimeGlobalNPC extends GlobalNPC {
    AppliesToEntity(npc, lateInstantiation) {
        return npc.type === NPCID.BlueSlime;
    }

    SetDefaults(npc) {
        npc.lifeMax *= 2;
    }

    OnKill(npc) {
        bl.log('a slime died at ' + npc.position);
    }
}
```

- `PreAI` returning `false` skips the game's AI **and** the other Globals'
  `AI`; `PostAI` runs regardless.
- `PreKill` returning `false` makes the NPC die with no drop and no `OnKill`.
- `GetChat(npc, chat)`: `chat` is a `Ref` with the line the game picked;
  swap `chat.value`. `CanChat(npc)`: `false` prevents chatting, `true` allows
  it, `null` leaves it to the game.
- `FindFrame(npc, frameHeight)` (after the game's frame), `CheckActive`,
  `CheckDead`, `ApplyDifficultyAndPlayerScaling`, `ModifyNPCHappiness`.
- Drawing: `PreDraw` (the Globals before the `ModNPC`; `false` does not draw),
  `PostDraw`, `DrawEffects`, `GetAlpha`, `BossHeadSlot`, `BossHeadRotation`,
  `BossHeadSpriteEffects`.
- Hits: the same as `ModNPC`'s, also on vanilla NPCs (see
  [Hits](16-golpes.md)).

`ModNPC` and `GlobalNPC` go through the same game hooks (the `NPCLoader`):
each hook only enters JS for the NPC types someone asked for, the mod one that
writes the method, or all of them when a Global writes it.

### Shops

`GlobalNPC` changes the game's shops and other mods':

```js
const { ItemID, NPCID } = Terraria.ID;

export class MerchantShop extends GlobalNPC {
    // Once per shop: what Add puts in goes after its items.
    ModifyShop(shop) {
        if (shop.NpcType === NPCID.Merchant) shop.Add(ItemID.Wood, { condition: () => Terraria.Main.dayTime });
    }

    // tModLoader's old format: the free slot in nextSlot (Ref).
    SetupShop(type, shop, nextSlot) {
        if (type !== NPCID.Merchant) return;
        shop.item[nextSlot.value]['void SetDefaults(int Type, ItemVariant variant)'](ItemID.Torch, null);
        nextSlot.value++;
    }

    // On every opening, with the items ready.
    ModifyActiveShop(npc, shopName, items) {}

    // The Traveling Merchant's shop: shop is Main.travelShop (the types).
    SetupTravelShop(shop, nextSlot) {
        shop[nextSlot.value++] = ItemID.Gel;
    }
}
```

`NPCShop.get(NPCID.Merchant)` returns the game's shop (the Painter's
decoration one is `NPCShop.get(NPCID.Painter, 'Decor')`), and `NPCShop.All`
lists them all. The mobile game builds every shop when the world opens and
again each time one opens: `SetupShop` runs in both.

### Natural spawning

As in tModLoader, `GlobalNPC` changes enemy spawning around each player. It
runs in single player or on the server, and `player` is the target player:

```js
export class BiomeSpawns extends GlobalNPC {
    // Ref: lower spawnRate = more spawns; maxSpawns = how many nearby.
    EditSpawnRate(player, spawnRate, maxSpawns) {
        if (player.InModBiome(MyBiome)) {
            spawnRate.value = Math.floor(spawnRate.value * 0.5);
            maxSpawns.value = Math.floor(maxSpawns.value * 1.5);
        }
    }

    // The roll: pool[type] = weight; 0 is the game's spawn (weight 1).
    EditSpawnPool(pool, spawnInfo) {
        if (spawnInfo.Player.InModBiome(MyBiome)) {
            pool[0] = 0.25;                          // fewer vanilla enemies
            pool[NPCID.GreenSlime] = 0.5;            // and one more vanilla one
        }
    }
}
```

- `EditSpawnFlags(spawnInfo)`: before everything (rate, area and point), the
  player's fields (`noWorms`, `invaders`, `ZoneCorrupt`...). The point is
  still -1.
- `EditSpawnRange(player, spawnRangeX, spawnRangeY, safeRangeX, safeRangeY)`:
  `Ref`s, in tiles: how far out it spawns and how far from the player.
- `EditSpawnInfo(spawnInfo)`: with the point chosen, before the roll. The
  `spawnInfo` fields (`waterTile`, `nearGranite`...) change what the game and
  the `SpawnChance`s read.
- `SpawnNPC(npc, tileX, tileY)`: after a rolled NPC that is not the game's
  spawns.

## Per-entity state

By default, there is **one** Global object, shared by every entity. A field on
it belongs to the whole mod, not to the item.

With `InstancePerEntity`, each entity gets its own copy, and the fields become
its own:

```js
export class ChargeGlobalItem extends GlobalItem {
    InstancePerEntity = true;
    charge = 0;

    AppliesToEntity(item, lateInstantiation) {
        return item.type === ItemID.Gel;
    }
}

// anywhere:
const g = item.GetGlobalItem(ChargeGlobalItem);
g.charge++;
```

- The copy is created in `SetDefaults`. Changing the item's type gives a new
  copy, with the fields reset.
- `item.Clone()` takes the copy along, with its state; the clone's copy is
  another object.
- `GetGlobalItem(Class)` (or `'ClassName'`) **throws** if the Global does not
  apply to that item. To ask, use `item.TryGetGlobalItem(Class, ref)`, which
  returns `true` or `false`.
- `GetGlobalNPC` and `GetGlobalProjectile` work the same way.

`InstancePerEntity = true` as a field, or `get InstancePerEntity() { return true; }`,
both work.

### Drawing, collision and grappling in GlobalProjectile

These methods receive vanilla and mod projectiles. `AppliesToEntity` limits
which entities receive the callbacks, and `InstancePerEntity` keeps each
projectile's state separate.

| Method | Contract |
|---|---|
| `PreDraw(projectile, lightColor)` | `lightColor` is a `Ref<Color>`: read or replace `.value`. `false` cancels the default sprite, keeping chains and lines. Every Global gets the callback; the local `PreDraw` only runs if all of them allow it. |
| `PostDraw(projectile, lightColor)` | After drawing, even with a veto in `PreDraw`. The color is a `Color`, not a `Ref`. Runs after the local `PostDraw`. |
| `GetAlpha(projectile, lightColor)` | Return a `Color` or leave it `undefined`/`null`. The first Global that returns a color decides; then come the local method and the game. |
| `Colliding(projectile, projHitbox, targetHitbox)` | The first boolean return from the Globals decides the collision. With no answer, it asks the local method and the game. |
| `CanDamage(projectile)` | `false` vetoes damage and cutting. Any veto wins; a global `true` wins over the local method. `undefined`/`null` keeps the next decision. The native damage conditions still apply. |
| `ModifyDamageHitbox(projectile, hitbox)` | `hitbox` is a `Ref<Rectangle>`. It can change `.value`'s fields or replace the rectangle. Runs after the local method. |
| `OnTileCollide(projectile, oldVelocity)` | Every Global runs. `false` prevents the death caused by the impact and skips the local method. Death by time, NPC or `Kill()` still works. |
| `TileCollideStyle(projectile, width, height, fallThrough, hitboxCenterFrac)` | All four parameters are `Ref`s. Local method first; then the Globals until the first `false`, which turns collision off for this movement. |
| `MinionContactDamage(projectile)` | `true` allows contact damage from a projectile considered a pet; asks the local method and then the Globals until one allows it. |
| `CanCutTiles(projectile)` | The first boolean return from the Globals decides; then it asks the local method and the game. `false` also prevents the cutting callbacks. |
| `CutTiles(projectile)` | Custom cutting, only when allowed. Globals first, then the local method and the default cutting. |
| `CanUseGrapple(type, player)` | Before spawning, on the class template. Each global boolean answer replaces the previous one; the last defined answer decides. The filter receives a sample of the type. |
| `UseGrapple(player, type)` | `type` is a `Ref<number>`. Runs after the local method, for every template that implements the callback. Filter `type.value` inside the method if needed. |
| `GrappleCanLatchOnTo(projectile, player, tile)` | `tile` is the tile's native object. Any global `false` vetoes; a global `true` allows. With no answer, it keeps the local or native decision. |

The `Ref`s of `PreDraw`, `ModifyDamageHitbox` and `UseGrapple` follow the
Global's contract in tModLoader. In the equivalent `ModProjectile` methods, the
existing API keeps receiving `Color` and `Rectangle` directly, and
`UseGrapple(player, type)` keeps returning the type number. Mobile provides a
`Tile` to the grapple latch test; that is why the callback receives the tile,
instead of the `x` and `y` coordinates of tModLoader's API.

```js
export class BlueArrow extends GlobalProjectile {
    AppliesToEntity(projectile) {
        return projectile.type === ProjectileID.WoodenArrowFriendly;
    }

    PreDraw(projectile, lightColor) {
        lightColor.value = Color.new(70, 130, 255, 255);
        return true;
    }

    ModifyDamageHitbox(projectile, hitbox) {
        hitbox.value.Width += 4;
        hitbox.value.Height += 4;
    }
}
```

The combination rules were checked against
[tModLoader's ProjectileLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ProjectileLoader.cs).
The adapted signatures are in the [GlobalProjectile reference](../referencia/classes.md#globalprojectile).

## Tiles, buffs, boss bars and recipes

`GlobalTile`, `GlobalBuff`, `GlobalBossBar` and `GlobalRecipe` work like the
other Globals, but with **one instance per class** and no `AppliesToEntity`:
the methods receive the type, and the code picks by it. The full list is in
the [reference](../referencia/classes.md#globaltile-globalbuff-globalbossbar-and-globalrecipe).

```js
const { TileID, BuffID, ItemID } = Terraria.ID;

export class SturdyGlass extends GlobalTile {
    // Glass does not break, and explosions spare it.
    CanKillTile(i, j, type, blockDamaged) { return type !== TileID.Glass; }
    CanExplode(i, j, type) { return type !== TileID.Glass; }
}

export class ShortChill extends GlobalBuff {
    // With more than 5 buffs, Chilled lasts at most 3 seconds.
    UpdatePlayer(type, player, buffIndex) {
        if (type === BuffID.Chilled && buffIndex.value >= 5) {
            player.buffTime[buffIndex.value] = Math.min(player.buffTime[buffIndex.value], 180);
        }
    }
}

export class NoTorchesAtNight extends GlobalRecipe {
    RecipeAvailable(recipe) {
        return Terraria.Main.dayTime || recipe.createItem.type !== ItemID.Torch;
    }
}
```

The cost depends on the method: `CanKillTile`, `KillTile`, `ReApplyPlayer`,
`OnCraft` and the like run on an event; `RandomUpdate` enters JS for every tile
the world update picks (hundreds per frame), `UpdateNPC` for every NPC every
frame, and `RecipeAvailable` for every recipe the crafting menu would show,
each time it is rebuilt. Methods that would run per tile on screen every frame
(drawing, light) do not exist on `GlobalTile`.

On a single recipe, tModLoader 1.4's way: `AddCondition` with a
[`Condition`](../referencia/classes.md#condition), and the callbacks.

```js
new ModRecipe()
    .SetResult(ItemID.AlphabetStatueJ)
    .AddIngredient(ItemID.StoneBlock, 10)
    .AddIngredient(ItemID.Chain)
    .AddCondition(Condition.TimeNight)
    .AddCondition('Conditions.LowHealth', () => player.statLife < player.statLifeMax / 2)
    .AddConsumeIngredientCallback((recipe, type, amount) => { if (type === ItemID.Chain) amount.value = 0; })
    .AddOnCraftCallback((recipe, item) => { /* after crafting */ })
    .Register();
```

The Example Mod has one of each: `Common/GlobalTiles/ShakeTrees.js` (what
falls when shaking a tree) and `SunflowerChanges.js` (the Sunflower grows on
the Example Block), `Common/GlobalBuffs/ExampleGlobalBuff.js` (remaining time
in every tip, the Campfire icon shaking), `Common/GlobalBossBars/ExampleGlobalBossBar.js`
(the Eye of Cthulhu's bar), `Common/GlobalRecipes/ExampleGlobalRecipe.js`, and
the conditions and callbacks in `Content/ExampleRecipes.js`.

## Drops

A vanilla NPC's drop table changes in a `GlobalNPC`'s `ModifyNPCLoot`. It runs
**once per NPC type**, when the game finishes loading, with that type's sample
(the same one the Bestiary uses):

```js
const { ItemID, NPCID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;

export class ExampleNPCLoot extends GlobalNPC {
    ModifyNPCLoot(npc, npcLoot) {
        if (npc.type === NPCID.Demolitionist) {
            npcLoot.Add(ItemDropRule.Common(ItemID.Dynamite, 10, 1, 3));
        }
        if (npc.type === NPCID.FireImp) {
            npcLoot.RemoveWhere((rule) => 'itemId' in rule && rule.itemId === ItemID.ObsidianRose);
        }
    }
}
```

- `npcLoot.Get()` returns the rules (in an array), `Add` and `Remove` change
  them one by one, `RemoveWhere(rule => ...)` removes the ones that match.
- A rule is a game object of several classes (`CommonDrop`,
  `DropBasedOnExpertMode`...), and not every one has `itemId`: ask with `in`
  before reading, otherwise it is an [error](01-hooks-do-zero.md#a-wrong-name-is-an-error).
- The Bestiary is rebuilt for the NPCs whose table changed: the new drop shows
  up there.

A rule that applies to **every** NPC goes in `ModifyGlobalLoot`, which runs
once:

```js
ModifyGlobalLoot(globalLoot) {
    globalLoot.Add(ItemDropRule.Common(ItemID.Sapphire, 1000));
}
```

If you come from ExMod, you can use `GlobalLoot` its way, without changing
anything:

```js
export class ExampleLoot extends GlobalLoot {
    ModifyGlobalLoot() {
        this.RegisterToNPC(NPCID.Demolitionist, ItemDropRule.Common(ItemID.Dynamite, 10, 1, 3));
    }
}
```

## ModSystem: the world

`ModSystem` is what belongs to the whole mod. Besides recipes
([guide 5](05-itens.md#modsystem)), it follows the **world**:

| Method | When it runs |
|---|---|
| `Load()` | On registration, once, right before `OnModLoad` (as in tModLoader: register touch buttons here). |
| `OnModLoad()` | On registration, once. |
| `ClearWorld()` | On entering any world, before loading it (and before generating one). |
| `OnWorldLoad()` | The world opened. On a multiplayer client, when the world arrives from the server. |
| `LoadWorldData(tag)` | Right after, with the data the mod saved in this world. |
| `PostWorldLoad()` | After the data. |
| `PreUpdateWorld()`, `PostUpdateWorld()` | Every frame, around the world update. |
| `PreUpdateTime()`, `PostUpdateTime()` | Every frame, around the game clock. |
| `PostUpdateEverything()` | Every frame, at the end of everything. |
| `SaveWorldData(tag)` | On every world save (leaving, autosave). |
| `PreSaveAndQuit()` | On clicking exit, before saving. |
| `OnWorldUnload()` | After leaving the world. |

### Data saved with the world

The `tag` is a `TagCompound`: write to it like an object, and read with
tModLoader's methods. It goes to a file next to the world
(`<world>.wld.bl.json`), as JSON: number, text, boolean, array and plain
object.

```js
export class DownedBossSystem extends ModSystem {
    static downedExampleBoss = false;

    ClearWorld() {
        DownedBossSystem.downedExampleBoss = false;
    }

    SaveWorldData(tag) {
        if (DownedBossSystem.downedExampleBoss) tag.downedExampleBoss = true;
    }

    LoadWorldData(tag) {
        DownedBossSystem.downedExampleBoss = tag.ContainsKey('downedExampleBoss');
    }

    NetSend(writer) {
        writer.WriteFlags(DownedBossSystem.downedExampleBoss);
    }

    NetReceive(reader) {
        [DownedBossSystem.downedExampleBoss] = reader.ReadFlags();
    }
}
```

- Reset the state in `ClearWorld`: without it, a boss defeated in one world
  shows up as defeated in the next.
- A world without the mod's data (new, or saved without it) calls
  `LoadWorldData` with an empty tag.
- `tag.GetInt('x')`, `GetBool`, `GetFloat`, `GetString`, `GetList`,
  `ContainsKey`, `Get(key, default)`: the same names as tModLoader's.
  `ModPlayer` uses the same `TagCompound` in `SaveData`/`LoadData`.
- The file belongs to the server: in multiplayer, the client receives the
  state through `NetSend`/`NetReceive` (see [Network](#network)).

### World generation: structures

`ModifyWorldGenTasks(tasks)` gets the game's generation steps as a JavaScript
array. Find a game step by name and insert yours after it, as Calamity does:

```js
export class ExampleStructureSystem extends ModSystem {
    ModifyWorldGenTasks(tasks) {
        const index = tasks.findIndex((task) => task.Name === 'Sunflowers');
        tasks.splice(index + 1, 0, new PassLegacy('Example Structure', (progress, configuration) => {
            progress.Message = ModLocalization.Translate('WorldGen.ExampleStructure');
            ExampleStructure.Generate();
        }));
    }
}
```

The Example Mod's castle
([`Content/Structures/ExampleStructure.js`](../../samples/ExampleMod/content/Content/Structures/ExampleStructure.js))
follows what the big mods do:

- **Draw the structure as text.** One map of blocks and one of walls, of the
  same size, read from top to bottom. A letter stands for a block, a wall or
  the top-left corner of a piece of furniture. Calamity keeps its structures
  as schematic files with the same idea: block and wall per cell.
- **Search with a limit.** Draw positions with `WorldGen.genRand` (the same
  seed builds the same world) and give up after N attempts. Check flat,
  natural ground, no liquid and `GenVars.structures.CanPlace(area, padding)`,
  which knows the game's structures and those of the other mods.
- **Clear the area first.** `WorldGen.KillTile(..., noItem: true)` on every
  cell, then `ClearEverything()`: a tree or a piece of furniture cut in half
  at the edge would be left broken.
- **Blocks and walls, then the frame, then the furniture.** `ResetToType` and
  `tile.wall`, `WorldGen.RangeFrame` over the area, and only then
  `WorldGen.PlaceObject`/`PlaceChest`: furniture needs the blocks to anchor
  to. The game places furniture by its `Origin`, so convert the map's corner
  with `TileObjectData.GetTileData(type, 0, 0).Origin`.
- **Walls only inside.** A wall draws a half-block border into an empty
  neighboring cell: behind the outer blocks (sides, roofs, battlements,
  foundation), that border sticks out of the structure.
- **Fill the chest** with the index `PlaceChest` returns. On mobile, the
  slots of `Main.chest[i].item` are `ChestItem` (type and stack), not `Item`:
  `SetDefaults(id)` and `stack`. The game's `Tile Cleanup` step rewrites
  every chest as a wooden chest; as in tModLoader, the loader keeps the type
  of mod chests placed before it.
- **Protect the area** with
  `GenVars.structures.AddProtectedStructure(area, padding)`: the later steps
  (trees, micro biomes) do not build over it.
- Fill the ground under the foundation, so the structure does not float.

Insert after `Sunflowers` for the surface (the trees and plants come after
and respect the area). The names are the game's (`Final Cleanup`, `Micro
Biomes`...); look them up defensively, as another version may not have
them. In an existing world, the Example Mod's `/castle` command
([`ExampleStructureCommand.js`](../../samples/ExampleMod/content/Common/Commands/ExampleStructureCommand.js))
builds the castle under the player: in multiplayer the server builds it and
sends the area with `NetMessage.SendTileSquare`.

## Multiplayer: who runs what

Each device runs its own mods; the methods run where the game does that
thing. Checked on host and client by the `mpglobals` test:

| | Host (or single player) | Client |
|---|---|---|
| Globals' `SetDefaults` | yes | yes (builds what arrives over the network) |
| `OnSpawn` | whoever created it | whoever created it (the arrow they shot) |
| `AI`, `HitEffect` | yes | yes |
| `PreKill`, `OnKill` (NPC) | yes | no: drops belong to the server |
| `ModifyNPCLoot` | yes | yes (each side builds the table) |
| `OnWorldLoad`, `PostUpdateEverything` | yes | yes |
| `LoadWorldData`, `SaveWorldData` | yes | no: the file belongs to the server |
| `PreUpdateWorld`, `PostUpdateWorld`, `Pre/PostUpdateTime` | yes | no |
| `NetSend` (world, NPC) | sends | receives in `NetReceive` |
| `NetSend` (projectile) | sends its own and relays the clients' | sends its own |

## Network

In multiplayer, each device has **its own** state: a field the server changed
does not change by itself on the client. To carry mod data from one side to
the other, there are three paths, all shaped like tModLoader's.

### Along with what the game already syncs

Every so often the game sends the world (on joining and when something
changes, like a boss being defeated), each NPC and each projectile. The mod's
data goes right behind, along the same path and in the same order:

| Who | Writes | Reads | Goes with |
|---|---|---|---|
| `ModSystem` | `NetSend(writer)` | `NetReceive(reader)` | the world data, from the server to the clients |
| `GlobalNPC` | `NetSend(npc, writer)` | `NetReceive(npc, reader)` | each NPC, from the server to the clients |
| `ModNPC` | `SendExtraAI(writer)` | `ReceiveExtraAI(reader)` | same |
| `GlobalProjectile` | `NetSend(projectile, writer)` | `NetReceive(projectile, reader)` | each projectile, from whoever controls it |
| `ModProjectile` | `SendExtraAI(writer)` | `ReceiveExtraAI(reader)` | same |

```js
export class NetSlime extends GlobalNPC {
    InstancePerEntity = true;
    mark = 0;

    NetSend(npc, writer) {
        writer.Write(this.mark);
    }

    NetReceive(npc, reader) {
        this.mark = reader.ReadInt32();
    }
}
```

- Read **in the same order** you wrote.
- Changed it and want to send it now: `npc.netUpdate = true` (or
  `proj.netUpdate`), as in the game. For the world,
  `Terraria.NetMessage.SendData(7, ...)`, which is what the game does.
- The `writer` keeps JavaScript values as they are: number, text, boolean,
  array, plain object, and `Vector2` (becomes `{X, Y}`; read it with
  `ReadVector2()`). `WriteFlags(a, b, ...)` and `ReadFlags()` (an array) store
  several booleans.
- tModLoader's typed names (`Write((byte)x)` becomes `Write(x)`; `ReadByte`,
  `ReadInt32`, `ReadSingle`, `ReadString`...) exist so ported code goes
  through unchanged.

### Custom packets

For a message that does not ride along with anything of the game's (a
client's request, a server notice), your package's `Mod` sends and receives:

```js
// content/main.js
export default class MyMod extends Mod {
    HandlePacket(reader, whoAmI) {
        const request = reader.ReadString();
        if (request === 'ping') {
            const p = this.GetPacket();
            p.Write('pong');
            p.Send(whoAmI);   // only to whoever asked
        }
    }
}
// on the client, from any file in the mod:
const p = bl.mod.GetPacket();
p.Write('ping');
p.Send();                     // from the client, goes to the server
```

- `Send()` on the client goes to the server. On the server, `Send(client)`
  goes to one, and `Send(-1, ignore)` goes to everyone except `ignore`. In
  single player, it goes nowhere.
- `whoAmI` is the sender: on the server, the client's index; on the client,
  `256` (the server).
- The packet arrives in the `HandlePacket` of the **same mod** on the other
  side (by uid).

### How it travels

tModLoader writes inside the game's messages. Here, that would require
rewriting native code; the path is the game's own **NetModules** (message 82),
with a module number the game does not know. The content is what you wrote,
as JSON: up to ~64 KB per packet.

The cost: with some `NetSend` registered, messages 7, 23 or 27 enter JS (the
native filter leaves the others out); and each NPC or projectile the game
syncs carries one more packet. Keep what goes over the network small.

## Not yet

- `GlobalItem`'s `NetSend`/`NetReceive` (items on the ground and in the
  inventory);
- per-entity `SaveData`/`LoadData` (of an item or a town NPC);
- `GlobalWall`; on `GlobalTile`, the drawing and light methods,
  `NearbyEffects`, `TileFrame`, `CanReplace`/`ReplaceTile` and the
  spelunker/danger ones;
- on `GlobalNPC`: `UpdateLifeRegen`, `SaveData`/`LoadData`, the town NPC
  attacks (`TownNPCAttack*`) and the chat buttons;
- on `ModSystem`: the separate
  `Pre/PostUpdate` for players, NPCs, projectiles and items.
