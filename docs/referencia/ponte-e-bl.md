# Reference: the bridge and `bl`

Everything a mod uses to talk to the game, on one page. Explained in depth in
[guide 1](../mods/01-hooks-do-zero.md); the mod classes are in
[classes](classes.md).

## The bridge

| Syntax | What it is |
|---|---|
| `Terraria.X.Y` | A class, through the namespace tree (`Terraria.ID.ItemID`, `Microsoft.Xna.Framework.Vector2`). |
| `GUIBuffs`, `GUIInstance` | A class without a namespace: global. |
| `Class.Nested` | A nested class (`SpriteFont.Glyph`, `Player.Hooks`). |
| `bl.classOf(ns, name)` | A class, when the tree does not reach it (`bl.classOf('', 'GUIBuffs')`). |
| `Class.field`, `Class.field = v` | A **static** field or property. |
| `obj.field`, `obj.field = v` | An **instance** field or property. The field's type decides the conversion. |
| `obj.nameThatDoesNotExist` | **Error**: `Member with name 'x' is not found in Class`, with the similar name if there is one. Names with C#'s case. |
| `'name' in obj` | Asks, without an error, whether the game class (or a mod, through `bl.defineField`) has the member. |
| `obj.position.X = 1` | A struct inside an object is a **view**: writing changes the game. |
| `proj.ai[0]`, `proj.oldPos[3].X` | A struct with numbered fields accepts an index. |
| `arr[i]`, `arr.length` | A game array. |
| `arr[x * columns + y]` | A game `T[,]` array (`TextureAssets.Players`): a single row, row by row, and `length` is the total. `arr[x, y]` in JS is the comma operator and becomes `arr[y]`. The dimensions: `System.Array['int GetLength(int dimension)'](arr, 1)`. |
| `arr.cloneResized(n)` | A copy of the array with `n` slots. |
| `arr.fill(v, start?, end?)` | Fills the array itself (changes the game) and returns it. |
| `arr.empty()` | A new array of the same type, with no slots. |
| `arr.find(fn)` | The first element with `fn(element, index, arr)` true, or `undefined`. |
| `[1, 2].makeGeneric('int')` | A JS array (or `Uint8Array`...) becomes a game array: `int[]`. Type by C# name, by full name or by class. |
| `Class.newArray(n)` | C#'s `new T[n]`: `Terraria.Item.newArray(10)`. With a list, already filled in. |
| `Class['ret Name(T a, U b)']` | A method, by the dump's **signature**, with the exact parameter names. |
| `Class.Name`, `obj.Name` | A method, if there is **only one** with that name. |
| `method(args)`, `obj['...'](args)` | Calling. In an instance method, the object is `this` or the 1st argument. |
| `Class.new()` + `['void .ctor(...)']()` | Creating an object (zeroed memory, then the constructor). |
| `Class.makeGeneric(T, ...)` | A generic class: `List.makeGeneric(Vector2)`. |
| `null` | Any object; also the `null` of a `Nullable<T>` (`float?`). |
| `null` in a struct with a default value | `Color newColor = default(Color)`: `null` or `undefined` is `default(T)`, zeroed, which is what the game receives when the parameter is omitted (it usually reads that empty value as "use mine"). Structs without a default value and numbers refuse `null`. |
| `new Ref(v)`, `r.value` | A `ref`/`out` parameter ([guide 2](../mods/02-ref-e-out.md)). |
| `method.hook(cb, options?)` | A hook ([guide 1](../mods/01-hooks-do-zero.md#hooks)). |

### Signatures

Copied from the dump as they are, with C# type names: `void`, `bool`, `int`,
`float`, `double`, `string`, `long`, `byte`, `short`, class names without a
namespace (`Item`, `Vector2`), arrays (`int[]`), generics (`List<Item>`),
`Nullable` in any spelling (`float?`, `Nullable<float>`, ``Nullable`1``) and
`ref`/`out`/`in` before the type. A constructor is `.ctor`.

**Each parameter's name** is part of the signature and has to be the game's,
with the same case: `PlayerDrawSet drawinfo` matches, `PlayerDrawSet drawInfo`
and `PlayerDrawSet` (with no name) do not. The error says which parameter
differs and shows the game's signature.

```js
'void SetDefaults(int Type, ItemVariant variant)'
'bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'
'void StartRain(bool instant, float? strengthOverride, bool garenteeCoinRain)'
'void .ctor(float x, float y)'
```

### `hook` options

| Option | Effect |
|---|---|
| `minType: N` | Only enters JS if `self`'s `type` is ≥ `N`. |
| `on: i` | The filter looks at parameter `i` (starting at 0), not at `self`. |
| `field: 'name'` | The filter looks at another `int` field. With a dot, it follows a reference first: `field: 'inner.type'` reads the `type` of the `Item` the `WorldItem` keeps in `inner`. |
| `field: 'array[index].field'` | Reads an integer field of an element of a reference array. The index comes from an `int` field of the object, or from a field of an embedded struct: `inventory[selectedItemState.selected].type`. Metadata and offsets are resolved at installation; a null array, an out-of-range index and a null element do not enter JS. |
| `tile: i` | Parameter `i` is a `Tile`; the filter looks at the block type. |
| `tileAt: [i, j]` | Parameters `i` and `j` are the position; the filter looks at the world's block. |
| `whileIn: method` | Only enters JS inside `method`'s JS hook (or `scope()`). |
| `ifBusy: 'wait' \| 'original' \| 'skip'` | With the JS engine on another thread: wait up to 3 s, run only the game, or nothing. |

Details in the [cost guide](../mods/03-custo-e-desempenho.md#native-filters).

`method.scope()` installs a hook with no callback: it marks natively that the
thread is inside the method and keeps the call's object, without entering JS.
`method.scopeSelf()` returns that object (the innermost call's), or `null`
outside it. It serves as `whileIn` for other hooks.

## `bl`: general

| | |
|---|---|
| `bl.log(...values)` | A line in the game's log and in logcat. JS objects and arrays come out as JSON. |
| `bl.error(...values)` | The same, at error level: opens the in-game error panel (with the option on). An error in a mod method (`ModItem.ModifyTooltips`, `ModNPC.AI`...) already comes out here, caught by the loader. |
| `print(...values)` | A line in the **game chat** (the values joined as in `bl.log`). From any thread; shows up on the next frame. |
| `bl.chat(text, color?)` | The same, with a color: `'#RRGGBB'`, `{ R, G, B }`, `[r, g, b]` or a `Color`. |
| `bl.mod` | The calling mod's `Mod`: `id`, `name`, `version`, `uuid`, `path`, `root`, `dataDirectory`. |
| `bl.info.appDirectory` | `Android/data/com.bunnyloader` (where `Players/`, `Worlds/` live). |
| `bl.info.logsDirectory` | The logs folder. |
| `bl.info.terrariaVersionCode` | The game version (301720 = 1.4.5.8.6). |
| `bl.onContentReady(fn)` | Calls `fn` when all mod content is in the game (the same moment as `PostSetupContent`). |

## `bl`: files

Relative paths are relative to the calling mod's `main.js` folder; absolute
ones are used as they are. For data the mod writes, use
`bl.mod.dataDirectory`: the package folder is replaced entirely when the mod
is updated.

| | |
|---|---|
| `bl.readJson(path)` | The parsed JSON, or `undefined`. |
| `bl.file.exists(path)` | |
| `bl.file.read(path)` | Text, or `undefined`. |
| `bl.file.readBytes(path)` | `Uint8Array`, or `undefined`. |
| `bl.file.write(path, text \| bytes)` | Creates the path's folders. |
| `bl.file.append(path, text \| bytes)` | |
| `bl.file.delete(path)` | `true` if it deleted. |
| `bl.directory.exists(path)` | |
| `bl.directory.create(path)` | Including the intermediate folders. |
| `bl.directory.delete(path)` | And everything inside. |
| `bl.directory.listFiles(path)` | The files (relative paths). |
| `bl.directory.listDirectories(path)` | The subfolders. |
| `bl.path.join(a, b, ...)` | |
| `bl.path.getName(p)`, `getParentPath(p)`, `getExtension(p)` | |

## `bl`: textures

Only on the **game thread** (in a hook, in `SetStaticDefaults`, in
`PostSetupContent`), never at the top of `main.js`.

| | |
|---|---|
| `bl.loadTexture(path)` | A PNG/JPG from the mod → the game's `Texture2D`, new on every call. |
| `bl.loadTextureAsset(path)` | A PNG/JPG from the mod → an already loaded `Asset<Texture2D>` (the type of the `TextureAssets` tables). |
| `ModContent.Request(path)`, `ModContent.Texture(path)` | The same, loaded **once** per file ([guide 4](../mods/04-conteudo-novo.md#modcontent)). |

## `bl`: fields and objects

| | |
|---|---|
| `bl.defineField(Class, 'name')` | A new field on every instance of the class (and its subclasses), kept next to the object. |
| `bl.defineMethod(Class, 'name', function () {...})` | A new method; `this` is the game object. |
| `bl.addressOf(obj)` | The object's address (a number). |
| `bl.objectAt(address)` | The object back. Only for those who know it still exists. |

## `bl`: content (low level)

The mod classes use these underneath; a mod rarely needs them directly.

| | |
|---|---|
| `bl.items.vanillaCount`, `bl.projectiles.vanillaCount`, `bl.npcs.vanillaCount`, `bl.buffs.vanillaCount`, `bl.tiles.vanillaCount` | The first mod type of each kind of content (6196, 1136, 697, 401, 754). Useful in `minType`. |
| `bl.items.isModItem(t)`, `bl.projectiles.isModProjectile(t)`, `bl.npcs.isModNpc(t)`, `bl.buffs.isModBuff(t)`, `bl.tiles.isModTile(t)` | Is it mod content? |
| `bl.items.typeOf('Class')` (and the others) | The type of content from **this** mod by name; -1 if there is none. |
| `bl.items.modItemsIn(player)` | The mod items in the player's 58 inventory slots. |
| `bl.items.worldItemOf(item)` | The `Main.item` `WorldItem` that holds this `Item` (`inner`), or `null`. A native scan of the 400 slots. |
| `bl.projectiles.latchedHooks(owner, except)` | The latched (`ai[0] == 2`) hooks (`aiStyle` 7) of player `owner`, minus the one with `whoAmI == except` (-1: all). A native scan of the 1000 projectiles. |
| `bl.npcs.freeSlot()` | The next free `Main.npc` slot. |
| `bl.tiles.typeAt(x, y)` | The type of the active tile at a position (-1 if there is none). |
| `bl.tiles.find(marks, x0, y0, x1, y1[, frames])` | The tiles in the rectangle whose type is marked in `marks`: `[i, j, ...]`, or `[i, j, frameX, frameY, ...]` with `frames`, read straight from the world's memory. |
| `bl.tiles.addLights([i, j, r, g, b, ...])` | Each tile's `Lighting.AddLight` in a native loop (one trip to the bridge for all of them). |
| `bl.tiles.setAnimationFrameHeight(type, height)`, `bl.tiles.onDrawData(fn)` | `AnimationFrameHeight` and the drawing hook (`ModTile` uses them). |
| `bl.tiles.setDoor(closed, open)` | A mod door's pair, for the player who walks into it and for NPCs to open it (`ModTile` calls it through `OpenDoorID`/`CloseDoorID`). |
| `bl.hookMarks.set(name, type[, on])`, `bl.hookMarks.has(name, type)` | The marks for the hooks' `marks` filter ([core](../nucleo/hooks.md#native-filters)). |
| `bl.hookFlags.set(name, on)`, `bl.hookFlags.get(name)` | The switch for the hooks' `flag` filter: off (the start), the method runs without entering JS ([core](../nucleo/hooks.md#native-filters)). |
| `bl.hookStats()` | Each installed JS hook: `{ name, calls, js, jsMs }` since installation. `calls` counts every call that went through the dispatch (including the ones the filter handed back to the game), `js` the ones that reached the callback and `jsMs` the time inside it without `original()`. For measuring: `tools/tests/frametime` uses it. |
| `bl.gc()`, `bl.gcThreshold()` | Runs a QuickJS cycle collection and returns how long it took (ms); the memory threshold for the next collection (it changes on each one: counting the changes gives the collections). For measuring only. |
| `bl.items.register`, `bl.npcs.register`... | The native registration. Use the classes (`ModItem.register`). |

## `bl.menu`: the Mod Menu

| | |
|---|---|
| `bl.menu.itemCategory(name, icon)` | An items folder in the mod's entry. `icon`: the path of a PNG in the mod. |
| `bl.menu.npcCategory(name, icon)` | An NPCs folder. |
| `bl.menu.addItem(folder, type)`, `bl.menu.addNpc(folder, type)` | Puts it in the folder. |
| `bl.menu.hide('item' \| 'npc' \| 'buff', type)` | Removes it from the Mod Menu (the same as `HideFromModMenu`). |
| `bl.menu.isHidden('item' \| 'npc' \| 'buff', type)` | |

## JavaScript globals

Besides standard JavaScript (ES2020):

| | |
|---|---|
| `performance.now()` | Time in ms, with a fraction: for measuring. |
| `print(...)` | The game chat (see above). |
| `import`/`export` | Between files of the same mod, with a relative path. |
| `Terraria`, `Microsoft`, `System`, `ReLogic`... | The game's namespace roots. |
