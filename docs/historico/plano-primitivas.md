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
  tML mods write it the first way, so ProxyGen needs a fix (for example, getters
  of game objects returning a `ref` to a slot).

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
