# 3. `ref`, `out` and `Nullable`

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · **3. `ref`, `out`, `Nullable`** · [4. Values and objects](04-valores-e-objetos.md) · [5. C# × JS × tModLoader](05-comparacao.md) · [6. Performance](06-desempenho.md) · [7. Moving to C#](07-migracao-para-csharp.md) · [8. Under the hood](08-por-baixo-dos-panos.md) · [9. Content classes](09-conteudo.md)

Some game methods hand back more than one value, and some values may be
missing. C# has words for both, and in Bunny Loader they work exactly as in
any C# program, including inside hooks.

## `ref` and `out` in one minute

A normal parameter is a **copy**: the method gets the value and whatever it
does with it stays inside. A `ref` or `out` parameter is the **variable
itself**: what the method writes there, the caller sees.

| Keyword | Goes in with a value? | Comes out with a value? | Typical use |
|---|---|---|---|
| (none) | yes | no | normal input |
| `ref` | yes | yes (may change it) | "here is a value, adjust it" |
| `out` | no | **yes, always** | "give me back a second result" |
| `in` | yes | no (read only) | a big struct passed without copying |

The game uses `out` a lot for "try to get" methods:

```csharp
// bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)
if (Main.TryGetBuffTime(0, out int time))
    Logger.Info($"the first buff has {time} ticks left");
```

And XNA uses `ref` and `out` to avoid copying structs:

```csharp
var a = new Vector2(1, 2);
var b = new Vector2(3, 4);
Vector2.Dot(ref a, ref b, out float dot);   // dot = 11
```

Calling these is plain C#: write `ref` / `out` at the call, as always.

## Hooking a method with `ref` or `out`

In the hook, write the same keyword before the parameter's name. You do not
need to write its type:

```csharp
// void GetSpawnRate(Player player, out int spawnRate, out int maxSpawns)
// (NPC.Spawner) — how often and how many enemies spawn near the player.
On_NPC_Spawner.GetSpawnRate += (orig, self, player, out spawnRate, out maxSpawns) =>
{
    orig(self, player, out spawnRate, out maxSpawns);   // the game computes both
    spawnRate /= 2;        // a lower rate means MORE frequent spawns
    maxSpawns *= 2;        // and twice as many at once
};
```

Read it as a story:

1. `out spawnRate, out maxSpawns` in the lambda: "these two are outputs".
2. `orig(..., out spawnRate, out maxSpawns)`: the game fills them in.
3. Then you change them, and **the game sees your values** when the hook
   returns, because they are its own variables.

Two rules the compiler enforces for you:

- An `out` must have a value **before your hook returns**. Calling `orig`
  gives it one; if you skip `orig`, assign it yourself (`spawnRate = 600;`).
- Pass them on with the keyword: `orig(out x)`, `orig(ref y)`.

A method that returns **and** has an `out`:

```csharp
// bool TryGetBuffTime(int buffSlotOnPlayer, out int buffTimeValue)
On_Main.TryGetBuffTime += (orig, slot, out time) =>
{
    bool found = orig(slot, out time);
    if (found) time *= 2;      // buffs look like they last twice as long
    return found;
};
```

### `ref`: change what goes in **and** what comes out

```csharp
// The game probes for the quest fish with a FishingAttempt struct, by ref.
On_Projectile.FishingCheck_ProbeForQuestFish += (orig, self, ref fisher) =>
{
    orig(self, ref fisher);
    fisher.fishingLevel += 20;     // the game keeps using the changed struct
};
```

Changing `fisher` **before** `orig` changes what the game reads; changing it
**after** changes what the game finds when your hook returns.

### `in`

`in` is a `ref` you may only read. Write `in name` in the lambda and
`orig(in name)`.

## Methods of structs: `ref self`

When the method belongs to a **struct** (`Vector2`, `Rectangle`, `Color`...),
`self` is the struct itself, by `ref`:

```csharp
// bool Rectangle.Intersects(Rectangle value)
On_Rectangle.Intersects_Rectangle += (orig, ref self, other) =>
{
    if (self.Width == 0) return false;    // you can read and change self
    return orig(ref self, other);
};
```

For classes (`Player`, `Item`...), `self` is a normal parameter: a class is
already passed by reference (see [guide 4](04-valores-e-objetos.md)).

## `Nullable`: a value that may be missing

Some game methods take or return `int?`, `float?`, `Vector2?`: "a number, or
nothing". In Bunny Loader that type is written **`GameNullable<T>`**. It has
the same memory layout as the game's, and it converts to and from `T?` on its
own, so most of the time you just use `null` and plain values:

```csharp
// void NPC.ScaleStats(int? activePlayersCount, float? strengthOverride)
NPC npc = Main.npc[0];
npc.ScaleStats(5, 1.5f);      // pass values...
npc.ScaleStats(null, null);   // ...or "nothing"
```

Reading one:

```csharp
// Vector2? Player.HandPosition (a property)
GameNullable<Vector2> hand = player.HandPosition;
if (hand.HasValue)
    Logger.Info($"hand at {hand.Value}");

Vector2? asCSharp = hand;                          // to a normal C# nullable
Vector2 orZero = hand.GetValueOrDefault();          // or a fallback
```

| Member | |
|---|---|
| `HasValue` | `true` if there is a value. |
| `Value` | The value; throws if there is none. |
| `GetValueOrDefault(fallback)` | The value, or `fallback`. |
| implicit conversions | from `T`, from `T?`, from `null`, and to `T?` |

In a hook:

```csharp
On_NPC.ScaleStats += (orig, self, players, strength) =>
{
    if (strength.HasValue) strength = strength.Value * 2f;   // double it if given
    orig(self, players, strength);
};
```

And as an `out`:

```csharp
// Item GetEffectiveArmor(int slot, out int? sharedFromLoadout)
Item armor = player.GetEffectiveArmor(0, out GameNullable<int> shared);
```

> **Why not plain `int?`?** C# does not let `int?` be used where Bunny Loader
> needs raw memory (pointers, game arrays). `GameNullable<int>` is the same
> bytes, allowed everywhere. The conversions make the difference almost
> invisible.

## Coming from the JS bridge

| | JavaScript | C# |
|---|---|---|
| Call with `out` | `const t = new Ref(); f(0, t); t.value` | `f(0, out int t);` |
| Hook with `ref`/`out` | the callback receives `Ref` objects, `.value` | the parameter **is** the variable |
| Nullable | `null` or a number | `GameNullable<T>`, converts from `null` and values |
| Struct `self` | a view object | `ref self` |

Next: [4. Values and objects](04-valores-e-objetos.md).
