# 3. Cost and performance

Every mod takes a little of each game frame's time. Most of the time that
little is so small it does not show. But a hook in the wrong place, or a loop
inside a method the game calls thousands of times per frame, can drop the game
from 60 to 20 frames per second. This guide explains **where the cost comes
from**, **how much each thing costs** and **how to write a lightweight mod**.

Prerequisite: [guide 1](01-hooks-do-zero.md), up to *Hooks*.

## The frame budget

Terraria updates and draws the world 60 times per second. That gives **16.6 ms
per frame** for everything: the game, Unity, drawing, and the mods. The game
itself already uses a good part of it: on the test emulator, the update
(`Main.DoUpdate`) takes ~3 ms and drawing (`Main.DoDraw`) ~6 ms, with bigger
spikes. What is left is the mods' headroom, and it is shared among **all**
enabled mods.

A useful way to think about it: **1 ms per frame is a lot** for a mod. 0.1 ms
is comfortable. 0.01 ms does not exist.

## Where the cost comes from

A mod's JavaScript runs in an interpreter (QuickJS, no JIT). And every access
to the game **crosses the bridge**: a JS name becomes a C# field, a C# value
becomes a JS value. Each crossing costs little, but it costs.

A hook adds four things to every call of the method:

1. **Dispatch**: the game calls the method, lands in Bunny Loader's function,
   which checks the filters, takes the JS engine (the lock) and builds the
   arguments;
2. **Arguments**: `self` and each game object become a JS object (a
   *wrapper*); numbers become numbers;
3. **Your callback**: the JS you wrote, and every game access inside it;
4. **`original()`**: releases the engine, runs the game's method, takes the
   engine back.

The **fixed** part (1, 2 and 4) is paid on every call, even if your callback
does nothing. That is why **how often** the method is called matters more than
anything else.

## How much each thing costs

Measured with [`tools/bench`](../../tools/bench), on the MuMu emulator (which
runs the game's ARM code translated to x86), in nanoseconds (ns; 1 ms =
1,000,000 ns), above the cost of an empty JS loop:

| Operation | ns | Example |
|---|---:|---|
| Read or write a field (`int`, `float`) | ~35 | `p.statLife`, `p.statLife = 400` |
| Read a static field | ~47 | `Main.maxTilesX` |
| Read an `int[]` element | ~48 | `Main.npcFrameCount[t]` |
| Read an element that is an object | ~64 | `Main.npc[i]` |
| The same, with an object JS did not have yet | ~146 | `Main.item[i]` sweeping 400 items |
| C# property (`get_X`) | ~85 | `Main.myPlayer` |
| `Main.player[0].whoAmI` (static + element + field) | ~157 | |
| Struct view | ~181 | `p.position.X` |
| Call a method stored in a variable | ~95 | `const f = Player['...']; f(p)` |
| Call a method looking up the signature every time | ~154 | `p['bool CanBePushedByWind()']()` |
| Static method with 2 floats | ~146 | |
| Create a JS object | ~180 | `{ x: 1, y: 2 }` |
| One JS loop iteration with one calculation | ~95 | `s = (s + i * i) % 1000003` |
| **Hook that only forwards to `original()`** | **~550–610** | per call |
| **Hook that does not call the original** | **~270** | per call |

> **The numbers are from the emulator.** MuMu runs the game's ARM code through
> translation, and the QuickJS interpreter is exactly the kind of code
> translation penalizes most. On a phone, the absolute values are different.
> What holds is the **ratio**: a hook costs the same as ~15 field reads; a
> struct view, the same as ~5.

With the game running (the world open, the screen drawing),
[`tools/tests/opcost`](../../tools/tests/opcost) measured other values for
what per-frame hooks usually do, also on MuMu. The most expensive:

| Operation | ns | Instead |
|---|---:|---|
| Item of a static array, looking up the array every time | ~2600 | Keep the array in a variable (~1300 per item) |
| `Color.new(r, g, b)` | ~3800–5400 | Create the color once and reuse it |
| `Vector2.new(x, y)` | ~1800–3000 | Same, when possible |
| `bl.tiles.typeAt(x, y)` | ~1800 | Once per cell, not per question |
| Path with a nested class (`Terraria.ID.ArmorIDs.Head.Sets`) | ~860 | Keep the class (`const Sets = ...`) outside the hook |

The `Terraria.X.Y` path was ~5000 ns until the bridge started caching each
class and namespace it had already resolved (2026-10-07).

A hook without `original()` costs less because it does not release and
retake the engine (see [why `original()` releases the engine](../nucleo/threads-e-motor-js.md#holding-the-engine-during-the-games-method)).

## How often the game calls each method

This is where the cost multiplies. Typical values, per frame:

| Method | Calls per frame | A forwarding-only hook costs |
|---|---|---:|
| `Main.DoUpdate`, `Main.DrawInterface` | 1 | ~0.6 µs |
| `Player.Update` | 1 per player in the world | ~0.6 µs per player |
| `NPC.AI`, `NPC.UpdateNPC` | 1 per active NPC (up to 200) | up to ~0.12 ms |
| `Projectile.AI`, `Projectile.Update` | 1 per active projectile (up to 1000), sometimes more (`extraUpdates`) | up to ~0.6 ms |
| `Item.SetDefaults` | 0 with the game idle; **thousands** when generating or loading a world | extra seconds while loading |
| Dust and particles (`Dust`) | thousands | ms per frame |
| `SpriteBatch.DrawString` | every text on screen: thousands | ms per frame |
| Light, collision, tiles | tens of thousands | **the whole frame** |

Example math, with the emulator numbers:

- A hook on `Player.Update` that reads 10 fields: `0.6 + 10 × 0.035 ≈ 1 µs`
  per frame. **Irrelevant**.
- An unfiltered hook on `NPC.AI`, with 150 NPCs, reading 5 fields from each:
  `150 × (0.6 + 5 × 0.035) ≈ 0.12 ms` per frame. **Acceptable**, but wasteful
  if only mod NPCs matter (see *Native filters*).
- An unfiltered hook on `SpriteBatch.DrawString`: ~3000 texts per frame on the
  inventory screen, `3000 × 0.6 ≈ 1.8 ms`. **Noticeable** in game.
- Sweeping `Main.npc` (201 slots) reading 3 fields from each: `201 × (0.064 +
  3 × 0.035) ≈ 34 µs`. Once per frame, fine. **Inside** an `NPC.AI` hook (one
  sweep per NPC), that is 200 sweeps: ~7 ms per frame.

The last one is the most common mistake: a loop that is cheap on its own,
inside a hook that runs for every entity, becomes quadratic.

## Mod classes are already economical

The methods of `ModItem`, `ModNPC`, `ModProjectile`, `ModTile`... only cost
something if your class **writes** them:

- the game hook behind each method is only **installed** when some registered
  class overrides that method;
- it is installed **once**, for every mod;
- almost all of them have a **native type filter**: an `NPC.AI` for a vanilla
  NPC does not even enter JS. Hitting dirt does not go through `ModTile`. A
  vanilla item in the inventory does not go through `UpdateInventory`;
- and, per method, only the **types that write it** get in: a projectile's
  `OnTileCollide` installs the movement hook, but the other mod projectiles,
  without `OnTileCollide`, go through it like vanilla ones. The same applies to
  the other `ModProjectile` methods, to `ModNPC`'s and to `ModItem`'s
  `GetAlpha`.

So writing an `AI(npc)` in your `ModNPC` only costs for **your** NPCs. If what
you want to do involves your own content, use the class instead of a direct
hook: it comes filtered already. The [class reference](../referencia/classes.md)
says, for each method, which game method is behind it and whether there is a
filter.

## Native filters

When you write a direct hook and only part of the calls matter, the native
filter decides **before** JS, with no lock and no wrapper, reading the
object's memory directly. Calls that do not pass cost almost nothing (one
memory read).

### `minType`: only entities from a type onward

```js
// only mod NPCs (their types start after the game's)
Terraria.NPC['void AI()'].hook(callback, { minType: bl.npcs.vanillaCount });

// only mod projectiles
Terraria.Projectile['void AI()'].hook(callback, { minType: bl.projectiles.vanillaCount });
```

`minType` compares `self`'s `type` field. With `on`, the filter looks at a
**parameter** (by index, starting at 0) instead of `self`; with `field`,
another `int` field:

```js
// ItemCheck_Shoot(int i, Item sItem, ...): the item is parameter 1
Terraria.Player['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)']
    .hook(callback, { minType: bl.items.vanillaCount, on: 1 });

// the grappling hook: what matters is item.shoot, not item.type
Terraria.Player['void FireGrapple(Item grappleItem)']
    .hook(callback, { minType: bl.projectiles.vanillaCount, on: 0, field: 'shoot' });

// the game calls DrawItem for the 400 ground slots: the type of the inner Item
Terraria.Main['void DrawItem(WorldItem item, int whoami)']
    .hook(callback, { minType: bl.items.vanillaCount, on: 0, field: 'inner.type' });
```

To find the heavy hook, `bl.hookStats()` tells how many calls each one
receives and how much time JS spent in it; the `tools/tests/frametime` mod
measures the frame and lists the most expensive hooks (in the log and in the
chat).

For tiles, the type comes from the **world**: `tile: i` (parameter `i` is a
`Tile`) or `tileAt: [i, j]` (parameters `i` and `j` are the position):

```js
Terraria.WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)']
    .hook(callback, { minType: bl.tiles.vanillaCount, tileAt: [0, 1] });
```

### `whileIn`: only inside another hook

For a method called thousands of times, but that only matters at one moment:
the callback only runs while the thread is **inside another method's hook**
(which needs a JS hook beforehand).

```js
const tooltip = Terraria.Main['void MouseText_DrawItemTooltip(Main.MouseTextCache info, int rare, byte diff, int X, int Y)'];
tooltip.hook((original) => original());

const drawString = Microsoft.Xna.Framework.Graphics.SpriteBatch['void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'];
drawString.hook(callback, { whileIn: tooltip });   // only the tooltip's texts
```

That is how Bunny Loader's colored tooltip changes `DrawString` without paying
for the thousands of other texts on screen.

### `flag`: only when the mod turns it on

For a hook that only has work to do now and then, and the mod knows when: a
global switch, off at the start. While off, the method runs without entering
JS.

```js
// my theme's sky: only in the menus, and only with the theme selected
Terraria.Main['void DrawSunAndMoon(SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)']
    .hook(drawSky, { flag: 'mymod.sky' });

bl.hookFlags.set('mymod.sky', true);    // when the theme is picked
bl.hookFlags.set('mymod.sky', false);   // when leaving the menus
```

Turning it on and off costs a native call; do it only when the state changes,
not every frame. Use a name with your mod's prefix: the switch is shared by
everyone.

## When the engine is busy: `ifBusy`

There is a single JS engine, and sometimes another game thread holds it (the
character save running a `ModPlayer`'s `SaveData`, world generation creating
items). A hook arriving at that moment **waits**, up to 3 s; if it cannot get
the engine, it runs the method without the mod and warns once in the log.

For a **per-frame** hook that can go **one frame without the mod**, not
waiting is better:

```js
// a fade: if a frame is skipped, nobody notices
Terraria.Main['void UpdateAudio()'].hook(fade, { ifBusy: 'original' });

// a decision the previous frame already made (void methods only)
Terraria.Main['void UpdateAudio_DecideOnNewMusic()'].hook(decide, { ifBusy: 'skip' });
```

| `ifBusy` | With the engine busy |
|---|---|
| `'wait'` (default) | Waits up to 3 s; then runs without the mod. |
| `'original'` | Runs only the game's method, right away. |
| `'skip'` | Runs nothing (`void` only): what the previous frame did stays. |

Do not use `ifBusy` on a hook that **has** to run (a save, damage, a recipe):
missing a call there is a bug. How it works inside is in
[threads and the JS engine](../nucleo/threads-e-motor-js.md#when-the-engine-is-busy-ifbusy).

## Recipes for a lightweight mod

**1. Get methods and classes once, outside the callback.**

```js
// good: resolved once
const Main = Terraria.Main;
const GetDamage = Terraria.Player['int GetWeaponDamage(Item sItem)'];
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    const d = GetDamage(self, self.inventory[self.selectedItem]);
});

// worse: looks up the signature every frame (~60% more expensive)
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    const d = self['int GetWeaponDamage(Item sItem)'](self.inventory[self.selectedItem]);
});
```

**2. Keep the struct view in a variable.** Each `npc.position` creates a view
(~180 ns). Read twice, that is two:

```js
const pos = npc.position;        // one view
if (pos.X > 100 && pos.Y < 500) { /* ... */ }
// instead of: npc.position.X > 100 && npc.position.Y < 500
```

**3. Bail out early.** What comes before the first `return` is what every
call pays:

```js
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer) return;   // the other players stop here
    // ...
});
```

**4. Do not create objects per call** if you can create one and reuse it.
`{ x, y }` costs ~180 ns; a game `Vector2.new(x, y)`, more.

**5. No per-entity loop inside a per-entity hook.** If each NPC needs to know
"the nearest player", compute it once per frame (in a `Main.DoUpdate` or
`Player.Update` hook) and store it.

**6. Spread heavy work out.** Sweeping the whole world (8400 × 2400 tiles in a
large world) does not fit in one frame. Do a chunk per frame, or do it in
`PostSetupContent`, during loading.

**7. Never `bl.log` every frame.** Each line goes to a file and to logcat. A
log per frame fills the file and costs disk writes. For debugging, log once
every N frames, or only when something changes.

**8. A repeated error costs too.** An error in a per-frame hook is logged **on
every call** (in mod class methods, only once). Besides the log, the
dispatcher runs the original for the broken callback: the game carries on, but
slower.

**9. An installed hook that is never called costs nothing.** No need to save
hooks on methods the game rarely calls (`Player.KillMe`, `WorldGen.KillTile`,
a UI event). Save them on per-frame ones.

## Measuring your mod

`performance.now()` gives the time in milliseconds, with a fraction:

```js
let total = 0, n = 0;
Terraria.NPC['void AI()'].hook((original, npc) => {
    const t0 = performance.now();
    original(npc);
    // ... your code ...
    total += performance.now() - t0;
    if (++n === 6000) {             // ~100 frames with 60 NPCs
        bl.log('AI: average ' + (total / n * 1000).toFixed(1) + ' us per call');
        total = 0; n = 0;
    }
});
```

To measure the bridge itself, the [`tools/bench`](../../tools/bench) mod runs
each operation 20 thousand times, takes the best of 7 and subtracts the empty
loop; this guide's results came from it. The emulator varies a lot between
runs (up to 70%): always compare by **alternating** the versions (before,
after, before, after), never one run against another from a different day.

## Summary

| Situation | What to do |
|---|---|
| Your own content (item, NPC, projectile) | Use the mod class: it comes filtered. |
| Direct hook on a per-entity method | `minType` (and `on`/`field`) if only mod content matters. |
| Method called thousands of times | `whileIn`, or do not hook it. |
| Per-frame hook that can skip one | `ifBusy: 'original'` or `'skip'`. |
| Inside the callback | Bail out early, keep methods and views, do not allocate, do not log. |
| Big work | A chunk per frame, or during loading. |
