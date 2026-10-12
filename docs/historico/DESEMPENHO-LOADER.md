# Mod loader performance (ExMod)

Assessment of 2026-10-01: how much the mod loader (the `ModProjectile`,
`ModNPC`, `ModItem`... classes and the hooks behind them) costs per frame, and
the three optimizations chosen from the measurement. It continues
[`PONTE-OTIMIZACAO.md`](PONTE-OTIMIZACAO.md), which covered the JS → IL2CPP
bridge operation by operation; here the subject is what the loader does with
it in a real world.

## Method

- [`tools/tests/perfloader`](../../tools/tests/perfloader): during loading,
  the patterns the loaders repeat on each call, in pure JS; in the world, with
  the Example Mod, 300 idle frames and 300 frames with **60 animated
  projectiles** (`ExampleAdvancedAnimatedProjectile`: AI, PreDraw, GetAlpha)
  and **15 live mod slimes**. `bl.hookStats` gives, per hook, the calls and the
  JS time (without `original()`'s); `DoUpdate` and `DoDraw`, the frame's CPU
  time.
- [`tools/tests/hookcost`](../../tools/tests/hookcost): the cost of a hook
  that only forwards to `original()`, measured in C++ and from inside JS.
- MuMu (translated ARM; see the caveat in `PONTE-OTIMIZACAO.md`): two rounds of
  each build. Raw data in [`dados/desempenho-loader/`](dados/desempenho-loader/).

## What the measurement showed

**1. In a hook called once per frame, the cost is entering and leaving JS, not
the JS.** Hooks that only forward to `original()`:

| Hook | From C++ | From inside JS |
|---|---:|---:|
| `Main.DoUpdate` | 62 µs | 7 µs |
| `Player.UpdateBiomes` | 49 µs | 5 µs |
| `Main.UpdateAudio` | 26 µs | 6 µs |
| `Player.ResetEffects` | 17 µs | 2 µs |

With timers on each section of the dispatch (a measurement-only build), the
whole difference falls inside `JS_Call`, outside the function's body:
preparing the arguments and `original()`'s prologue and epilogue add up to 1
to 3 µs. The same hook called in sequence costs ~0.6 µs (the
microbenchmark), and `Main.get_backTextureValues`, called 12 times in a row
per frame, 1.6 µs. It is the interpreter entering "cold" after milliseconds of
game code, and MuMu (ARM → x86 translation) weighs on that: **check on the
phone**. With the Example Mod, 43 hooks like that came to ~1.9 ms per frame on
MuMu.

**2. Per entity, the hooks the class does not even use.** It took just ONE
projectile of the mod writing `OnTileCollide` for `HandleMovement` and `Update`
to be installed, and from then on EVERY mod projectile entered JS in them; the
same with `Damage`, `CanCutTiles`... With 60 projectiles: ~21 µs per
projectile per frame in hooks its class does not write.

**3. `PreDrawExtras`' two-part drawing.** It took just one of the mod's
projectiles having a chain (flail, hook) for three drawing hooks
(`EntitySpriteDraw` twice and `SpriteBatch.Draw`) to enter JS inside the
drawing of **every** mod projectile, almost always just to forward: ~10 µs per
drawn projectile.

**4. The JS patterns** (during loading, pure JS, MuMu):

| Pattern | ns above a method call |
|---|---:|
| `Safe.Run(n + '.AI', () => m.AI(p))` | ~440–660 |
| the same with the label ready | ~320–470 |
| `try { m.AI(p) } catch` | ~0 |
| `AnimateTile` with 2 new `Ref`s and a closure | ~1400–1850 |
| with the 2 `Ref`s reused, no closure | ~440–610 |

Each projectile's AI and drawing went through three `Safe.Run`s with a built
label and a new closure (`PreAI`, `AI`, `PostAI`; `PreDrawExtras`, `PreDraw`,
`PostDraw`), even calling the methods the class does not write.

## The three optimizations

1. **Per-type marks on each method's hooks.** The `marks` filter (which
   already existed for tiles) now applies to `ModProjectile` (movement,
   `Kill`, `StatusNPC/Player`, `Colliding`, `Damage`, hitbox, `CutTiles`,
   `GetAlpha`, hook, drawing), `ModNPC` (AI, `FindFrame`, `GetChat`,
   `CheckActive`, `NPCLoot`) and `ModItem`'s `GetAlpha`: only the types whose
   class writes the method enter JS.
2. **The native `flag` filter** (`Bridge.h`, `JsHook.cpp`, `bl.hookFlags`): a
   global switch JS turns on and off; when off, the method runs without JS.
   The four filters of the two-part drawing only turn on during a two-part
   draw (`proj.split`), and the menu theme's `DrawSunAndMoon` only with a mod
   theme in the menus (`menu.sky`; before, it entered JS every frame inside
   the world).
3. **The per-frame hooks' JS without closures or per-call labels.**
   `ModProjectile`'s AI and drawing and `ModNPC`'s AI follow a per-class plan
   (what it writes and the log labels, built once) and only call what it
   writes, with a direct `try/catch` (`Safe.Report` for the one-time warning);
   `AnimateTiles` reuses the two `Ref`s. The drawing's light is only computed
   if `PreDraw` or `PostDraw` receive it.

## Result (MuMu, average of 2 rounds per build)

| | Before | After | Δ |
|---|---:|---:|---:|
| JS in the hooks, idle world | 1.88 ms | 1.20 ms | **−36%** |
| JS in the hooks, 60 projectiles + 15 slimes | 8.9 ms | 6.0 ms | **−32%** |
| `DoUpdate`, with the entities | 9.4 ms | 5.2 ms | **−45%** |
| `DoDraw`, with the entities | 13.8 ms | 11.7 ms | −15% |
| `Projectile.AI`, per projectile | 18.3 µs | 14.0 µs | −23% |
| hooks the class does not write, per projectile | ~21 µs | 0 | |
| two-part drawing filters, per projectile | ~10 µs | 0 | |

(The JS in the hooks includes ~0.85 ms from the test itself, which keeps the
projectiles alive.)

What is left in the drawing (~65 µs per projectile) is almost all the Example
Mod's `PreDraw`: a dozen calls to the bridge per draw, each `Vector2` and
`Rectangle` a new struct. That is the next target, in the bridge, not in the
loader.

Tests: `projectiles`, `npcs`, `moditems`, `exmod1`, `exmod2`, `hooks`,
`hookchain`, `globals`, `tileframes`, `trailcache`, `oldpos`, `boss`,
`summons` and the new [`menusky`](../../tools/tests/menusky) green (run alone:
together, some get in each other's way, as before). `ifbusy` fails in the
`'original'` case with and without these changes (0 frames in 1.5 s): an
earlier failure, to be investigated separately.

**Pending:** the same measurements on the phone (item 1 depends on them).
