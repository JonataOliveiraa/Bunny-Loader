# Screen effects: shaders over the world

A screen effect changes **what is already drawn**: it bends the image around
a black hole, ripples it where a beam hits, tints it red when the player is
dying, pixelates it, flashes it. It is post-processing in GLSL, the same idea
as tModLoader's `Filters.Scene` and `ScreenShaderData`, but written for
Terraria Mobile, where those filters do not exist.

Effects are drawn **between the world and the interface**: the hotbar, the
minimap and the buttons stay sharp on top. They are client-side visuals
(each device draws its own), and every player can turn them all off in
Settings.

```js
// a ready-made effect, following a projectile
ScreenPresets.BlackHole.Spawn(projectile, { radius: 140 });

// your own, with your own shader (Content/Effects/Rift.js + Rift.frag)
export class Rift extends ModScreenEffect {
    SetDefaults() { this.Radius = 110; }
}
Rift.Spawn(npc.Center);
```

The API comes in layers. Use the one you need:

| Layer | What it is | When |
|---|---|---|
| `ScreenPresets` | 18 ready-made effects, each a class you can extend | A shockwave, a flash, a tint: one line |
| `ModScreenEffect` | The base class: life cycle, following, fades, keys | Your own effect |
| `.frag` / `static Source` | A GLSL fragment shader with a standard library | Your own look |
| `cmd` (`ScreenCommands`) | Draw steps: several passes, off-screen targets, textures | Blur, bloom, anything multi-pass |
| `d` (`DistortionMap`) | Shapes that displace the image, merged into one pass | Swirls, lenses, waves |
| `ScreenGraphics`, `ScreenUtils` | Shaders, textures, passes, blend modes; space, masks, easing, tweens, curves, colors | The tools under all of the above |

The [Example Mod](#in-the-example-mod) uses all of it.

## Contents

- [Quick start: the presets](#quick-start-the-presets)
- [Your own effect: `ModScreenEffect`](#your-own-effect-modscreeneffect)
- [Writing the shader](#writing-the-shader)
- [Draw steps: `cmd`](#draw-steps-cmd)
- [Distortion: `d`](#distortion-d)
- [Masks, colors and animation](#masks-colors-and-animation)
- [Color grading with a LUT](#color-grading-with-a-lut)
- [Coordinates and zoom](#coordinates-and-zoom)
- [Tools for effect makers](#tools-for-effect-makers)
- [Cost](#cost)
- [Limits and pitfalls](#limits-and-pitfalls)
- [In the Example Mod](#in-the-example-mod)

---

## Quick start: the presets

```js
ScreenPresets.Shockwave.Spawn(npc.Center, { radius: 320, duration: 45 });
ScreenPresets.Flash.Spawn(null, { color: '#ffffff', duration: 10 });
ScreenPresets.Tint.Spawn(null, { saturation: 0, fadeIn: 30, key: 'mymod:gray' });
```

`Spawn(center, options)`:

- `center` is a world position (`{ X, Y }`, a `Vector2`), an **entity**
  (player, NPC, projectile: the effect follows it and dies with it) or `null`
  (the whole screen);
- `options` are the effect's properties in camelCase: `radius`, `intensity`,
  `color`, `duration`, `fadeIn`, `fadeOut`, `key`, `mask`, `layer`, plus each
  preset's own (`strength`, `spin`...). An unknown option throws, with its
  name.

Spawn returns the effect, which you can keep and change every frame
(`fx.Radius = ...`, `fx.Intensity = ...`), animate (`fx.Tween(...)`) or kill
(`fx.Kill()`).

| Preset | What it does | Own options (defaults) |
|---|---|---|
| `Twirl` | Spins the image around the center | `spin` 3 (radians), `speed` 0.8 |
| `Shockwave` | A ring that grows to `radius` over `duration`, pushing outwards | `thickness` 0.18, `strength` 0.6 |
| `Ripple` | Water rings. With `duration`, one pulse of `count` rings; with `duration: 0`, endless | `count` 3, `wavelength` 24, `strength` 4, `speed` 1.5 |
| `BlackHole` | Pulls and spins the image, with a dark core and a lit ring in `color` | `pull` 0.35, `spin` 2.5, `core` 0.18 |
| `HeatHaze` | Hot air: the image wobbles upwards (distortion only) | `strength` 2.5, `scale` 40, `speed` 1.5 |
| `Lens` | Magnifies (`strength` > 0) or pinches (< 0) (distortion only) | `strength` 16 |
| `ForceField` | A glass bubble: bends the image at the edge, lit rim, `Hit(position)` lights the side that was hit | `refraction` 0.12, `rim` 0.3, `fill` 0.06, `shimmer` 0.8 |
| `Blur` | Gaussian blur, around the center or full screen | `strength` 4 |
| `ZoomBlur` | Speed lines: the image drags away from the center | `strength` 0.1, `clear` 60, `samples` 12 |
| `Bloom` | Bright parts glow, in `color` | `threshold` 0.65, `spread` 6 |
| `Echo` | Mixes in the previous frame: moving things leave a trail | `persistence` 0.75, `drift` [0, 0] |
| `ChromaticAberration` | Red and blue split apart, radially or in a `direction` | `amount` 4 (px at the screen corner), `direction` null |
| `Pixelate` | Blocks locked to the world (they do not shimmer when the camera moves) | `size` 6 |
| `Glitch` | Slices jump sideways and colors split, `rate` times a second | `strength` 0.6, `slices` 24, `rate` 14, `split` 4, `shift` 0.06 |
| `Tint` | Full color correction where the mask allows: blend a color, saturation, contrast, brightness, gamma, hue, temperature, pulse | `blend` Color, `amount` 0.5, `preserveLuminance`, `saturation`, `contrast`, `brightness`, `gamma`, `hueShift`, `temperature`, `pulse: { period, amount }` |
| `Flash` | A `Tint` with additive blend that fades out on its own | as `Tint`; `duration` 12 |
| `ColorGrade` | A color lookup table made in a photo editor ([below](#color-grading-with-a-lut)) | `lut` (path), `amount` 1 |
| `Vignette` | Darkened (or colored) screen edges | `strength` 0.6, `start` 0.45, `softness` 0.55, `roundness` 1 |

Sizes are in **world pixels** and time in **ticks** (1/60 s). Presets are
plain classes: `class VoidRift extends ScreenPresets.Twirl { ... }` keeps the
shader and changes the rest.

## Your own effect: `ModScreenEffect`

```js
// Content/Effects/Rift.js — registered on its own, like any Content/ class
export class Rift extends ModScreenEffect {
    Open = 0;

    SetDefaults() {
        this.Radius = 110;
        this.Color = Color.new(180, 90, 255);
    }

    OnSpawn() {
        this.Tween('Open', 1, 25, ScreenUtils.Easing.OutBack);
    }

    Update() {
        this.Uniforms.uOpen = this.Open;     // reaches `uniform float uOpen;`
    }

    DrawDistortion(d) {
        d.Swirl(this.Center, this.Radius, 1.6 * this.Open);
    }
}
```

The shader is found, in this order: the class's own `static Source`; a
`Shader` set in `SetDefaults` (a `ScreenGraphics.Shader` or a path); the
**`.frag` next to the `.js`** (`Content/Effects/Rift.frag`); an inherited
`static Source`. With none, the effect can still distort (`DrawDistortion`)
but not draw.

### Properties

| | |
|---|---|
| `Center` | `{ X, Y }` in the world, or `null` for the whole screen. |
| `Follow` | The entity it follows (set by `Spawn(entity)`): the effect moves with it and dies with it, also when the game reuses the slot for another type. |
| `Radius` | World pixels (`uRadius` in the shader). |
| `Intensity` | Multiplies `uIntensity` (with the fades). |
| `Color` | A game `Color`, `[r, g, b, a]` from 0 to 1 or `'#rrggbb'` (`uColor`). |
| `Duration` | Ticks; 0 = until `Kill()`. |
| `FadeIn`, `FadeOut` | Ticks. They scale `uIntensity`. |
| `Key` | Spawning with a key that is alive **renews** that effect instead of stacking a new one (it moves, takes the new options and its age restarts). |
| `Layer` | `ScreenEffects.Layer.World` (default: under the interface) or `Screen` (over everything). |
| `Order` | Lower draws first. |
| `Fullscreen` | Draw on the whole screen instead of the circle around the center. |
| `CaptureMargin` | World pixels beyond `Radius` that the shader reads (8). Raise it if your shader samples far from the pixel it draws. |
| `Mask` | A `ScreenUtils.Mask`: where the effect applies ([below](#masks-colors-and-animation)). |
| `Uniforms` | `{ name: value }` sent to the shader every frame. |
| `Age`, `Progress`, `TimeLeft`, `Opacity`, `Active` | Ticks lived; 0 to 1 over `Duration`; ticks left; the current fade; alive. |

### Methods

| | |
|---|---|
| `SetDefaults()` | The starting values. |
| `OnSpawn()` | After the options are applied. |
| `Update()` | Every frame, before drawing. |
| `ShouldDraw()` | `false` skips drawing this frame (the effect keeps living). |
| `DrawDistortion(d)` | Shapes for the shared distortion map. |
| `Draw(cmd)` | The draw steps. The default draws the effect's shader in its area. |
| `OnKill()` | Once, when it dies. |
| `Kill()`, `Tween(property, to, ticks, easing, options)` | |
| `static Spawn(center, options)` | |

`ScreenEffects` manages them all: `Spawn(Class, center, options)`,
`Find(key)`, `Kill(key)`, `KillAll()`, `Active` (the live ones), `Classes`
(every known effect class), `Enabled` (the player's setting), `Debug`,
`HotReload`, `Reload()`.

Time stops while the game is paused and moves at the same speed at 45, 60 or
90 fps.

## Writing the shader

A screen shader is a **GLSL ES 3.00 fragment shader**. Bunny Loader puts a
prelude in front of it, so it only writes `main()`:

```glsl
// Content/Effects/Rift.frag
#include <bl/screen>
#include <bl/noise>
uniform float uOpen;

void main() {
    vec2 d = blFromCenter(vUV);          // from the effect's center, aspect-corrected
    float r = length(d);
    if (r > uRadius) discard;            // only the circle; outside stays as it was

    float edge = blFbm(vec2(atan(d.y, d.x) * 3.0, uTime), 3);
    vec3 col = blScreen(vUV).rgb + uColor.rgb * edge * uOpen * uIntensity;
    fragColor = vec4(col, 1.0);
}
```

`gl_FragColor` and `texture2D` also work (GLSL 1.00 style). `discard` keeps
the original pixel. Compile errors open the error panel with **your file and
line** (`Content/Effects/Rift.frag:12: 'fragColour' : undeclared identifier`)
and the effect stops drawing until the shader is fixed.

### What every shader has

| | |
|---|---|
| `vUV` | This pixel, 0 to 1 over the screen, **y up**. |
| `fragColor` | The output. |
| `blScreen(uv)` | What is drawn on screen there (`uScreen`). |
| `blSource(uv)` | The step's source (`uSource`), when it has one. |
| `uCenter` | The effect's center, in the same 0..1 as `vUV`. |
| `uRadius` | The radius, as a fraction of the screen **height**. |
| `uIntensity` | `Intensity` × the fades. |
| `uColor` | `Color` (rgba). |
| `uTime`, `uAge` | Seconds since boot; seconds the effect has lived. |
| `uProgress` | 0 to 1 over `Duration`. |
| `uAspect`, `uResolution` | Width/height; the screen size in pixels. |
| `uScreenTexel`, `uSourceTexel` | One pixel of each texture, in UV. |

### The library (`#include <bl/...>`)

| Include | Gives |
|---|---|
| `bl/screen` | `blFromCenter(uv)` (offset from the center, aspect-corrected, in height units: compare with `uRadius`), `blToUV(d)`, `blAtCenter(d)` (back to UV), `blDistance(uv)` |
| `bl/math` | `BL_PI`, `BL_TAU`, `blRotate(v, angle)`, `blRemap(v, a, b, c, d)`, `blSaturate(v)` |
| `bl/noise` | `blHash(p)`, `blValueNoise(p)`, `blNoise(p)` (gradient noise, 0..1), `blFbm(p, octaves)` |
| `bl/color` | `blLuma(c)`, `blRgbToHsv`, `blHsvToRgb`, `blSetLuma`, `blBlend(base, top, mode)` with `BL_BLEND_*`, `blAdjust(c, saturation, contrast, brightness, gamma, hue, temperature)`, `blLut(lut, c, size)` |
| `bl/shapes` | `blCircle(distance, radius, feather)`, `blRing(distance, radius, width, feather)`, `blFalloff(distance, radius)` |
| `bl/mask` | `blMask(vUV)`: the effect's `Mask` (1 inside, 0 outside) |

### Your own uniforms and textures

`Uniforms` on the effect, or `uniforms` in a draw step, take a number, a
`[up to 4]` array, a `Vector2`, a game `Color` (converted to 0..1), `'#hex'`,
a boolean (0/1) or `ScreenGraphics.Array(components, values)` for
`uniform vec4 uPoints[8];`.

Textures are PNGs from the mod:

```js
const noise = ScreenGraphics.Texture.Load('Content/Effects/Noise.png', { wrap: 'repeat' });
cmd.DrawShader(this.Shader, { textures: { uNoise: noise } });   // uniform sampler2D uNoise;
```

`filter: 'nearest'` keeps pixel art crisp; `wrap: 'repeat'` tiles. In GLSL,
`v = 0` is the bottom of the image, like `vUV`. Up to 6 textures per step.

## Draw steps: `cmd`

`Draw(cmd)` records steps; they run in order after every effect has
recorded. One step is usually enough:

```js
Draw(cmd) {
    cmd.DrawShader(this.Shader, {
        uniforms: { uStrength: this.Strength },
        mask: ScreenUtils.Mask.Vignette(),
    });
}
```

`cmd.DrawShader(shader, options)` copies the effect's area from the screen
and redraws it with the shader. Every option defaults to the effect's own:
`center`, `radius`, `fullscreen`, `margin` (world pixels the shader reads
beyond the radius), `uniforms`, `textures`, `mask`, `source`, `layer`.

For more than one pass, draw into off-screen targets:

```js
Draw(cmd) {
    const { Passes, Sources } = ScreenGraphics;
    const a = cmd.Target('blur.a', 0.5);    // half resolution
    const b = cmd.Target('blur.b', 0.5);
    cmd.Blit(Sources.Screen, a, Passes.BlurH, { uniforms: { uBlurRadius: 4 } });
    cmd.Blit(a, b, Passes.BlurV, { uniforms: { uBlurRadius: 4 } });
    cmd.DrawShader(this.Shader, { textures: { uBlurred: b } });
}
```

- `cmd.Target(name, scale, { persistent })`: a texture the size of the screen
  × `scale`. Temporary targets are shared by name and may hold garbage outside
  what you drew this frame; `persistent: true` keeps the content between
  frames (per effect class + name), for feedback effects.
- `cmd.Blit(source, dest, shader, options)`: reads `source` as `uSource` and
  writes `dest`.
- `ScreenGraphics.Passes`: `Copy`, `Threshold` (`uThreshold`, `uKnee`),
  `BlurH` / `BlurV` (`uBlurRadius` in source texels).
- `ScreenGraphics.Sources.Screen` (what is drawn) and `Sources.History` (the
  previous frame with its effects; it is only kept on frames where someone
  reads it).

Every target covers the whole screen, so `texture(uAnyTarget, vUV)` reads the
same place in all of them.

## Distortion: `d`

Displacements from every effect in a frame are **added together in one
pass**: ten black holes cost about as much as one. `DrawDistortion(d)`
receives the shared map:

| | |
|---|---|
| `d.Radial(center, radius, strength)` | Pushes out (`strength` > 0, a lens) or pulls in (< 0), strongest at the center. |
| `d.Swirl(center, radius, angle)` | Rotates the image `angle` radians at the center, none at the edge. |
| `d.Ring(center, ringRadius, width, strength)` | A ring that pushes outwards (a shockwave). |
| `d.Haze(center, radius, strength, { scale, speed })` | Hot-air wobble. |
| `d.Push(center, radius, direction, strength)` | Shifts everything in one direction. |

Positions in the world, sizes and strengths in world pixels; the effect's
`Intensity` and fades scale them. Up to 32 shapes per frame. The pass is drawn
just before the first effect that distorted, so a black hole's core is drawn
on top of the image it already pulled.

## Masks, colors and animation

`ScreenUtils` holds the helpers (`const { Mask, Easing, Tween } = ScreenUtils;`):

**`Mask`** — where an effect applies, with a soft edge. Presets and
`cmd.DrawShader` apply it on their own; in your GLSL, `#include <bl/mask>` and
multiply by `blMask(vUV)`. A bounded mask (circle, rectangle) also shrinks the
area drawn, so it costs less.

```js
Mask.Circle(player.Center, 120, { feather: 60 }).Inverted()   // everything but the circle
Mask.Ring(center, 200, 40)
Mask.Rect(npc.Hitbox, { feather: 16 })
Mask.Gradient('Up', { start: 0, end: 0.6 })                   // strong at the bottom
Mask.Vignette({ start: 0.3, softness: 0.6 })
Mask.Texture(ScreenGraphics.Texture.Load('Content/Effects/Shape'), { channel: 'luma' })
// falloff of the edge: { falloff: Mask.Falloff.Linear | Smooth | Exponential }
```

**`ScreenGraphics.BlendMode`** — `Normal`, `Multiply`, `Screen`, `Overlay`,
`SoftLight`, `Add`, `Color`, `Luminosity` (the same numbers as `BL_BLEND_*`
in `blBlend`).

**`ColorMath`** — `ToFloats(color)`, `FromHex('#ff8000')`, `Hsv(degrees, s, v)`,
`Lerp(a, b, t)`, `Temperature(kelvin)` (1000 K red, 6500 K white, 12000 K
blue), `Luma(color)`.

**`Easing`** — `Linear`, `In/Out/InOut` × `Sine`, `Quad`, `Cubic`, `Expo`,
`InBack`, `OutBack`, `OutElastic`, `OutBounce`.

**`Tween`** — animates a number on any object over ticks:
`fx.Tween('Radius', 300, 20, Easing.OutBack, { delay: 10, onDone: () => fx.Kill() })`.
A new tween on the same property replaces the old one; tweens on an effect
stop when it dies; they stop while the game is paused.

**`Curve`** — a value over the effect's life:
`const pulse = new Curve([[0, 0], [0.2, 1], [1, 0]]); Update() { this.Intensity = pulse.At(this.Progress); }`.

## Color grading with a LUT

`ColorGrade` passes the image through a color table made in any photo
editor:

1. Save the neutral table once: `ScreenGraphics.Texture.SaveNeutralLut('Content/Effects/Neutral.png', 16)`
   (in a hook, then copy the file off the device).
2. Open a screenshot of the game in the editor, grade it as you like, and
   apply **the same adjustments** to `Neutral.png`.
3. Ship the graded PNG: `ScreenPresets.ColorGrade.Spawn(null, { lut: 'Content/Effects/Hell.png', amount: 0.8 })`.

The table is a strip of size² × size, the ReShade layout: red grows to the
right, blue from slice to slice, green **downwards** (the top row is green 0).

## Coordinates and zoom

Effects take **world positions** and **world-pixel sizes**, and the shader
gets fractions of the screen. `ScreenUtils.Space` converts:
`WorldToUV(pos)`, `UVToWorld(uv)`, `PixelsToUV(px)` (to height units, like
`uRadius`), `UVToPixels(f)`, `Aspect`, `Size`, `IsOnScreen(pos, margin)`.

On the phone, pinch zoom changes the game's virtual screen
(`Main.screenWidth/Height`); since everything goes through it, a radius in
world pixels follows the zoom on its own: a 100 px twirl covers the same
tiles at any zoom.

## Tools for effect makers

**`/fx` in the chat** (always available, only on your device):

| | |
|---|---|
| `/fx list` | The live effects: class, key, size, age. |
| `/fx spawn <Effect> [radius] [ticks]` | Spawns a preset or any mod effect on the player. A wrong name lists them all. |
| `/fx kill [key \| Effect]` | Kills those (all, without an argument). |
| `/fx reload` | Re-reads every `.frag` now; changed ones recompile. |
| `/fx watch` | Re-reads them every half second: edit the file on the device and see it change in the game. |
| `/fx debug` | The overlay: each step's area (dashed box) and radius (circle) in the effect's color, the distortion in white, and a panel with the live effects, steps per frame, GPU time and fps. |
| `/fx on`, `/fx off` | The player's setting (saved). |
| `/fx stats` | The averages in one line. |

The same from code: `ScreenEffects.HotReload = true`, `ScreenEffects.Reload()`,
`ScreenEffects.Debug = true`. Hot reload only covers shaders that come from a
file (the `.frag` next to the `.js`, or `ScreenGraphics.Shader.FromFile`), not
`static Source`.

**Settings > General > "Screen Effects"**: On/Off, in the main menu and in
the world, saved in `BunnyLoader.screen.json` in the game's save folder.
When off, every mod's effects keep living (timers, keys, tweens) but do not
draw. It is there for players who feel sick with flashes and distortion, or
want the frame time back. Do not build gameplay that depends on seeing an
effect.

## Cost

Measured on a Mali-G57 phone (2340×1080): an effect only copies and redraws
its own area. An area of 100 thousand pixels costs ~0.8 ms of GPU per frame,
one of 500 thousand ~1.2 ms; the GPU time of the whole frame did not change
beyond the noise, and the game stayed at 60 fps. CPU: ~0.5 ms on the render
thread and ~0.2 ms on the game thread while any effect is alive; next to
nothing when none is.

On the MuMu emulator, per frame: distortion only (`Lens`, `ForceField`, a
`Pixelate` circle) ~0.1 ms; `Ripple` 0.2–0.4 ms; full-screen color
(`Tint`, `ColorGrade`, `Vignette`) 0.9–1.6 ms; `Blur` and `Bloom` ~1.5 ms;
`ZoomBlur` (12 reads per pixel) ~2.9 ms.

To keep effects cheap:

- prefer a radius or a bounded mask to `fullscreen`;
- many distortions are one pass: use `DrawDistortion` instead of a shader when
  you only displace;
- blur at half resolution (`cmd.Target(name, 0.5)`);
- give short-lived effects a `duration`, and `key` repeating ones so they
  renew instead of stacking.

## Limits and pitfalls

- **Uniforms persist per shader.** A GL program keeps the last value of each
  uniform: if two effects share a shader and one skips a uniform, it inherits
  the other's value. Send every uniform your shader reads, every frame (the
  presets do).
- Effects are visual and **client-side**: spawn them where the visual happens
  (`AI`, `OnKill`, a hook), not only on the server (`Main.netMode !== 2`).
- Up to 32 distortion shapes and 32 debug outlines per frame, 6 mod textures
  per step.
- Keep the effect you spawned in a field of the entity's instance
  (`Ripple = null;`) and kill it in `OnKill`: each projectile or NPC has its
  own instance ([guide 4](04-conteudo-novo.md#template-and-instance)).
- The phone's own screen filters (`Filters.Scene`) are a different system and
  some do not show on the emulator.

## In the Example Mod

| File | What it shows |
|---|---|
| [`ExampleLastPrism`](../../samples/ExampleMod/content/Content/Items/Weapons/Magic/ExampleLastPrism.js), [`ExampleLastPrismHoldout`](../../samples/ExampleMod/content/Content/Projectiles/ExampleLastPrismHoldout.js), [`ExampleLastPrismBeam`](../../samples/ExampleMod/content/Content/Projectiles/ExampleLastPrismBeam.js) | tModLoader's Last Prism, ported: ten beams that focus while charging. Where tModLoader ripples the water, the beam keeps a `Ripple` where it hits, moving with it and growing with the charge. |
| [`ExampleRiftStaff`](../../samples/ExampleMod/content/Content/Items/Weapons/Magic/ExampleRiftStaff.js), [`ExampleRiftProjectile`](../../samples/ExampleMod/content/Content/Projectiles/ExampleRiftProjectile.js) | A projectile that carries its own effect and, on death, closes it and fires a `Shockwave`, a `Flash` and a `ChromaticAberration`. |
| [`ExampleRiftEffect.js`](../../samples/ExampleMod/content/Content/Effects/ExampleRiftEffect.js) + [`.frag`](../../samples/ExampleMod/content/Content/Effects/ExampleRiftEffect.frag) | A mod effect with its own shader, a uniform animated by a `Tween`, and distortion. Try `/fx spawn examplerifteffect` and `/fx watch`. |
| [`ExampleLowLifePlayer`](../../samples/ExampleMod/content/Common/Players/ExampleLowLifePlayer.js) | A `ModPlayer` that tints the screen edges red and pulsing below 25% life, and fades it out with a `Tween` when healed. |
