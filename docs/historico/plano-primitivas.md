# Primitives: DrawUserPrimitives, BasicEffect and vertex buffers

Started 2026-11-26, after the FNA inventory ([inventario-fna.md](inventario-fna.md))
showed primitives as the largest `3 native` gap.

## What the phone has (read 2026-11-26)

- PC `VertexStrip.DrawTrail` calls `GraphicsDevice.DrawUserIndexedPrimitives`.
  The phone's (`0x90C1D8`) calls its own
  `GraphicsDevice.DrawGeometry(VertexStrip.CustomVertexInfo[], short[], vertexCount, indexCount, Vector3 position, Vector3 scale)`
  with `position = 0, scale = 1` (y flipped only with reversed gravity).
- `DrawGeometry` (`0xEE6A50`, and the `SpriteBatcher.VertexData[]` overload at
  `0xEE674C`) takes the material from `GetMaterial` (the bound `EffectPass` and
  `GraphicsDevice.Textures`), takes a `GeometryBufferInstance` from a pool, sets
  its `renderQueue`, uploads a Unity `Mesh` and draws it in order with the sprite
  batches. Vertices are in `Main.screenWidth` x `Main.screenHeight` pixels, y down.
- `GetMaterial` dereferences `EffectPass.BoundEffect` without a null check: a
  draw with no pass bound crashes (SIGSEGV in `MaterialBuffer.GetMaterial`).
- `SpriteBatcher.VertexData` is `Vector2 Position, Color32 Color, Vector2 TextureCoordinate`.
  `CustomVertexInfo` has a `Vector3 TexCoord`, but ProxyGen drops its overload
  (same C# signature as the `VertexData` one).

## Proof (`tools/tests/csprimpoc`, MuMu 16384, 2026-11-26)

Mod-made `VertexData` drawn by the game's `DrawGeometry` from
`PostDrawInterface`:

- Lands at the expected `Main.screen` pixels.
- A mod `.fxc` pass bound through `MiscShaderData.Apply` colors it through the
  existing sprite shader path (`SpriteFx`), with no new native code.
- V comes out flipped (Unity textures are bottom-up): the converter writes `1 - v`.
- A vanilla trail shader (`RainbowRod`) needs the `CustomVertexInfo` layout and
  shows garbage with `VertexData`; vanilla trails keep going through
  `VertexStrip.DrawTrail`.

## Phase 1: managed (no native change)

A new `BunnyLoader.dll`, so a new APK, but no change to `libbunny.so`.

The vertex shader runs on the CPU for the common case: the position times one
matrix. The pixel shader stays on the GPU through `SpriteFx`.

1. Vertex types written from the FNA API (not copied): `VertexElement`,
   `VertexElementFormat`, `VertexElementUsage`, `VertexDeclaration`, `IVertexType`,
   `VertexPositionColor`, `VertexPositionColorTexture`, `VertexPositionTexture`,
   `VertexPositionNormalTexture`, `PrimitiveType`, `BufferUsage`,
   `IndexElementSize`, `SetDataOptions`, `VertexBufferBinding`.
2. `GraphicsDevice.DrawUserPrimitives<T>` and `DrawUserIndexedPrimitives<T>`
   (`short[]` and `int[]`, with and without a `VertexDeclaration`): read position,
   color and texture coordinate 0 through the declaration, transform, convert to
   `VertexData` (`1 - v`), triangle lists only (strips unrolled, lines as 1 px
   quads, points refused), split above 32767 vertices, and call `DrawGeometry`
   with pooled game arrays.
3. The matrix: `BasicEffect`'s `World * View * Projection`; for a mod `.fxc`,
   its 4x4 parameter (the only one, or the one named like
   `WorldViewProjection`/`MatrixTransform`/`TransformMatrix`), read back from the
   game `EffectParameter`. Clip space to pixels:
   `((x / w + 1) / 2 * screenWidth, (1 - y / w) / 2 * screenHeight)`.
4. `BasicEffect` (2D subset): `World`, `View`, `Projection`, `DiffuseColor`,
   `Alpha`, `VertexColorEnabled`, `TextureEnabled`, `Texture`; the pixel shader is
   one embedded `.fxc` pass (`tex2D(uImage0, uv) * color`, white texture when
   `TextureEnabled` is off). Lighting and fog are refused with a clear error.
5. `VertexBuffer`, `DynamicVertexBuffer`, `IndexBuffer`, `DynamicIndexBuffer`
   as managed copies; `SetVertexBuffer`, `Indices`, `DrawPrimitives`,
   `DrawIndexedPrimitives` over the same path.
6. Test pack `cstprim` with screenshot cells, and the wiki section.

Limits of phase 1: a vertex shader that does more than one matrix product, a
`TEXCOORD0.z` read by the pixel shader, and z/depth are not carried.

Found on the way:

- The game's `EffectParameter.SetValue(Matrix)` is an empty method (`ret` at
  `0xEBB27C`); `SetValue(float)` writes `Data[0]`. Every `SetValue` is now written
  by hand (`!EffectParameter.SetValue` in `types.txt`): numbers and vectors go to
  `Data` (up to 4 floats, as before), matrices to a table in C#, with the FNA
  array overloads and `GetValue*`.
- `Matrix` had no operators; `*`, `+`, `-`, `/`, `==` and the 16-float
  constructor are in `Matrix.Operators.cs`.
- `Main.instance.GraphicsDevice.BlendState = ...` (or `.Indices`,
  `.RasterizerState`, `.ScissorRectangle`) does not compile in BL (CS1612): the
  device is a struct returned by value. A local variable works
  (`GraphicsDevice device = Main.instance.GraphicsDevice; device.Indices = ...`).
  tML mods write it the first way. Fixed the same day in ProxyGen: a get-only
  property whose type is a game object returns `ref readonly` to a per-thread
  ring of 32 slots (`Interop/GameSlot.cs`), so a setter can be called through
  it; 426 properties changed (`Game.GraphicsDevice`, `Main.LocalPlayer`,
  `Player.HeldItem`...). Properties with a setter and value types (`Vector2`)
  are unchanged.

### Phase 1 status (2026-11-26, not committed)

Built as planned: `Vertices.cs`, `Buffers.cs`, `BasicEffect.cs` (+ the embedded
`BasicEffect.fx`/`.fxc`), `PrimitiveRenderer.cs`, `GraphicsDevice.Primitives.cs`,
`Matrix.Operators.cs`, the hand-written `EffectParameter` values in
`Effect.TML.cs`, and `FxcGameEffect.SourceOf`.

- All `BasicEffect` instances share one game `Effect` (`SpriteFx` has 64 slots
  for the whole game); each instance keeps its state in C#, and the instance a
  mod last touched (a property or `CurrentTechnique`) is the one drawn.
- Like FNA, drawing leaves the effect's texture bound in `Textures[0]`: putting
  the previous one back on every draw made the game redo the material and cost
  ~130 µs more per draw.
- Pack `tools/tests/cstprim` on MuMu 16384: 11/11 checks, and `screen.py` finds
  all 11 cells (`BasicEffect` color and texture, triangle strip, lines, vertex
  and 32-bit index buffers, a mod `.fxc` placed by `uWorldViewProjection`).
  `csspritefx` 14/14, `csfxc` 18/18 and `cstexdata` 19/19 unchanged.
- Cost on MuMu (houdini, so only relative): the game's `DrawGeometry` alone is
  ~130-200 µs per call; a `DrawUserPrimitives` quad adds ~25 µs of C# (state
  ~17 µs, conversion ~7 µs). The first calls past the frame's previous peak
  cost more (~350-700 µs) because the game grows its `GeometryBufferInstance`
  pool. A 2000-vertex strip is ~1-1.4 ms. Not measured on a phone yet.
- World space works as on PC: from `PostDrawTiles`, `World =
  CreateTranslation(-screenPosition)`, `View = GameViewMatrix.TransformationMatrix`
  and an ortho of `Main.screenWidth` x `Main.screenHeight` (578x324 there, the
  zoomed world screen, against 1019x573 for the interface) put the quad next to
  the player (`world_magenta` cell, 12/12 cells).
- Wiki: `advanced/Drawing.md`, section "Primitives", not pushed.
- Not checked yet: reversed gravity (the game's `DrawTrail` flips y itself when
  `gravDir == -1`), and a phone.

### Review against FNA and the PC game (2026-11-26)

Read side by side with `exmod_tmod/tModLoader/FNA/src` and the PC source.
Fixed:

- `DrawUserPrimitives` converted every vertex to the end of the array; FNA
  uploads `PrimitiveVerts(type, count)` vertices (`numVertices` for the indexed
  call) and steps by the declaration's `VertexStride`, not `sizeof(T)`. The
  renderer now reads the indices first and converts only the range they use,
  which also covers big vertex buffers with `baseVertex`.
- `VertexDeclaration.FromType` no longer rejects a declaration whose stride
  differs from the struct size (FNA does not check it).
- Buffers: tML's FNA has `SetDataOptions options = None` on every
  `VertexBuffer.SetData`; `GetData` on a `WriteOnly` buffer throws
  `NotSupportedException`; a size mismatch throws `InvalidOperationException`.
- `Matrix.ToString` uses FNA's format.
- `BasicEffect`: `LightingEnabled`, `FogEnabled` and `EnableDefaultLighting`
  keep their values (with FNA's default light values) and log once instead of
  throwing; `DirectionalLight0..2`, `IEffectLights` and `IEffectFog` added. The
  color math is FNA's (`(DiffuseColor + EmissiveColor) * Alpha`, then texture ×
  diffuse × vertex color).
- `MiscShaderData.Apply()`: on PC it is `Apply(DrawData? drawData = null)`; in
  BL the inherited `ShaderData.Apply()` won the overload and skipped
  `uColor`/`uOpacity`/images. `Apply` is now written by hand
  (`!MiscShaderData.Apply`), and it also stores the `MatrixTransform` the PC
  game sets (`UseProjectionMatrix`, `UseTransformMatrix`), since the game's
  `SetValue(Matrix)` is empty.
- `Main.GameViewMatrix.NormalizedTransformationMatrix`: the phone builds it from
  the physical viewport, while the world is drawn in `Main.screenWidth` x
  `Main.screenHeight` (578x324 against 1600x900 on MuMu), so trails using it
  landed shrunk in the corner. It is now written by hand with PC's formula on
  the virtual screen (`!SpriteViewMatrix.NormalizedTransformationMatrix`), and
  the renderer uses it when an effect's matrix was never set from C#.

Found, then fixed the same day:

- `GraphicsDevice.BlendState` was ignored by primitives: the game's
  `GetMaterial` picks the material by pass and textures only, so an additive
  trail drew alpha-blended (probe: gray over red stayed gray). Now
  `PrimitiveRenderer` packs the device's `BlendState` (the six XNA values,
  cached per object) and hands it to the new NativeApi entry
  `SpriteFxSetBlend` around `DrawGeometry`; `hkApply` stores it in the draw's
  record, and on the render thread `drawMarked` sets `glBlendFuncSeparate` and
  `glBlendEquationSeparate` for that draw and puts Unity's state back. Unity's
  blend state comes from new hooks on `glBlendFunc(Separate)` and
  `glBlendEquation(Separate)` (read once with `glGet`, then shadowed), so no
  draw queries GL. It covers draws marked by `SpriteFx` (`BasicEffect` and mod
  `.fxc` passes); a primitive drawn with one of the game's own shaders keeps
  that material's blending. `cstprim` cells: additive gray over red gave
  (255, 130, 132), `NonPremultiplied` half blue over green (0, 125, 132), and
  everything drawn after stayed right; `csspritefx`, `csexspritefx` and `csfxc`
  unchanged.

Not carried yet: `SamplerStates`, `RasterizerState` (culling, scissor) and
depth.

Tests after the review: `cstprim` 16/16 and 13/13 cells (with the
`MiscShaderData` + `UseProjectionMatrix` trail in world space), 179 test packs
compile.

### Cost (2026-11-26, MuMu 16384, `tools/tests/cstprimperf`)

MuMu runs ARM code through houdini, so managed code is far slower than on a
phone, and runs vary by ~30%. Relative numbers:

| Case (per frame, world layer) | Frame | Draw time | Per call |
|---|---|---|---|
| idle | 16.7-17.0 ms | 0 | |
| 20 `BasicEffect` trails, 60 vertices | 16.8-17.0 ms | 0.9-1.4 ms | 43-72 µs |
| 100 trails | 18.4-20.3 ms | 6.3-6.8 ms | 63-68 µs |
| 20 indexed `.fxc` trails (`MiscShaderData`) | 17.0-18.0 ms | 1.9-2.3 ms | 94-113 µs |
| 20 game `DrawGeometry` calls, same size | 16.7-16.9 ms | 0.5-0.8 ms | 25-40 µs |
| 20 draws of 2 triangles from a 4000-vertex array | 16.8-17.1 ms | 0.8-1.0 ms | 40-51 µs |

- A 60-vertex `DrawUserPrimitives` costs ~8 µs more than the game's own
  `DrawGeometry` of the same size (state and conversion in C#); the rest of a
  trail is the test generating its vertices (12-23 µs) and the pass `Apply`
  (5-9 µs, mostly game calls).
- `MiscShaderData.Apply` is 18-29 µs against 1.5-3 µs for the game's own
  `Apply_`; the matrix bookkeeping is a few game reads, and each managed call
  costs ~0.5-1 µs under houdini.
- Taken out of the hot path during the review: allocations in `SetValue`
  (1.4 → 0.9 µs), the array clone in `SetValue(Matrix)` (3.3 → 1.5 µs),
  `World * View * Projection` recomputed only when one changes (FNA's
  dirty flag), `in` on `Matrix` operators, the per-vertex matrix copy, the
  cached `CurrentTechnique`, the cached `MatrixTransform` parameter, and the
  normalized matrix built in C# instead of six game `Matrix.Create*` calls.
- Not measured on a phone yet.

## Phase 2: GPU vertex shader (new APK)

`SpriteFx` links the pass's MojoShader vertex shader with its pixel shader,
binding the attributes to the Unity mesh's locations and filling
`vs_uniforms_vec4`, so any tML vertex shader runs as written. With it, the
`CustomVertexInfo` overload (by hand in `GraphicsDevice`) carries a 3-float
texture coordinate.

## Phase 3: render targets

A real `RenderTarget2D` (draw into it, then sample or `GetData`) needs an FBO and
`glReadPixels` on the render thread: the game build stripped
`RenderTexture.active` and `ReadPixels`. Planned apart.
