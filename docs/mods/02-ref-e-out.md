# 2. `ref` and `out`

Many game methods return their result through a `ref` or `out` parameter,
not through `return`: the fishing roll, the spawn rate, collision when
stepping up a block, the tooltip lines. They are some of the best spots for a
mod to change, because that is how the game passes decisions from one method
to another.

In Bunny Loader you can **call** these methods and **hook** them, reading and
changing the parameter. This guide shows how, and explains the only trap: a
hook's `Ref` "comes loose" when the hook ends.

Prerequisite: [guide 1](01-hooks-do-zero.md), up to *Hooks*.

## What they are

In C#, a plain argument is a **copy**: the method changes it and the caller
does not see it. With `ref` or `out`, the method receives **the caller's own
variable**, and whatever it writes there sticks.

```csharp
int time;
Main.TryGetBuffTime(0, out time);       // the method writes to `time`

Collision.StepUp(ref position, ref velocity, ...);   // may change both
```

- **`out`**: one more return value. The method writes; the incoming value
  does not matter.
- **`ref`**: comes in with a value and may come out changed.
- **`in`**: comes in, read-only. Rare in the game.

Many good modding spots go through these parameters. Changing an `out` in a
hook is the equivalent of tModLoader's `ref int damage`.

## `Ref`

In JS, a `ref`/`out` parameter is a **`Ref`** object, with the value in
`.value`. `Ref` is global, like `Vector2`.

```js
const r = new Ref();      // empty: for `out`
const s = new Ref(10);    // with a value: for `ref`
r.value = 5;
bl.log(r.value, String(r));   // 5 "Ref(5)"
```

In the **signature**, write the parameter as in the dump. `out int`,
`ref Vector2` and `in T` all work:

```js
'bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'
```

## Calling a method with `ref`/`out`

Pass a `Ref` in place of the parameter. After the call, `.value` holds what
the method left there:

```js
const Main = Terraria.Main;
const time = new Ref();
if (Main['bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'](0, time)) {
    bl.log('the first buff still lasts', time.value, 'frames');
}
```

Several `out`s at once, each in its own `Ref`:

```js
const names = ['common', 'uncommon', 'rare', 'veryrare', 'legendary', 'crate'];
const outs = names.map(() => new Ref());
const bobber = Terraria.Projectile.new();
bobber['void .ctor()']();
bobber['void FishingCheck_RollDropLevels(int fishingLevel, out bool common, out bool uncommon, out bool rare, out bool veryrare, out bool legendary, out bool crate)'](30, ...outs);
names.forEach((n, i) => bl.log(n, outs[i].value));
```

Passing a number where the method wants `ref`/`out` throws, and the message
asks for `new Ref(...)`. An empty `Ref` goes into an `out` as zero.

## Hooking a method with `ref`/`out`

In the hook, the parameter arrives as a `Ref` **bound to the caller's
variable**:

- reading `.value` reads the game's variable;
- writing to `.value` changes what the game will use afterwards;
- pass the `Ref` to `original` as it came. It receives the real variable.

An `out` only has a value **after** `original`, as in C#: before that, it is
whatever was left in that memory.

### Changing a result: more crates when fishing

The game rolls what the bobber will catch in a method with six `out bool`s.
After the original, just flip `crate`:

```js
Terraria.Projectile['void FishingCheck_RollDropLevels(int fishingLevel, out bool common, out bool uncommon, out bool rare, out bool veryrare, out bool legendary, out bool crate)'].hook(
    (original, self, level, common, uncommon, rare, veryrare, legendary, crate) => {
        original(self, level, common, uncommon, rare, veryrare, legendary, crate);
        if (!crate.value && Math.random() < 0.10) crate.value = true;
    });
```

`self` is the bobber projectile. Its owner is `Terraria.Main.player[self.owner]`,
and you can require a buff or an accessory on them before changing anything.

### Reading and changing two results: spawn rate

The game calls this method on its own, every frame, to decide spawns near
each player. `spawnRate` is the interval between spawns (lower = more
enemies), and `maxSpawns` is the enemy limit near the player:

```js
Terraria.NPC.Spawner['void GetSpawnRate(Player player, out int spawnRate, out int maxSpawns)'].hook(
    (original, self, player, spawnRate, maxSpawns) => {
        original(self, player, spawnRate, maxSpawns);
        spawnRate.value = Math.floor(spawnRate.value / 2);
        maxSpawns.value *= 2;
    });
```

In multiplayer, the host does the spawning: the hook applies on the host's
device.

### Changing the input

With `ref`, the incoming value matters. Write to the `Ref` **before**
`original`, and the game's method already runs with the new value. After
`original`, you change what it returns.

### The hook sees the mod's calls

A hook also catches calls made by **the mod itself**: they go through the hook
like any other. With this hook, the `TryGetBuffTime` from the start of the
guide answers one second more:

```js
Terraria.Main['bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'].hook((original, slot, time) => {
    const r = original(slot, time);
    time.value += 60;   // whoever asked sees one second more
    return r;
});
```

### A real case: the tooltip

The mobile tooltip comes from a method with six `ref`s:
`Main.MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int
researchLine, ref int materialsLine, float oldKB, ref int numLines, string[]
toolTipLine, ...)`. It appends the lines to `toolTipLine` and returns how many
there are in `numLines`. By hooking it, you can insert a line right below the
name:

```js
Terraria.Main['void MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, ref int setBonusLine, ref Color setBonusColour)'].hook(
    (original, item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor) => {
        original(item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor);
        const n = numLines.value;
        for (let i = n; i > 1; i--) { lines[i] = lines[i - 1]; pre[i] = pre[i - 1]; bad[i] = bad[i - 1]; }
        lines[1] = 'Right below the name';
        pre[1] = bad[1] = false;
        numLines.value = n + 1;
        for (const r of [yoyo, research, materials, setBonus]) if (r.value >= 1) r.value += 1;
    });
```

In practice, a mod item does not need this: `ModItem`'s `ModifyTooltips` does
the same, with line names and color per line and per span ([guide 5, *Tooltip*](05-itens.md#tooltip)).

### Passing the `ref` on to another call

Inside the hook, you can pass the received `Ref` **directly** to another
method that also wants a `ref`/`out` of the same type, as in C#. The inner
method writes to the hook caller's variable, with no copy in between:

```js
Terraria.NPC['bool GetNPCLocation(int i, bool seekHead, bool averageDirection, out int index, out Vector2 pos)'].hook(
    (original, i, seekHead, avg, index, pos) => {
        return Terraria.Main['bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)'](0, index);
    });
```

Whoever called `GetNPCLocation` receives in `index` what `TryGetBuffTime`
wrote. The type has to match: passing `pos` (a `Vector2`) where the method
wants `out int` throws an error that states both types.

## Structs by `ref`

With a struct (`ref Vector2`, `ref FishingAttempt`), `.value` returns a
**copy**. Change the copy and write it back:

```js
Terraria.Projectile['void FishingCheck_ProbeForQuestFish(ref FishingAttempt fisher)'].hook((original, self, fisher) => {
    const a = fisher.value;       // copy
    a.crate = true;
    fisher.value = a;             // writes to the game
    original(self, fisher);
});
```

Changing `fisher.value.crate` directly does **not** change the game: it
changes a copy that disappears right after. It is the same care as with a
struct returned by a method in C#.

To call a method that wants a struct `ref`, start the `Ref` with a struct:

```js
const a = Terraria.DataStructures.FishingAttempt.new();
a.fishingLevel = 25;
const fisher = new Ref(a);
bobber['void FishingCheck_ProbeForQuestFish(ref FishingAttempt fisher)'](fisher);
bl.log(fisher.value.questFish);
```

## Why a hook's `Ref` "comes loose"

The caller's variable lives on its **stack**. When the method returns, that
address becomes garbage and is reused by another call. A mod that kept the
pointer and wrote to it later would corrupt the game's memory, and the game
would close with no explanation.

That is why, when the hook's callback ends, the `Ref` **comes loose**: it
keeps the last value and forgets the address. If you stored it in a variable,
it remains readable, and writing to it no longer touches the game.

```js
let last = null;
method.hook((original, x) => { original(x); last = x; });
// later: last.value is the value at the end of that call; last.value = 1 does nothing in the game
```

To change the game, do it **inside** the hook.

## Limits

- **Calling** with `ref`/`out` works on both bridge paths: the direct one
  (almost every method) and `runtime_invoke` (no compiled code of its own, or
  too many floats going on the stack, like
  `Collision.CheckAABBvLineCollision(..., ref float)`).
- Each `ref`/`out` counts as one integer argument (it is a pointer) for the
  hook limits: up to 16 integers counting `this`, and 8 floating point.
  `RollDropLevels`, with 9, fits.
- A struct's `.value` is always a copy: no direct view.

## Quick reference

| | |
|---|---|
| `new Ref()` / `new Ref(v)` | variable for `out` / for `ref` |
| `r.value`, `r.value = v` | read, write |
| `'... (out int x)'`, `'... (ref Vector2 v)'` | signature with `ref`/`out`/`in` |
| in the hook, the parameter | a `Ref` bound to the game's variable, until the callback returns |
| passing the hook's `Ref` on | the other call writes directly to the caller's variable |
| struct by `ref` | `.value` is a copy; write back with `.value = copy` |

The `tools/tests/refs` test covers every case in this guide: an `out` in a
call, a hook changing an `out`, fishing's six `out bool`s,
`ref FishingAttempt`, `GetSpawnRate` called by the game, `ref Vector2` in
`Collision.StepUp`, a `Ref` passed on to another call and a `Ref` after the
hook.
