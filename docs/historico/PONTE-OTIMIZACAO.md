# Optimizing the JS → IL2CPP bridge

Continues [`AVALIACAO-PONTE-E-CRASH.md`](AVALIACAO-PONTE-E-CRASH.md). One step
at a time, each measured with [`tools/bench`](../../tools/bench) and checked
with the test mods before the next one. All on MuMu (see the caveat there: the
absolute numbers are from translated ARM; what counts is the comparison).

## Plan

| # | Step | What changes | Where it should show |
|---|---|---|---|
| 0 | Baseline | nothing; two rounds to see the noise | every row |
| 1 | Root table | `gchandle_new/free` per wrapper → write/clear a slot in a block the collector sees (`il2cpp_gc_alloc_fixed`) | `players[0]`, `Main.player[0].whoAmI`, instance hook |
| 2 | One wrapper per object | pointer → wrapper map; the second read reuses it; `===` starts to hold | same, and identity |
| 3 | Method cache | `obj['signature']` always returns the same `GameMethod` | `p['...']()` every time |
| 4 | Fast array index | integer atom read directly; element type stored in the array | `bt[0]`, `players[0]` |
| 5 | Borrowed arguments in the hook | `self`/arguments without a root during the callback; root only if JS keeps them | instance hook |

Each step has to keep green:

- [`tools/tests/nullable`](../../tools/tests/nullable) (the whole bridge: types,
  structs, arrays, hooks);
- [`tools/tests/wrappers`](../../tools/tests/wrappers) (created for this
  round): an object held only by JS survives `GC.Collect()`, identity, a `self`
  kept after the hook stays valid.

## Method

- [`tools/bench/run.sh`](../../tools/bench/run.sh) installs the mods, opens the
  game, enters the test world and saves the log;
  [`compare.py`](../../tools/bench/compare.py) averages the rounds and computes
  the delta against the baseline.
- The benchmark takes the **best of 7** for each item, and the whole battery
  runs twice per session. With "best of 3" the same code varied up to 70%
  between sessions (875 × 1494 ns on the instance hook); this way, it stays
  between 5 and 10%. Raw data: [`dados/otimizacao/`](dados/otimizacao/).
- **Negative control for the wrappers test**: with the anchor sabotaged (the
  slot does not store the pointer), the test flags all 4 cases — the collector
  reclaimed the items and the memory was reused by garbage (damage 5, type 1).
  That is, the test catches the error these changes can introduce.

## Results

Average of two rounds; ns above the empty loop.

### Step 0 → 1: root table

`Roots` ([`script/bridge/Roots.cpp`](../../app/src/main/cpp/script/bridge/Roots.cpp)):
blocks of 4096 pointers in `il2cpp_gc_alloc_fixed` (the collector scans them,
does not reclaim them), a free-slot list, writes through the write barrier.
This Terraria's collector: **not incremental** (boot log).

| Item | Base | Step 1 | Δ |
|---|---:|---:|---:|
| `players[0]` (object element) | 424 | 319 | **−25%** |
| `Main.player[0].whoAmI` | 784 | 582 | **−26%** |
| instance hook (`self`) | 840 | 706 | **−16%** |
| other items | | | ±10% (noise) |

Tests: `wrappers` and `nullable` green, 0 segfaults.

### Step 1 → 2: one wrapper per object — and the cause of the open crash

A pointer → wrapper map in `script/bridge/Bindings.cpp` (`wrapperFor`),
without reference counting; the finalizer removes the entry. Identity now
holds: `Main.player[0] === Main.player[0]` and `kept[0] === array[0]` give
`true`.

**With step 2, entering the world with bench + tests crashed in 3 of 4
rounds** (0 of 6 in steps 0 and 1). The same failure as the crash when
creating a world (generic class initialization in the runtime, SIGSEGV in a
loop), now while loading: nested JS hooks on the main thread (`DoDraw` →
`DrawInterface`) and the world loading thread entering the engine through the
`Item.SetDefaults` hooks.

First experiment: the same code with `JsSuspend` turned off (`original()`
holds the engine lock). It gave 0 of 4, and **I wrongly concluded** that
`JsSuspend` was the cause. With it removed, the next battery fell again (1
crash and 1 round with methods coming back `undefined`: memory corruption).

**The real cause: a dangling reference in the hook's `original()`**
([`script/bridge/JsHook.cpp`](../../app/src/main/cpp/script/bridge/JsHook.cpp)).
`js_original` kept `Frame& f = g_frames.back()` and wrote the result into it
after calling the game's method. If the method fired another hook on the same
thread, `push_back` reallocated the `vector`, and the write (~200 bytes) landed
in already-freed memory — which IL2CPP reused for its generics tables. It
happens whenever nesting goes past the thread's `vector` capacity, which
includes the FIRST time on a new thread: the example mods have two JS hooks
chained on `Item.SetDefaults` (one calls the other through `original()`), and
the world generation and loading threads are new each time. Hence "always on
the process's first world generation". Fixed by keeping the INDEX.

| Configuration | Rounds | Crashes |
|---|---:|---:|
| with `JsSuspend`, without the fix | 4 | 3 |
| without `JsSuspend`, without the fix | 7 | 1 (+1 corrupted) |
| **with the fix, without `JsSuspend`** | 5 | **0** |
| **with the fix, with `JsSuspend`** | 5 | **0** |

`JsSuspend` was innocent. Its cost, measured: ~100 ns more per hook that calls
the original (+27–30% versus +6–8% over the base).

Performance (before the fix, without `JsSuspend`, 4 consistent rounds; the 10
rounds after the fix stayed in the same range —
[`frame-fix-*`](dados/otimizacao/)):

| Item | Base | Step 1 | Step 2 | Total Δ |
|---|---:|---:|---:|---:|
| `players[0]` (object element) | 424 | 319 | **176** | **−59%** |
| `Main.player[0].whoAmI` | 784 | 582 | **281** | **−64%** |
| instance hook (`self`) | 840 | 706 | **437** | **−48%** |
| static hooks | 490–538 | | 456–460 | −7 to −14% (without `JsSuspend`) |
| other items | | | | ±10% (noise) |

`wrappers` and `nullable` tests green in the 5 rounds that reached the end.

**Decision (user):** keep `JsSuspend`. It exists so a long hook (drawing) does
not make hooks on other threads (world generation) wait for the lock; the cost
of ~100 ns per hook is worth that.

> **2026-09-26:** `JsSuspend` stopped being "one parked thread at a time". With
> the game thread almost always parked, a hook on the save or world loading
> thread could not release the engine and held it for the whole method. The
> music hook waited 3 s on the user's device. Now each thread carries its own
> QuickJS frame stack (`QuickJsExt.c`, which compiles `quickjs.c` with access
> to `rt->current_stack_frame`), and any number of threads can be inside
> `original()` at the same time.
>
> Cost, on MuMu's bench (3 alternating rounds per build): a hook with
> `original()` from ~595 to ~750 ns; without `original()`, the same (~300);
> frame time the same. A measurement-only variant, which did LESS than the old
> one (no stack nor owner, no parking lock), came out at ~700: most of the
> difference is where the code landed, felt through MuMu's ARM→x86
> translation, not what it does. `enginethreads` under the old rule: 0 frames
> in 1.5 s and the save's JS stack with game thread frames; under the new one,
> 14 frames.
>
> Fixed the same day: with the engine released inside an `original()`, the
> nested hook entered "from scratch" and `JsLock` realigned QuickJS's stack
> limit to the stack pointer of NOW (`JS_UpdateStackTop`), deeper than the one
> at the start of the chain; each level got 256 KB more (`hookslots` went from
> 53 to 120 levels). Now each thread keeps the top from where it entered the
> engine and reuses it on every nested entry and every return from
> `original()`: 55 levels, and whatever goes beyond that falls into "Maximum
> call stack size exceeded" with the original running in its place.

Final measurement of step 2 (fix + `JsSuspend`, 5 rounds,
[`frame-fix-comsuspend-*`](dados/otimizacao/)):

| Item | Base | Final step 2 | Δ |
|---|---:|---:|---:|
| `players[0]` (object element) | 424 | 189 | **−55%** |
| `Main.player[0].whoAmI` | 784 | 305 | **−61%** |
| instance hook (`self`) | 840 | 619 | **−26%** |
| static hooks with original | 490–538 | 624–696 | +27–30% (session +5–10% slower; see above) |

### Step 2 → 3: method cache

`makeGameMethod` keeps one `GameMethod` per `MethodInfo` (holds the reference
forever, like the hook callbacks; the JS runtime lives until the process
dies). 4 rounds, [`passo3-*`](dados/otimizacao/):

| Item | Base | Step 2 | Step 3 | Total Δ |
|---|---:|---:|---:|---:|
| `p['bool CanBePushedByWind()']()` every time | 294 | 325 | **157** | **−47%** |
| `f(p)`, stored method (reference) | 102 | 101 | 102 | 0% |
| other items | | | | as in step 2 |

What is left between 157 and 102 is the member lookup and the property read.
Tests green, 0 segfaults. (A 5th round came out empty: `run.sh` aborted at
`run-as` right after `adb install`; the script now waits for the package.)

### Step 3 → 4: fast array index

The index is read directly from QuickJS's integer atom (bit 31 + value), with
the encoding checked once against `JS_NewAtomUInt32`; if it ever changes, it
falls back to the text path. Element type stored in the array's wrapper. 4
rounds, [`passo4-*`](dados/otimizacao/):

| Item | Base | Step 3 | Step 4 | Total Δ |
|---|---:|---:|---:|---:|
| `bt[0]` (`int[]` element) | 148 | 160 | **47** | **−68%** |
| `players[0]` (object element) | 424 | 198 | **71** | **−83%** |
| `Main.player[0].whoAmI` | 784 | 305 | **160** | **−80%** |

A stumble along the way: the first version used `JS_AtomToValue`, assuming it
returned an integer for an integer atom — it builds a **string**. The tests
caught it in the first round (`Main.player[0]` became `undefined`). And the fix
revealed a literal NUL byte in `Value.cpp` (from before this round; it compiled
the same, but made `grep` treat the file as binary and hide results) —
replaced with `'\0'`.

### Step 4 → 5: cheaper new wrapper (adjusted plan)

The plan was "borrowed arguments in the hook": not anchoring `self` during the
callback. Measuring before doing it, the anchor no longer weighed: since step 1
it is writing a pointer into a slot. A new benchmark item (`arrayElemFresh`:
distinct elements of `Main.item`, which JS does not hold — a NEW wrapper per
access, exactly the case of a hook's `self` in `Projectile.AI`) showed
~400 ns, against ~70 ns for the reused wrapper. The bulk was allocation. Step 5
went after that, in three parts measured separately:

| Sub-step | New wrapper (ns) | Δ |
|---|---:|---:|
| before (step 4) | ~403 | |
| 5a: no write barrier when the collector is not incremental (this Terraria's) | ~381 | −5% |
| 5b: `Pinned` reused from a free list, instead of `new`/`delete` | ~301 | −21% |
| 5c: [`WrapperMap`](../../app/src/main/cpp/script/bridge/WrapperMap.h) — open addressing, no per-entry allocation | **~154** | **−49%** |

`WrapperMap` has its own fuzz test against `std::unordered_map`
([`tools/tests/wrappermap`](../../tools/tests/wrappermap), 9 million
operations, runs on MuMu as an x86_64 binary), with a negative control:
inverting the removal condition, it fails at operation 185. The wrappers test
gained a stress test of the map (8 passes over the 400 items of `Main.item`,
checking `whoAmI`).

## Summary

ns above the empty loop, MuMu, average of each step's rounds:

| Operation | Before | After | Δ |
|---|---:|---:|---:|
| `bt[0]` (`int[]` element) | 148 | 50 | **−66%** |
| `players[0]` (object element) | 424 | 65 | **−85%** |
| `Main.player[0].whoAmI` | 784 | 150 | **−81%** |
| new wrapper per access (a hook's `self`) | ~403¹ | ~154 | **−62%** |
| `p['signature']()` every time | 294 | 146 | **−50%** |
| field, property, stored method, pure JS | | | no change (noise) |

¹ measured from step 4; before step 1 there was also a `gchandle` per object,
so the real gain over the base is larger.

Besides performance, the round:

- **found and fixed the crash when creating/loading a world** — a dangling
  reference in the hooks' `original()` (step 2);
- gave game objects **identity** in JS (`Main.player[0] === self`);
- removed a literal NUL byte from `Value.cpp`.

Every step with `tools/tests/wrappers` and `tools/tests/nullable` green.
