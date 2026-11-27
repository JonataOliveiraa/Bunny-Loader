# Plan: FNA small gaps and the SpriteBatch matrix

Status: planned 2026-11-26. A1 done the same day (313 game structs get
equality; test `tools/tests/csequality`). A2 done the same day (test
`tools/tests/csfnagaps`, 11 cases checked against the game's own methods). A3 done
2026-11-27 (FNA code in `Microsoft/Xna/Framework/FNA/`, 5 more `csfnagaps` cases;
16/16 on the phone). A4 done 2026-11-27 (22/22 on the phone; csfxc 18/18 and
csspritefx 14/14 unchanged). Source of the gaps:
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
