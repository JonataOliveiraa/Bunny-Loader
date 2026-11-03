# 7. Migration to C#

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · [3. `ref`, `out`, `Nullable`](03-ref-out-e-nullable.md) · [4. Values and objects](04-valores-e-objetos.md) · [5. Bunny Loader × tModLoader](05-comparacao.md) · [6. Performance](06-desempenho.md) · **7. Migration to C#** · [8. Under the hood](08-por-baixo-dos-panos.md) · [9. Content classes](09-conteudo.md)

Up to version 0.3.3, Bunny Loader ran mods in **JavaScript**, on the QuickJS
engine. Since 0.4.0 it runs **only C#**. This page is the one place in the
documentation about that change: why it happened, what was measured, how to
port a JavaScript mod and what is left of the migration. Everything else about
the JavaScript engine is in the [history](../historico/README.md#the-javascript-engine).

| | |
|---|---|
| Last version with JavaScript | the `js-final` tag; fixes to it go on the `release-js` branch |
| The JavaScript guides and reference | [`historico/mods-js`](../historico/mods-js/README.md), [`historico/referencia-js`](../historico/referencia-js/metodos.md) |
| The migration plan, phase by phase | [`historico/plano-csharp.md`](../historico/plano-csharp.md) |
| A JavaScript package on 0.4.0 | the launcher refuses to import it; an installed one is skipped with "JS mods are no longer supported" |

## Why C#

- **Speed.** C# is compiled to machine code; QuickJS interprets. Mod code
  runs faster, and reading the game is a memory read instead of a trip
  through a bridge (numbers below).
- **The compiler catches mistakes.** A wrong name or type never reaches the
  phone.
- **tModLoader is C#.** The API has tModLoader's names and shapes, so a
  tModLoader mod ports by copying, plus the
  [mobile differences](05-comparacao.md).
- **One engine.** Two runtimes meant two APIs, two sets of loaders and two
  test suites for the same features.

## Measured on the same phone

Both versions on a **Samsung Galaxy A15 (SM-A156M)**, on 2026-11-03: release
0.3.3 (QuickJS, commit `11a9da4b`) with that commit's test packs and
ExampleMod, and 0.4.0 (C#) with `ExampleModCSharp` already compiled. Same flat
test world, same character, hostile NPCs removed on both sides. "Mod code" is
the time spent inside mod hooks per frame.

| | JavaScript (0.3.3) | C# (0.4.0) |
|-|---:|---:|
| No mods: frame / `DoUpdate` / `DoDraw` | 16.66 / 2.71 / 5.10 ms | 16.62 / 2.67 / 5.11 ms |
| No mods: memory (PSS) | 870-905 MB | 930-985 MB |
| Entering a hook, isolated (once per frame) | 10.8 µs | 12.5-17.5 µs |
| Entering a hook, right after another | 1.85 µs | 2.1-3.9 µs |
| Entering a hook, in a burst (400 per frame) | 0.87 µs | 0.53-0.77 µs |
| ExampleMod idle: mod code | 3.04 ms/frame (54 hooks) | 1.79 ms/frame (86 hooks) |
| ExampleMod idle: `DoUpdate` / `DoDraw` | 3.57 / 5.73 ms | 3.06 / 5.60 ms |
| ExampleMod idle: the mod pylon check | 320 µs | 9.5 µs |
| ExampleMod idle: memory (PSS) | 1.06 GB | 1.30 GB |
| ExampleMod: loading the mods | 1.3 s | 6.5 s (plus 6.3 s the first time, compiling) |
| 60 projectiles + 15 NPCs: mod code | 7.50 ms/frame | 4.10 ms/frame |
| 60 projectiles + 15 NPCs: `DoUpdate` / `DoDraw` | 5.59 / 8.94 ms | 5.37 / 6.42 ms |
| Per projectile: draw hook / AI hook | 49 / 14 µs | 4.2 / 8.2 µs |

All runs held 60 fps. The two ExampleMods are not the same mod (the
JavaScript one had 232 files, the C# port 136), so the idle rows compare the
same kind of mod, not the same work; the heavy rows use the same entities
(`SparklingBall` and `ExampleCustomAISlimeNPC`).

**Small operations** (the same cases in `benchjs`, from `js-final`, and
`benchcs`; median of three rounds minus an empty loop):

| Case | C# | JavaScript | C# is |
|---|---:|---:|---:|
| Integer loop, 1 million iterations | 2.3 ms | 67.8 ms | **29× faster** |
| `fib(20)`, recursive | 82 µs | 1,488 µs | **18×** |
| Allocate 100,000 small objects | 2.1 ms | 25.1 ms | **12×** |
| `sin` + `sqrt`, 100,000 times | 4.9 ms | 19.9 ms | **4×** |
| Map/Dictionary, 10,000 set + get | 1.2 ms | 4.7 ms | **4×** |
| String building, 1,000 parts | 85 µs | 330 µs | **4×** |
| Sort 10,000 integers | 1.1 ms | 3.9 ms | **3.6×** |
| `new Vector2(1, 2)` | 2.9 ns | 1,320 ns | **457×** |
| `new Color(1, 2, 3)` | 26 ns | 3,235 ns | **123×** |
| `p.position.X` | 5.4 ns | 265 ns | **49×** |
| `p.head` | 5.0 ns | 73 ns | **15×** |
| `p.statLife = p.statLife` | 11 ns | 152 ns | **13×** |
| `p.inventory[p.selectedItem].type` | 103 ns | 734 ns | **7×** |
| `Main.player[Main.myPlayer]` | 106 ns | 545 ns | **5×** |
| `Vector2.Distance(a, b)` | 106 ns | 572 ns | **5.4×** |
| `p.Center` | 82 ns | 427 ns | **5.2×** |
| `WorldGen.InWorld(x, y, 0)` | 211 ns | 254 ns | 1.2× |
| `Lang.GetItemNameValue(98)` | 314 ns | 343 ns | 1.1× |
| `p.CountBuffs()` | 339 ns | 261 ns | 0.8× (JavaScript wins) |
| A pass-through hook, per call | 397 ns | 609 ns | 1.5× |
| `Matrix.Add(a, b)` (64-byte struct) | 141 ns | not possible | |

**Where C# loses:** loading the mods takes about five times longer, the
process uses about 240 MB more with the ExampleMod (the .NET runtime and the
mod assemblies), and entering one isolated hook costs a little more.

## Porting a JavaScript mod

The class shapes are the same (`ModItem`, `ModNPC`, `ModSystem`...): what
changes is the language and how the game is reached.

| JavaScript | C# |
|---|---|
| `export default class M extends Mod { Load() {} }` | `public class M : Mod { public override void Load() {} }` |
| `export class Sword extends ModItem { SetDefaults() {} }` | `public class Sword : ModItem { public override void SetDefaults() {} }` |
| `const F = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)']` | nothing: `On_Item.SetDefaults` or `item.SetDefaults(...)` |
| `F.hook((original, self, a, v) => { original(self, a, v); })` | `On_Item.SetDefaults += (orig, self, a, v) => { orig(self, a, v); };` |
| `bl.log('x')`, `bl.error('x')` | `Mod.Logger.Info("x")`, `Mod.Logger.Error("x")` |
| `bl.loadTextureAsset('Content/X.png')` | `Mod.Assets.Request<Texture2D>("Content/X")` |
| `bl.vanillaTexture('Images/Item_1')` | `ModContent.Request<Texture2D>("Terraria/Images/Item_1")` |
| `bl.readJson('data.json')`, `bl.file(...)` | `Mod.GetFileBytes("data.json")`, `Mod.GetFileStream(...)` |
| `bl.items.typeOf(MyItem)` | `ModContent.ItemType<MyItem>()` |
| `Terraria.ID.ItemID.Minishark` | `ItemID.Minishark` (`using Terraria.ID;`) |
| `Vector2.new(1, 2)`, `Color.new(255, 0, 0)` | `new Vector2(1, 2)`, `new Color(255, 0, 0)` |
| `const r = new Ref(); f(0, r); r.value` | `f(0, out int r);` |
| `null` for a `float?` | `null` (a `GameNullable<float>`) |
| `main.js` as the entry | `<Id>.cs` (sources) or `<Id>.dll` at the package root ([guide 1](01-primeiro-mod.md)) |
| texts in the manifest or in JSON | `Localization/<culture>_Mods.<Id>.hjson` ([guide 9](09-conteudo.md)) |
| editing on the phone | the BL Editor, or a PC with `dotnet build` |

**Saves carry over.** A port that keeps the package `uid` and the class names
keeps its players' and worlds' data, because the C# loaders read the same
files under the same keys:

| Data | Where, for both |
|---|---|
| Mod items (inventory, chests, the ground) | the item sidecars, by mod and name; each item's data is the same JSON |
| `ModPlayer` data | `<file>.plr.bl.json`, under `<mod uid>/<class>` |
| `ModSystem` world data | the world's sidecar, under `<mod uid>/<class>` |
| Mod tiles | `.wld.tiles.bl`, by `<mod uid>/<tile name>`; the numeric type may change, the key does not |
| Mod walls | `.wld.walls.bl`, by mod and name |
| Banner kill counts | `.wld.bl.json`, under `<mod uid>/<NPC name>` |
| Mod hairs | `bunny:hair` in `.plr.bl.json` |
| Prefixes and rarities | saved by name, so their numbers may change freely |

**Some things are not ported as they were:**

- `bl.*` helpers have no single replacement: each one maps to the tModLoader
  member that does the same job (table above, and the guides).
- A JavaScript mod and a C# mod cannot call each other; there are no
  JavaScript mods left to call.
- Native hook filters from `.hook(cb, { minType })` are not exposed: the C#
  loaders install their own, and a mod's `On_` hook filters with an early
  `return` ([Performance](06-desempenho.md#making-a-fast-mod)).

## What is left of the migration

The C# API covers what the JavaScript loaders did (items, players, buffs,
projectiles, NPCs, tiles, world generation, systems, biomes, menus, sounds,
configs, UI, touch buttons, screen effects, fonts). What remains before a
release:

- [ ] `samples/ExampleModCSharp` covering everything the JavaScript Example Mod
  covered: the boss, pets, minions and sentries, most furniture, prefixes,
  resource bars, structures and a few weapons (the laser, the rocket
  launcher, the golf ball).
- [ ] The other samples in C#: HelloMod, DobroDeDano, SemQueda, TemploPiramide,
  VidaCheia. Until then, the JavaScript ones stay in `samples/`.
- [ ] The catalog: our own mods ported, JavaScript mods flagged with the app
  version they need, the authors of the others contacted with this guide, and
  the online catalog reconnected (`RemoteCatalog.ENABLED`).
- [ ] `tools/mods` (`pack.py`, `publish.py`, `port_textures.py`) without
  `main.js`; the JavaScript API catalog in `tools/docs` retired.
- [ ] ProxyGen: typed `List<T>` and `Dictionary<TKey, TValue>` (today
  `GameList`/`GameDictionary` handles).
- [ ] Loading time and memory (the two rows above where C# loses).

Next tab: [8. Under the hood](08-por-baixo-dos-panos.md).
