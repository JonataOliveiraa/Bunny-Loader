# 6. Performance

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · [3. `ref`, `out`, `Nullable`](03-ref-out-e-nullable.md) · [4. Values and objects](04-valores-e-objetos.md) · [5. Bunny Loader × tModLoader](05-comparacao.md) · **6. Performance** · [7. Migration to C#](07-migracao-para-csharp.md) · [8. Under the hood](08-por-baixo-dos-panos.md) · [9. Content classes](09-conteudo.md) · [API reference](referencia/README.md)

Measured, not guessed: each case runs in the game on a **Samsung Galaxy A15
(SM-A156M, Android 16)**, three rounds; the tables show the median, minus the
cost of an empty loop.

## The numbers

**Your own code (no game involved):**

| Case | Time |
|---|---:|
| Integer loop, 1 million iterations | 2.3 ms |
| `fib(20)`, recursive | 82 µs |
| Allocate 100,000 small objects | 2.1 ms |
| `sin` + `sqrt`, 100,000 times | 4.9 ms |
| Map/Dictionary, 10,000 set + get | 1.2 ms |
| String building, 1,000 parts | 85 µs |
| Sort 10,000 integers | 1.1 ms |

**Reading and writing the game:**

| Case | Time |
|---|---:|
| `new Vector2(1, 2)` | 2.9 ns |
| `new Color(1, 2, 3)` | 26 ns |
| `p.position.X` (a struct field in an object) | 5.4 ns |
| `p.head` (an `int` field) | 5.0 ns |
| `p.statLife = p.statLife` (write) | 11 ns |
| `p.inventory[p.selectedItem].type` | 103 ns |
| `ItemID.Sets.Deprecated[5]` (static array) | 6.9 ns |
| `Main.player[Main.myPlayer]` | 106 ns |
| `Main.myPlayer` (a static property) | 94 ns |

**Calling the game's methods:**

| Case | Time |
|---|---:|
| `Vector2.Distance(a, b)` | 106 ns |
| `p.Center` (property, returns a `Vector2`) | 82 ns |
| `MathHelper.Clamp(x, 0, 10)` | 74 ns |
| `Matrix.Add(a, b)` (64-byte struct) | 141 ns |
| `WorldGen.InWorld(x, y, 0)` | 211 ns |
| `Lang.GetItemNameValue(98)` (returns a string) | 314 ns |
| `p.CountBuffs()` | 339 ns |

**Hooks:**

| Case | Time |
|---|---:|
| Cost a pass-through hook adds to each call | **397 ns** |
| The same, on a method with `ref`/`out` | 384 ns |

The same 43 correctness checks (calls, hooks, `ref`, `out`, `Nullable`,
structs, strings, exceptions) pass on this phone and on the emulator.

## Why reading the game is cheap

- **Your code is machine code.** The .NET runtime compiles your C# to ARM the
  first time a method runs; after that the processor runs it directly.
- **A game field is one memory read.** `p.head` becomes "read 4 bytes at the
  player's address + 0x634".
- **A `Vector2` is just two floats on the stack**, as in any C#.

## Why calling the game is not much faster

Every C# call into the game crosses from the .NET world into the game's
native code. The crossing has two parts:

```mermaid
flowchart LR
    C["your C#"] -- "~130 ns<br/>the .NET runtime's<br/>thread switch" --> G["guard against the<br/>game's exceptions<br/>~70 ns"]
    G --> M["the game's method<br/>(InWorld: ~7 ns)"]
```

1. **The runtime's switch (~130 ns).** Before running non-.NET code, the
   .NET runtime marks the thread as "outside .NET", so its garbage collector
   does not wait for it. That is what keeps a long game method from freezing
   the runtime.
2. **The guard (~70 ns).** Arguments go into registers, and the call runs
   inside a guard that turns a game exception into a `GameException` instead
   of a crash.

For **XNA math** (`Vector2`, `Matrix`, `Color`, `MathHelper`...) and
**property getters** (`p.Center`, `Main.myPlayer`), Bunny Loader skips part 1:
they are short, never wait for anything, and never call mod code. That is why
`p.Center` costs 82 ns and `p.CountBuffs()` 339 ns.

## Making a fast mod

**Do the work in C#, cross into the game less.** Reading fields is nearly
free; calling methods is not.

```csharp
// Slower: one game call per NPC (Entity.Distance), ~300 ns each.
for (int i = 0; i < Main.npc.Length; i++)
{
    NPC npc = Main.npc[i];
    if (npc.active && npc.Distance(target) < 400f) near++;
}

// Faster: only fields (~5 ns each) and the math in C#.
for (int i = 0; i < Main.npc.Length; i++)
{
    NPC npc = Main.npc[i];
    if (!npc.active) continue;
    Vector2 center = npc.position + new Vector2(npc.width, npc.height) / 2f;
    Vector2 d = center - target;
    if (d.X * d.X + d.Y * d.Y < 400f * 400f) near++;
}
```

**Filter early in hooks.** A hook on a method the game calls hundreds of times
per frame costs ~0.4 µs per call even if it does nothing:

```csharp
On_Item.SetDefaults += (orig, self, type, variant) =>
{
    orig(self, type, variant);
    if (type != ItemID.Minishark) return;   // the common case leaves at once
    ...
};
```

**Budget it.** A frame at 60 fps has 16,600 µs. 1,000 hook calls per frame
are about 400 µs, 2.4% of it. 20,000 would be half the frame.

**Hook only what you need.** The first hook on a method patches the game, and
the patch stays even after you remove the hook: the calls keep passing
through it.

**Allocate little every frame.** C# objects are cheap (21 ns each above), but
each one is work for the .NET garbage collector later. Prefer structs
(`Vector2`, your own `struct`s) and reuse lists.

**Cache what you look up.** `ItemID.Minishark` is a constant (free).
`Lang.GetItemNameValue(98)` is a game call: if you need it every frame, keep
the string.

## Measuring your own mod

```csharp
using System.Diagnostics;

long t0 = Stopwatch.GetTimestamp();
// ...the code you want to measure, many times...
double us = Stopwatch.GetElapsedTime(t0).TotalMicroseconds;
Logger.Info($"took {us:0.0} µs");
```

The benchmark above is in the repository: `tools/tests/benchcs`.

Next tab: [7. Migration to C#](07-migracao-para-csharp.md).
