# Plan: FNA small gaps and the SpriteBatch matrix

Status: planned 2026-11-26. A1 done the same day (313 game structs get
equality; test `tools/tests/csequality`). A2 done the same day (test
`tools/tests/csfnagaps`, 11 cases checked against the game's own methods). A3 done
2026-11-27 (FNA code in `Microsoft/Xna/Framework/FNA/`, 5 more `csfnagaps` cases;
16/16 on the phone). A4 done 2026-11-27 (22/22 on the phone; csfxc 18/18 and
csspritefx 14/14 unchanged). Part B done 2026-11-27 (commit 9cd515bc,
csbatchmatrix 11/11 on the phone). The optimization review and the XNA math in
C# followed the same day (38aad5fd, csxnamath 160/160), then the Part B
performance review with the native transform (7f53c9ba, csbatchmatrix 21 cells
on MuMu). Nothing in this plan is left open. Source of the gaps:
`tools/docs/FnaInventory/members.txt` and `inventario-fna.md`.

## Part A: small gaps

### A1. Value equality for every game struct (ProxyGen)

- Today `Equals`, `GetHashCode` and `ToString` are reserved names in ProxyGen
  (`Reserved()` in `ProxyGen/Program.cs`), so the game's own versions come out as
  `Equals_`/`GetHashCode_`, and mirrored structs (`Rectangle`, `Point`,
  `Vector3/4`, `Quaternion`, `Plane`, `KeyboardState`, `MouseState`, `Point16`...)
  have no `==`, `!=`, `Equals(T)` or `IEquatable<T>`.
- In `EmitType`, struct branch: emit `IEquatable<T>`, `Equals(T)` (field by
  field over `layout.Fields`: `==` for primitives, enums and pointers,
  `EqualityComparer<T>.Default.Equals` otherwise), `Equals(object)`,
  `GetHashCode` (`HashCode` builder), `==` and `!=`.
- Remove the hand-written duplicates: `Vector2` and `Color` in
  `Microsoft/Xna/Framework/XnaConstructors.cs`, `Matrix` in `Matrix.Operators.cs`.
  Only these declare struct equality today.
- ToString is not generated; hand-write the missing ones (Point, Vector4,
  Quaternion, Plane, MouseState, Viewport) in FNA's format.

### A2. Operators and constructors (hand-written, `XnaConstructors.cs`)

- `Point`: `+ - * /`.
- `Vector3`: ctor(float), ctor(Vector2, float), unary `-`, `*`/`/` by vector.
- `Vector4`: ctors (float), (Vector2, float, float), (Vector3, float); all operators.
- `Quaternion`: ctors (x, y, z, w), (Vector3, float); operators.
- `Plane`: ctors (a, b, c, d), (Vector3, float), (Vector4), (Vector3 x3).
- `Matrix`: `*` and `+` take `in` today; switch them to by-value as in FNA.
- `Color`: ctor(Vector3), ctor(Vector4), `FromNonPremultiplied`, `Equals(Color)` (from A1).
- `KeyboardState`: indexer `this[Keys]` (over `IsKeyDown`), ctor(Keys[]) over the
  generated `New(GameArray<Keys>)`.
- `MouseState`, `Viewport`, `GameTime`: real constructors over the generated
  static `New(...)` (ProxyGen emits game ctors as `New`). For `GameTime` (a class
  proxy) this needs a parameterless struct ctor (C# 14 allows it).

Done: the struct constructors fill the fields in C# (no call into the game);
only `GameTime` calls the game's `New`, since it is a game object. `Point16`
also got tML's `Point16.TML.cs` API (size ctors, `(Point)`, `(short, short)`,
`+ - * / %`) plus the PC's implicit `Point` and `{X, Y}` ToString.
`Color(Vector3/Vector4)` round like BL's float ctor and like the game
(0.5 -> 128); FNA truncates (127). `new GameTime()` now allocates a game
object, so BL's own per-frame call in `SubworldVoteUI` uses
`Main.gameTimeCache`.

### A3. Math types that the game lacks

`BoundingBox`, `BoundingSphere`, `BoundingFrustum`, `Ray`, `Curve`, `CurveKey`,
`CurveKeyCollection`, and `Plane.Intersects`. Copy FNA's code (Ms-PL, keep the
notice), with inline arithmetic instead of the bridged `Vector3`/`Matrix`
statics, which call into the game.

### A4. Smaller ones

- `Microsoft.Xna.Framework.Design.ColorConverter` plus `[TypeConverter]` on
  `Color`. ModConfig already parses `DefaultValue(typeof(Color), "...")` itself
  (`ConfigOption.ReadDefault`), so this is for compiling.
- `new Effect(GraphicsDevice, byte[])` = `FxcGameEffect.Create(FxcEffect.FromBytes(bytes))`;
  `new Effect(Effect)` clones from `FxcGameEffect.SourceOf`.
- `Texture2D.SaveAsPng` over `GetData` with a small PNG encoder (zlib from the
  BCL). `SaveAsJpeg` would need a JPEG encoder; leave it unless a mod needs it.
- `SpriteBatch.DrawString(SpriteFont, StringBuilder, ...)`: the game has the
  overloads, check why a mod binds to another one.
- `EffectParameter` metadata (`ParameterClass`, `ParameterType`, `RowCount`,
  `ColumnCount`, `Elements`, `Annotations`, `Semantic`) and the two enums, from
  the parsed `.fxc` in `FxcEffect`.
- `SpriteSortMode.Texture/BackToFront/FrontToBack`: add them as extra enum
  values (`=` lines in types.txt, values 2, 3 and 4 as in FNA), and have BL's
  `Begin` pass `Deferred` to the game. Depth sorting is not possible after the
  fact: `SpriteBatchItem` keeps no layer depth. Note that the game's Deferred
  already groups by texture.

Done: `ColorConverter` (`Design/ColorConverter.cs`, own code) and `[TypeConverter]`
on `Color`. `new Effect(device, bytes)`, `new Effect(Effect)` and `Clone()` (the
generated `Clone` became `Clone_` through `!` in types.txt; a game effect still
clones in the game). `fxcInfo` sends the semantic and `N` lines with the
annotations of parameters, techniques and passes; the C# side maps MojoShader's
class/type to `EffectParameterClass`/`EffectParameterType` (samplers report as
textures, BL's extra standard parameters as `Void`), `Elements` is null for a
plain parameter and throws for an array, plus `GetParameterBySemantic`.
`SaveAsPng` in `PngWriter.cs` (nearest-neighbour when the size differs).
`DrawString`/`MeasureString` with `StringBuilder` go through the string overloads.
`SpriteSortMode` 2-4 exist and `Begin` (hand-written over `Begin_`) passes
`Deferred` to the game.

Found while testing: the game's own
`DrawString(SpriteFont, string, Vector2, Color)` ignores `position.X` on mobile
(the text always starts at the left edge; Y is right). It is a separate
per-character loop in the game (RVA 0xED9924), not the 9-argument overload.
Fixed the same day: ProxyGen accepts `!Type.Method(Type,Type,...)` to rename a
single overload to `Method_`, and the `string` and `StringBuilder` 4-argument
overloads call the 9-argument one with FNA's defaults (`\n` still breaks lines).
Checked on the phone with text at X = 100, 400 and 700.

## Part B: the matrix passed to `SpriteBatch.Begin`

Facts (disassembly, 2026-11-26):

- `Begin` stores only `_hasMatrix` (0xD8) and never uses the matrix.
  `BatchDeffered` = `defferedBatch && <runner flag> && sortMode == Deferred`.
- `End` -> `Setup` (non-immediate) -> `SpriteBatcher.DrawBatch`, which copies the
  queued items' vertices into the mesh. `FlushIfNeeded` draws early only for
  Immediate without BatchDeffered: `DrawImmediateBatch` copies
  `_batchItemList[0]` and draws it inside each `Draw`.
- On the phone, `GameViewMatrix.Zoom` and `Main.UIScale` are 1 (zoom changes the
  virtual screen), so the matrices vanilla passes are identity. One exception:
  reverse gravity. `DrawBatch` and `DrawImmediateBatch` flip the mesh themselves
  (`WorldCameraHeight`, scale y -1) when `gravDir == -1`.
- The game has its own scaling, `Clipper.Scale`: `(p - centre) * scale + offset`.
  It is applied only in the `Set` variants, not in `Set_Fast`, which
  `Draw_Fast` and one `Draw` use. It cannot rotate. It is not used here.

Design:

1. Hand-written `Begin(..., GameNullable<Matrix>)` in `SpriteBatch.TML.cs`
   (mods only). If the matrix is not identity and differs from
   `Main.GameViewMatrix.TransformationMatrix`, store {matrix,
   `_batcher._batchItemCount` at Begin, immediate} keyed by the batcher
   pointer. It also raises a native flag.
2. `On_SpriteBatch.End`, behind a `HookGates` absolute flag (open only while a
   matrix batch is active): transform positions of items
   `[start, _batchItemCount)` (or `[0, count)` if the count went back), offsets
   of `vertexTL/TR/BL/BR` by name. `p' = (x*M11 + y*M21 + M41, x*M12 + y*M22 + M42)`.
   Then call orig and clear the state.
3. `On_SpriteBatcher.DrawImmediateBatch`, same gate: transform item 0, then orig.
4. Limits to document: a matrix composed with `TransformationMatrix` under
   reverse gravity, and a custom effect's own vertex shader (the positions are
   transformed on the CPU).

Done 2026-11-27 in `Graphics/SpriteBatchMatrix.cs`. Differences from the design:
the matrices left alone are identity, `Main.Transform`, `Main.UIScaleMatrix`,
`GameViewMatrix.TransformationMatrix` and `ZoomMatrix`; the hooks install on the
first matrix `Begin` and sit behind one absolute flag (the number of pending
batches); the matrix is also written to `_renderMatrix`, which the game never
reads, so `DrawBatches.Restart` keeps it. Known gap: a flush in the middle of a
batch (`Flush()` or the 5461-item limit) draws the items before it untransformed.
Test `csbatchmatrix` (11/11 cells on the phone with `screen.py`; the log gives the
UI rectangle of the physical screen, since the phone's safe area offsets the UI).

## Tests

- New pack `csfnagaps`: equality, hashes, operators, constructors, bounding
  math against FNA's results, `new Effect(bytes)`, `SaveAsPng` round trip.
- New pack `csbatchmatrix` with `screen.py`-style cells: scale, translation,
  rotation, Deferred and Immediate, and `TransformationMatrix` (must not move).
- Regression: cstprim, csspritefx, csfxc, cstexdata, csgenerics, csplayer; all
  packs compile (179).
- Then regenerate the inventory and update `wiki/advanced/XNA-and-FNA.md`.

## Optimization review (2026-11-27)

Pack `tools/tests/csfnaperf`: one case per frame in the world, ns and bytes per
operation (best of 5, calibrated to about 2 ms), each next to a plain C#
reference; drawing cases best of 3 frames. Phone SM-A156M, JIT (no AOT image of
BunnyLoader.dll). Before = the Part B commit, after = this review.

| Case | Before | After |
|---|---:|---:|
| `Vector2.Zero` / `Vector2.One` | 80 / 84 ns | 3.6 / 5.2 ns |
| `Matrix.Identity` | 128 ns | 9 ns |
| `Matrix a - b` | 167 ns | 82 ns |
| `Point.GetHashCode()` | 16 ns | 6.8 ns |
| `Dictionary<Point>` / `<Vector2>` / `<Color>` lookup | 67 / 80 / 130 ns | 52 / 68 / 103 ns |
| `BoundingSphere.Contains(BoundingBox)` | 242 ns, 128 B | 95 ns, 0 B |
| `effect.Parameters["uColor"]` | 671 ns, 112 B | 322 ns, 0 B (82 of it is the game's `Parameters` getter) |
| `Effect.Clone()` | 344 µs, 36 KB | 249 µs, 21 KB |
| `SaveAsPng` 256x256 | 20.3 ms | 11.3 ms (141 KB file) |
| `DrawString(font, text, pos, color)` | 2241 ns | 1897 ns |
| `Begin`+`End` with the identity / a mod's matrix | 1259 / 4139 ns | 979 / 2132 ns |

What changed:

- The game's static math properties (`Vector2/3/4.Zero`, `One`, `UnitX`...,
  `Vector3.Up`..., `Matrix.Identity`, `Quaternion.Identity`, `Point.Zero`,
  `Rectangle.Empty`) were calls into the game; they are C# values now (`!` in
  types.txt; the game's getter stays as `Zero_`, `Identity_`..., and
  `csfnagaps constants` checks they match).
- ProxyGen's struct hash: `hash * 397 ^ field` instead of `HashCode.Combine`
  (equal values still hash the same; `-0f` and `0f` too).
- `Matrix` subtraction without the negate; `Matrix.Identity` from a static field.
- `BoundingSphere.Contains(BoundingBox)` takes the farthest corner per axis (same
  float result as the eight corners, no array); the frustum cases read the
  frustum's own corner array instead of a clone.
- Parameter, pass and technique lookups by name go through a per-collection
  cache, cleared whenever the game's GC runs (`GameGc.Collections()`), so a
  freed collection's address is never trusted; without the GC count they scan
  as before.
- `SaveAsPng`: `CompressionLevel.Fastest` and no per-pixel division when the
  size does not change.
- `SpriteBatch.End` is hand-written: it transforms a pending matrix batch
  itself, so the `End` hook (still there for the game's own `End`) stays shut;
  the game's matrices are read once per `_gameUpdateCount`; the transform loop
  reads the item array as a span.

Measured and left alone:

- `Explicit` against `Sequential` layout for the mirrored structs: the same
  (14.5 against 14.3 ns per `Vector2 +`), so ProxyGen keeps `Explicit`.
- `Matrix *` by value costs 129 ns against 76 ns with `in` parameters; the
  operators stay by value as in FNA.
- The game's math methods still cross the bridge: `Vector2.Distance` 90 ns
  (35 in C#), `Length()` 80 ns (20), `Matrix.CreateRotationZ` 132 ns (101).
- A Mono detail found on the way: reading `GameNullable<T>.Value` inside the
  `Begin` wrapper cost about 110 ns per call even when the branch was not taken;
  passing the `GameNullable` on and checking `HasValue` in the callee does not.

Correctness after the review: csfnagaps 27/27, csbatchmatrix 13 cells (with a
batch ended by the game's own `End`), cstprim 16, csspritefx 14, csfxc 18,
cstexdata 19, csgenerics 10, csplayer 20 on the phone; all 183 packs compile.

## XNA math in C# (2026-11-27)

The review showed that the cost of the FNA layer was crossing into the game.
Every value-taking member of the game's XNA math now runs in C#: 162 members of
Vector2/3/4, Matrix (with its seven direction properties), Quaternion, Plane,
MathHelper, Rectangle (with Left/Right/Top/Bottom/Center/Location/IsEmpty) and
Color (Multiply, Lerp, PackedValue). The `ref`/`out` overloads, the ones that take
game arrays, the `New` constructors and Color's GetHue/GetSaturation/GetBrightness
stay the game's.

- ProxyGen takes `!Type.Method(Type,Type,...)` per overload (all marked overloads
  of a name share `Name_`); `!Type.Property` for properties. 162 markers.
- `FNA/FnaMathCore.cs` is FNA's code, extracted by a script (155 methods: the
  value and `ref`/`out` forms of each, renamed `TypeMethod`, calls between them
  pointed inside the class). `FNA/Vector.Math.cs` and `FNA/Matrix.Math.cs` are
  the public members over it; trivial ones are written inline.
- Rule: FNA's result, since tModLoader runs on FNA. Where the phone's XNA
  differs, the test checks FNA's answer with a fixed example or an equivalent
  path the game has, and the wiki lists the cases (XNA-and-FNA, "The math
  follows FNA..."): CreateRotationY sign, Clamp with min > max, WrapAngle(-Pi),
  Intersects/Intersect of a zero-size rectangle, Matrix.Divide per element,
  Quaternion.Concatenate order, Color.Multiply clamping, Color.Lerp truncation,
  CreateShadow without normalizing. Not implemented on the phone (throws) and
  working now: Transform by a Quaternion (5), Matrix.Lerp, CreateFromAxisAngle,
  CreatePerspective, CreatePerspectiveOffCenter, Plane.Transform (2).
- Test `tools/tests/csxnamath`: each member against the game's `Name_` on 300
  random inputs plus edge cases (zero vector and quaternion, empty rectangles,
  angles of +-pi), relative tolerance 2e-5, NaN equal to NaN: 160/160 on the
  phone.

| Case (ns, phone) | Game | C# |
|---|---:|---:|
| `Vector2.Distance` | 99 | 39 |
| `v.Length()` | 88 | 22 |
| `Vector2.Normalize` | 85 | 42 |
| `Vector2.Lerp` | 93 | 27 |
| `Vector2.Transform(v, m)` | 99 | 37 |
| `MathHelper.Clamp` / `Lerp` | 86 / 92 | 9 / 6.5 |
| `Rectangle.Intersects` | 104 | 13 |
| `rect.Center` + `rect.Bottom` | 160 | 12 |
| `Color.Lerp` | 141 | 51 |
| `Matrix.CreateRotationZ` | 136 | 124 (FNA computes sin/cos in double) |

## Part B review (performance)

New cases in `csfnaperf` (`b ...`: 1000 sprites in `Deferred`, 200 in
`Immediate`, 50 strings, 100 restarts) and in `csbatchmatrix` (three sprites in
one `Immediate` batch, a composite matrix, `DrawBatches.Restart` after and
inside a mod's matrix batch, called through reflection).

Phone (SM-A156M), before any change, per unit:

| Case | No matrix | Mod's matrix |
|---|---:|---:|
| Sprite, `Deferred` | 306-341 ns | 342-362 ns |
| Sprite, `Immediate` | 785 ns | 1,722-1,761 ns |
| 23-letter string, `Deferred` | 2,566-2,611 ns | 3,451-3,460 ns |

Found and fixed:

- `DrawBatches.Restart` (used by `NPCDrawHooks` around NPC drawing) restarted a
  batch with `_renderMatrix` when `_hasMatrix` was set. The game never writes
  that field, so it held the last mod matrix (the test moved a square drawn
  after a restart) or whatever the object started with. It now takes the
  pending mod matrix (`SpriteBatchMatrix.TryPending`) or the identity, and Bunny
  Loader no longer writes `_renderMatrix`.
- A `Begin` without a mod matrix on a batcher that still has one pending (a mod
  batch that never reached `End`) drops it.
- `DrawImmediateBatch` reads the last registered batcher from two statics before
  falling back to the locked dictionary.

Emulator A/B in one session (MuMu 16384; an earlier session showed +3 µs on
`Begin`+`End` with a matrix that the next session did not repeat, the usual
MuMu noise): sprite in `Immediate` with a matrix 1,710-1,718 -> 1,432 ns (no
matrix: 547-652); `Begin`+`End` with a matrix 1,665-1,671 -> 1,669 ns; restart
inside a mod batch 2,506-2,560 -> 2,444 ns. The rest of an `Immediate` sprite's
extra cost is entering the C# hook once per sprite; a native version of the
`End`/`DrawImmediateBatch` transform would remove it and needs a new APK.

Also seen: `new Effect(bytes)` in a loop fills the native sprite-effect table
("too many sprite effects"): every effect keeps its passes registered.

Regression on MuMu: csbatchmatrix (21 cells), csnpcdraw 9, csspritefx 14,
cstprim 16, csfxc 18, csfnagaps 27, cstexdata 19, csgenerics 10, csplayer 20.

Native transform (same day): `cpp/graphics/SpriteMatrix.cpp` keeps up to 16
pending batchers with the 2D part of the matrix (M11, M12, M21, M22, M41, M42)
and the batcher's item count at `Begin`; native hooks on `SpriteBatch.End` and
`SpriteBatcher.DrawImmediateBatch` move the corners and call the original, with
an atomic count so the hooks cost one load while nothing is pending. Four
`NativeApi` entries at the end (`SpriteMatrixBegin`, `SpriteMatrixGet`,
`SpriteMatrixDrop`, `SpriteMatrixPending`), so the DLL needs the new APK. The C#
side keeps only `FromGame` and the calls; the C# hooks, the hand-written `End`
(and its `!` marker) are gone. Extra cost of the matrix, matrix minus no matrix
in the same run (MuMu, two runs each, C# hooks -> native): sprite in
`Immediate` +1,041-1,066 -> +139-140 ns; 23-letter string +664-692 ->
+265-293 ns; `Begin`+`End` +1,215 -> +778-867 ns; restart inside a mod batch
+2,215-2,290 -> +1,553-1,843 ns. csbatchmatrix 21 cells with the native path.
