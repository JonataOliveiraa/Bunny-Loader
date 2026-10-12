# Assessing the JS → IL2CPP bridge, and the crash when creating a world

Round of 2026-09-23, on MuMu (x86_64 translating ARM through houdini), Terraria
1.4.5.6.4, `libbunny` built with `-O2` (it also applies to the debug APK:
checked in `compile_commands.json`). Raw data in
[`dados/2026-09-23/`](dados/2026-09-23/); tools in
[`tools/bench`](../../tools/bench), [`tools/crash-trials`](../../tools/crash-trials)
and [`tools/disasm`](../../tools/disasm).

> **MuMu is not the device.** Everything here runs translated from ARM, and
> QuickJS's interpreter (indirect dispatch on every bytecode) is exactly the
> kind of code translation penalizes most. The absolute numbers are for
> comparing with each other; on a phone they should be 5 to 10 times smaller.
> Measuring on real ARM is still missing.

## Part 1 — performance

### How it was measured

[`tools/bench`](../../tools/bench) is a mod: on the player's first update
inside the world, on the game thread, it runs each operation 20,000 times
(best of 3) and subtracts an empty loop. The hook's cost is (call with a hook)
− (the same call without a hook), measured before and after installing hooks
that only forward to the original.

### Result

Empty loop (`s += i`): **55 ns** per iteration. Cost *above* it:

| Operation | ns/op | Note |
|---|---:|---|
| `p.statLife` (read an int field) | 60 | crossing the bridge costs the same as a JS iteration |
| `p.statLife = v` (write) | 63 | |
| `p.moveSpeed` (read a float) | 54 | |
| `Main.maxTilesX` (static field) | 65 | |
| `Main.myPlayer` (C# property, direct call) | 121 | |
| `f(p)`, a method kept in a variable | 112 | |
| `p['bool CanBePushedByWind()']()` every time | **402** | +290 ns to build the method object on every access |
| static method, 2 floats | 216 | |
| `bt[0]` (an `int[]` element) | **179** | 3× a field |
| `players[0]` (an element that is an object) | **556** | creates a wrapper + `gchandle` |
| `p.position.X` (struct view) | **488** | creates a JS object per access |
| `Main.player[0].whoAmI` | **1039** | two wrappers with `gchandle` |
| pure JS arithmetic, per iteration | 128 | the interpreter |
| `{x, y}` (allocating a JS object) | 261 | |
| **static hook (2 floats) that forwards** | **+829** | per call |
| **instance hook (`self`) that forwards** | **+1400** | per call |

### What the numbers say

1. **Fields and properties are fine.** Reading or writing a field costs the
   same as an empty loop iteration. The member cache in
   `script/bridge/Members.cpp` works.
2. **What is expensive is creating wrappers.** Every time a game object enters
   JS (an array element, the hook's `self`, a method's return) a JS object, a
   `Pinned` on the C++ heap and a `gchandle` are created, and they all die at
   the end. That is what puts `players[0]` at 556 ns and accounts for about 570
   of the instance hook's 1400 ns (1400 − 829).
3. **Three easy wastes to remove:**
   - `obj['signature']` builds a new `GameMethod` on every access
     (`makeGameMethod`: `malloc` + JS object + `method_get_flags`). Keeping one
     per `MethodInfo` eliminates the ~290 ns.
   - An array index converts the atom to a string, then to `std::string`, then
     `strtol`, and asks IL2CPP for the element's class on every access
     (`ga_exotic_get`). An integer atom is the common case and can be read
     directly; the element type can live in the array's `Pinned`.
   - The struct view creates an object per read of `p.position`.
4. **The hook is what decides the frame.** At +1.4 µs per call on MuMu: a hook
   on `Player.Update` (1 per frame) does not show; one on `Projectile.AI` with
   300 projectiles costs ~0.4 ms; a method called 10,000 times per frame blows
   the 16.6 ms budget by itself. It is not QuickJS that weighs there, it is
   what the bridge does around it: two wrappers with `gchandle`, the engine
   lock, and `original()` releasing and taking the lock again.

### Improvements, in order of payoff

1. **A borrowed wrapper in the hook**: `self` and object arguments without a
   `gchandle` during the callback (the object is alive, the game holds it),
   promoting to pinned only if JS keeps the reference (refcount > 1 at the end
   of the callback). Likewise for array elements. Should remove about half of
   the instance hook's cost.
2. **Caching `GameMethod` per `MethodInfo`** (−290 ns per call through a
   property).
3. **A fast integer index path** on arrays, with the element type stored.
4. A pool of struct views.

### Would V8 be worse?

**On the bridge, it would not be better, and would probably be a bit worse.
In pure JS, it would be much better.**

- The cost here is **crossing the boundary**, not running JS. In V8, the
  equivalent of our exotic getter is an *interceptor*, which takes the access
  off the optimized path (no *inline cache*, one C++ call per access). Calling
  JS from C++ requires a `HandleScope`, entering the context and, with several
  threads, `v8::Locker`. Per crossing, V8 is in the same range as QuickJS or
  above.
- Where V8 really wins is in the JS body: the 128 ns per iteration of
  interpreted arithmetic become ~1 ns with a JIT. For a mod that does heavy
  math (a world generator in JS, complex AI), it would make a difference; for
  the typical mod, which reads and writes game fields, almost none.
- The only way for V8 to also win at the boundary would be to redesign the
  bridge: generate JS accessors that read the object's memory through a
  `DataView` over the IL2CPP heap, which the JIT turns into a load
  instruction. That is another architecture, with its own risks (an object the
  collector moves or reclaims underneath the `DataView`).
- V8 costs that do not exist today: +7 to 10 MB of library per ABI, a much
  larger base memory, a heavy build (gn/depot_tools), a JIT generating ARM code
  that houdini has to translate at runtime on the emulator. And a serious
  problem for this bridge: V8 only frees wrappers when its collector runs.
  QuickJS frees them **right away** (refcount), and that is what returns
  IL2CPP's `gchandle`s immediately. With V8 the `gchandle`s would pile up
  between collections.

**Conclusion:** do not switch engines. The bottleneck is the bridge's design,
and it gets fixed in QuickJS (list above). If there is ever a mod that needs
heavy JS, the alternative to evaluate is V8 without the JIT (`--jitless`) or
Hermes, not for boundary speed.

## Part 2 — crash when creating a world

> **Update (2026-09-24): cause found and fixed** — a dangling reference in the
> hooks' `original()` (`script/bridge/JsHook.cpp`): with nested hooks on the
> same thread, the result was written to already-freed memory. See
> [`PONTE-OTIMIZACAO.md`](PONTE-OTIMIZACAO.md), step 2 (0 crashes in 10 rounds
> after the fix). `JsSuspend`, which I had pointed at, was innocent. The text
> below is the investigation as it stood before that.

### Symptom

When tapping **Create** on a new world, the game "freezes": actually the
thread failed with SIGSEGV and got stuck in a loop of repeated signals
(houdini forwards the signal every ~10 ms) until the app was closed.

### Where it fails

Always the same place, inside the IL2CPP runtime:

```
#00 libil2cpp.so +0x6ac7ac   ldr x9, [x2]    x2 = 0   (two-pointer key comparator)
#01 libil2cpp.so +0x6e7868   lookup in a hash table
#02 libil2cpp.so +0x6e773c
#03 libil2cpp.so +0x6d9d4c
 ... recursive generic type inflation ...
    libil2cpp.so +0x6e945c   Runtime::ClassInit (the function called from 549 places)
```

It is the runtime's table of generic instances, with a null-key bucket: the
classic symptom of one thread reading the table while another rebuilds it.
Registers `x6 = 0xfefefeff…` and `x7 = 0x7f7f7f7f…` are the constants of
libc's string routines, which helped find the function.

In two crashes, two different threads fell at the same point:

1. **Main thread**, inside our hook
   ([`bunny_crash1-logcat.txt`](dados/2026-09-23/bunny_crash1-logcat.txt)):
   `Main.Draw` → JS hook on `Main.DoDraw` → `JS_Call` → `original()` →
   `callRaw` → `Main.DoDraw` → `DrawMenu` → `GUIStatusMenu.Draw` →
   `GameTipsDisplay.AddNewTip` → `LanguageManager.RandomFromCategories` →
   `Dictionary.get_Keys` → `ClassInit` → **fails**.
2. **The game's own world generation thread**, with no frame of ours
   ([`cfgD_crash1-logcat.txt`](dados/2026-09-23/cfgD_crash1-logcat.txt)):
   `WorldGen.worldGenCallback` → `GenerateWorld` → the dungeon step →
   `DungeonCrawler.SetupDungeonData` → `new DungeonData` → `ClassInit` →
   **fails**.

That is: the table is already broken (or being fought over) when someone gets
to it; whoever gets there first falls.

### Attempts (a new process each time)

The crash can only happen on the **first** world creation of each process,
which is when those classes are initialized. The first batches reused the
process and so only the first attempt counts. Honest count:

| Configuration | JS hooks on which threads | Crashes |
|---|---|---|
| Original Terraria (`com.and.games505.TerrariaPaid`) | none | **0 of 9** |
| Bunny Loader, just one JS hook on `Main.DoDraw` forwarding to the original | main only | 0 of 7 |
| Bunny Loader + HelloMod + ExampleMod (normal use) | generation only (`Item.SetDefaults`) | **0 of 7** |
| Bunny Loader + HelloMod + ExampleMod + hook on `DoDraw` | **both** | 1 of 6 |
| Bunny Loader + HelloMod + ExampleMod + benchmark mod | **both** | 2 of ~3 (includes the user's crash) |

The crash only showed up with a JS hook running **on both threads at the same
time**: 0 of 14 with a hook on one thread, 3 of 9 with both. Normal use (only
the example mods) never fell.

> The "normal use" row was measured by mistake: the batch was "interrupted"
> with `TaskStop`, but the script's `bash` survived and kept creating worlds.
> Of the 8 attempts, one gave TIMEOUT because the worlds were deleted in the
> middle of it; the other 7 count
> ([`tentativas-cfgE.txt`](dados/2026-09-23/tentativas-cfgE.txt)). To really
> stop the script: kill its `bash.exe` (`Stop-Process`).

### What was ruled out

- **It is not the benchmark mod alone**: without it, it also fell; with it,
  it also created.
- **It is not calling `DoDraw` through the hook's trampoline**: just the hook
  on `DoDraw`, 0 of 7.
- **It is not a thread of ours**: the probe finishes ~10 s after boot and the
  menu UI only reads ready values. But our code does run at the moment of the
  crash: the JS hooks execute inside the game's threads (see hypothesis 1).
- **It is not swapping the 127 item tables** (`content/items/ModItems.cpp`):
  the copy respects the element size (value type through
  `class_value_size`, reference through `Array.Copy`), and no table was
  rebuilt during generation.

### Open hypotheses

1. **Two concurrent JS hooks, and `JsSuspend`.** That is what the numbers point
   to. With a hook on `DoDraw`, the main thread spends almost the whole frame
   inside `original()`, and `callRaw` releases the JS engine lock
   (`JsSuspend`) meanwhile. The generation thread enters the engine through the
   `Item.SetDefaults` hooks, thousands of times, creating wrappers with
   `gchandle` and calling the IL2CPP API. What exactly breaks the runtime's
   generics table is not known yet; the candidates are the IL2CPP API calls
   made by the bridge (`describe`, `class_get_*`, `gchandle_*`) at the same
   time the other thread initializes classes.
2. **The boot scan (`installNamespaceRoots`)**, which walks every class on the
   probe thread. **Weakened**: it runs in every configuration, including the
   14 attempts without a crash.

### Proposed next step

Isolate hypothesis 1 with a batch of ~15 attempts on each variation: a hook on
`DoDraw` + the example mods **with `JsSuspend` turned off** (`original()`
holds the engine lock). If it goes to zero, the problem is letting two threads
inside the bridge at the same time, and the fix is serializing what the bridge
does in IL2CPP. Either way, it is worth moving the probe's boot to the first
`DoUpdate`, because touching IL2CPP from the game's threads is the right
design.

### How to reproduce

```bash
# 1. leave the game on the character screen with a saved world (or none)
# 2. the desired mods in files/mods/<uid>/ (see tools/crash-trials)
bash tools/crash-trials/trials2.sh 10 result.txt         # Bunny Loader
bash tools/crash-trials/trials_vanilla.sh 10 base.txt    # original Terraria
```

The scripts tap MuMu's screen at 1600×900, restart the app on each attempt,
count a created world by the new `.wld` in
`/sdcard/Android/data/<package>/Worlds/` and a crash by the logcat's
`Forwarding signal` line (they save the logcat of each crash).
