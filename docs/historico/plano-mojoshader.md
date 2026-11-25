# Plan: compiled effects (`.fxc`) through MojoShader

Status: stage 0 done in `f0300b47` (2026-11-24), tested on MuMu only. Stages 1 to 4 not started.
Research and the proof of concept: [pesquisa-shaders.md](pesquisa-shaders.md), section
"2026-11-24".

GLSL stays the language of Bunny Loader's own effects. `.fxc` (output of `fxc /T fx_2_0`) is a
compatibility input so effects from tModLoader mods load as they are. Every stage ends with a
test package under `tools/tests/` and, when it adds API, an example in
`samples/ExampleModCSharp`.

## Stage 0: proof of concept (done)

- MojoShader in `cpp/third_party/mojoshader`, GLSL profiles only.
- `graphics/FxcEffect.cpp`: pixel shader of a pass rewritten into the screen effect format,
  preshader run on every draw, header check before MojoShader.
- C# `FxcEffect` and `FxcScreenEffect`.
- `tools/tests/csfxc`: 11/11 on MuMu 16384.
- Refinements (18/18): `Load` cached per file, passes converted at load, allocation-free
  `SetValue` by index with register padding, shader resolved when the pass is set, game
  `UnscaledScreen*` values and the remaining `ScreenShaderData.Apply` parameters, `.fxc` hot
  reload through `ScreenEffects.Reload`.

## Stage 1: phone and small gaps

Goal: what already exists works on a real phone and covers the common shader features.

- Run `csfxc` on the phone (SM-A156M, Mali-G57) and measure the GPU and CPU cost of one
  fullscreen `.fxc` effect against the same effect written in GLSL.
- Integer and bool registers: send `ps_uniforms_ivec4` and `ps_uniforms_bool` like the float
  array.
- Extra samplers: honor the effect's `sampler_state` (address mode, filter) for `ps_s1..3`.
- `VPOS`/`VFACE`: set `ps_vposFlip` from the viewport.
- Passes that pick their pixel shader through a preshader: compile each variant, choose one per
  commit.
- Pass render states (`AlphaBlendEnable`, `SrcBlend`, `DestBlend`) mapped to `BlendMode`.
- Add `csfxc` to the list in `tools/tests/build-cs.sh`.

Done when: `csfxc` passes on the phone and MuMu with a case for each item above, and the cost
is recorded in `pesquisa-shaders.md`.

## Stage 2: tModLoader API for screen filters

Goal: a tModLoader screen filter ports without touching its code.

- `Mod.Assets.Request<Effect>("Effects/X")` and `ModContent.Request<Effect>` return an effect
  backed by `FxcEffect` when the file is a mod `.fxc` (the bridge `Effect` is the game's
  Unity-backed struct: needs a mod variant both can share).
- `new ScreenShaderData(effect, "Pass")` with `UseColor`, `UseSecondaryColor`, `UseOpacity`,
  `UseIntensity`, `UseProgress`, `UseTargetPosition`, `UseDirection`, `UseImageOffset`,
  `UseImage`, `Shader.Parameters["x"].SetValue`.
- `Filters.Scene["Mod:X"] = new Filter(...)`, `Activate`, `Deactivate`, `IsActive`, opacity
  fade and `ModSceneEffect.SpecialVisuals`: a filter with a mod effect is drawn by our renderer
  (it then shows on MuMu too); vanilla filters keep the game's path.
- Example: a tModLoader screen filter ported into `samples/ExampleModCSharp` with its `.fx`
  source next to the `.fxc`.
- Wiki `advanced/Screen-Effects.md`: replace the "a mod's own HLSL does not exist on mobile"
  line.

Done when: the example activates and deactivates by biome/event on MuMu and the phone, and a
test checks each `Use*` call reaches the shader.

## Stage 3: game textures in `UseImage`

Goal: filters that sample `Images/Misc/Perlin`, `Images/Misc/noise` and other game textures
work.

- Find how to get a GL texture for a game texture: read the image from the game's files, or the
  Unity native texture pointer if the build kept that call.
- Cache per texture; same sampler rules as stage 1.

Done when: a filter using `UseImage("Images/Misc/Perlin")` draws the same pattern as on PC.

## Stage 4: `.xnb` effects

Goal: older mods that ship the effect inside an XNB load too.

- XNB header and the `EffectReader` payload (length + effect bytes).
- LZX decompression for compressed XNB (port from FNA).

Done when: the same effect loads from `.xnb` (compressed and not) and from `.fxc` with identical
registers.

## Discarded for now

```
// TODO: sprite shaders (done on MuMu 2026-11-25, see pesquisa-shaders.md): uImage2/3, reload of sprite passes, programs compiled before the hooks, phone run and cost.
// TODO: vertex shaders and primitives (trails, DrawUserPrimitives): needs our own GL primitive renderer and a layer inside the world drawing.
// TODO: .fx source on the device: needs an HLSL compiler (vkd3d-shader, LGPL) inside the app.
// TODO: harden MojoShader against crafted .fxc files: a bad file can still crash the game (bug, not a security issue).
```
