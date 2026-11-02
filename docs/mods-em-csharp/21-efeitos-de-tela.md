# Screen effects

Post-processing over the world, drawn **under the interface**: twirls, shock
waves, blur, bloom, color grading, distortion, or your own GLSL shader. There
are two layers:

- **The game's own effects**, with tModLoader's names: `Filters.Scene`,
  `SkyManager.Instance`, `Overlays.Scene` and
  `Player.ManageSpecialBiomeVisuals`. They switch on what the mobile game
  already has (`"HeatDistortion"`, `"MoonLord"`...).
- **Bunny Loader's effects** (`BunnyLoader.ModScreenEffect` and the other
  `Screen*` classes), for everything else. tModLoader has no counterpart: on
  the PC a mod ships an HLSL `Effect`; the phone runs Unity and cannot compile
  one, so Bunny Loader draws GLSL with its own renderer.

```csharp
using BunnyLoader;

ScreenEffects.Spawn<ScreenPresets.Shockwave>(npc.Center, fx =>
{
    fx.Radius = 320;
    fx.Duration = 45;
});
```

## The game's effects

```csharp
using Terraria.Graphics.Effects;

Filters.Scene.Activate("HeatDistortion", player.Center);
Filter heat = Filters.Scene["HeatDistortion"];
heat.GetShader().UseIntensity(0.5f);
Filters.Scene.Deactivate("HeatDistortion");

SkyManager.Instance.Activate("MoonLord", player.Center);
bool on = SkyManager.Instance["MoonLord"].IsActive();

player.ManageSpecialBiomeVisuals("HeatDistortion", inZone, player.Center);
```

`ModSceneEffect.SpecialVisuals` is the place for `ManageSpecialBiomeVisuals`,
as in tModLoader. Differences from the PC:

- Game objects are created with `New`, not `new`:
  `Filter.New(ScreenShaderData.New("FilterMiniTower"), EffectPriority.High)`.
- A mod's own HLSL (`new ScreenShaderData(ModContent.Request<Effect>(...))`) and
  a mod's own `CustomSky` subclass do not exist on mobile: use a
  `ModScreenEffect`.
- Extra activation arguments (`Activate(name, pos, args)`) are refused.
- The mobile game draws its filters through Unity's post-processing, which
  the MuMu emulator does not show. Skies draw everywhere.

## Quick start: the presets

`ScreenPresets` has 18 ready effects. They are ordinary `ModScreenEffect`
classes: spawn them, or extend one and change what you want.

| Preset | What it does | Main properties |
|---|---|---|
| `Twirl` | The image spins around the center | `Spin`, `Speed` |
| `Shockwave` | A ring that leaves the center over `Duration` | `Thickness`, `Strength` |
| `Blur` | Blur in a circle or full screen (two half-resolution passes) | `Strength` |
| `Bloom` | Bright areas glow in the effect's color | `Threshold`, `Spread` |
| `Echo` | Mixes in the previous frame: motion trails | `Persistence`, `Drift` |
| `BlackHole` | Pulls and spins the image, dark core with a lit ring | `Pull`, `Spin`, `Core` |
| `HeatHaze` | Hot air ripples (distortion only) | `Strength`, `Scale`, `Speed` |
| `Tint` | Color correction: tint with a blend mode, saturation, contrast, brightness, gamma, hue, temperature, pulse | `Blend`, `Amount`, `Saturation`, `Pulse`... |
| `ColorGrade` | A LUT made in a photo editor | `Lut`, `LutPath`, `Amount` |
| `Vignette` | Dark (or colored) screen edges | `Strength`, `Start`, `Softness`, `Roundness` |
| `Ripple` | Rings on water: a pulse with `Duration`, endless without | `Count`, `Wavelength`, `Strength`, `Speed` |
| `ForceField` | A glass bubble with a lit rim; `Hit(position)` lights the side hit | `Refraction`, `Rim`, `Fill`, `Shimmer` |
| `ChromaticAberration` | Red and blue split, radial or in a `Direction` | `Amount`, `Direction` |
| `Pixelate` | Blocks locked to the world | `Size` |
| `Flash` | A `Tint` that adds light and fades out | as `Tint` |
| `Glitch` | Slices jump sideways, colors split | `Strength`, `Slices`, `Rate`, `Split`, `Shift` |
| `ZoomBlur` | Speed lines out of the center | `Strength`, `Clear`, `Samples` |
| `Lens` | Magnify (or pinch) the center (distortion only) | `Strength` |

```csharp
ScreenEffects.Spawn<ScreenPresets.Twirl>(Projectile, fx => fx.Radius = 96);
ScreenEffects.Spawn<ScreenPresets.Flash>(fx => fx.Color = new Color(255, 80, 40));
ScreenEffects.Spawn<ScreenPresets.Tint>(fx =>
{
    fx.Color = new Color(200, 20, 20);
    fx.Blend = ScreenGraphics.BlendMode.Multiply;
    fx.Mask = ScreenUtils.Mask.Vignette(0.35f, 0.55f);
    fx.Pulse = new ScreenPresets.TintPulse { Period = 50, Amount = 0.4f };
    fx.Key = "MyMod:lowLife";
});
```

### `ScreenEffects.Spawn`

| Overload | Center |
|---|---|
| `Spawn<T>(setup)` | None: the whole screen |
| `Spawn<T>(Vector2 center, setup)` | A world position (copied) |
| `Spawn<T>(Player / NPC / Projectile, setup)` | Follows the entity and dies with it (an NPC or projectile slot reused by another type counts as dead) |
| `Spawn(Type type, ...)` | The same by type (what `/fx spawn` uses) |

`setup` runs after `SetDefaults` and before `OnSpawn`. With a `Key`, spawning
again **renews** the live effect of the same class (the setup runs on it, its
`Age` goes back to 0) instead of stacking; a different class with the same
key replaces it. `ScreenEffects.Find(key)`, `Kill(key)`, `KillAll()`,
`Active`, `Classes` and `Enabled` complete the manager.

## Your own effect: `ModScreenEffect`

```csharp
using BunnyLoader;

namespace MyMod.Content.Effects;

public class Rift : ModScreenEffect
{
    public float Open { get; set; }

    public override void SetDefaults()
    {
        Radius = 110;
        Color = new Color(180, 90, 255);
    }

    public override void OnSpawn() => Tween(nameof(Open), 1, 25, ScreenUtils.Easing.OutBack);

    public override void Update() => Uniforms["uOpen"] = Open;

    public override void DrawDistortion(DistortionMap d)
    {
        if (Center is Vector2 center) d.Swirl(center, Radius, 1.6f * Open);
    }
}
```

Effects in the mod are loaded like any `ModType` (client only). The shader is,
in this order: `Source` overridden by the class itself, a `Shader` set in
`SetDefaults`, the `.frag` next to the class (`ShaderPath`, by default the
namespace and the class name: `Content/Effects/Rift.frag` above), and last
the `Source` inherited from a preset (a mod class that extends `Twirl` uses
the twirl shader unless it has its own).

### Properties

| | |
|---|---|
| `Center` | World position, or null for the whole screen. Updated by `Spawn` when it follows an entity. |
| `Radius` | In world pixels: it follows the zoom on its own. |
| `Intensity`, `Color` | `uIntensity` (times the fades) and `uColor`. |
| `Duration`, `FadeIn`, `FadeOut` | In ticks (1/60 s; they stop while the game is paused). `Duration` 0 lives until `Kill`. |
| `Key`, `Order` | Renewing by key; lower `Order` draws first. |
| `Layer` | `ScreenEffects.Layer.World` (default: under the interface) or `Screen` (over everything). |
| `Fullscreen`, `CaptureMargin` | Draw on the whole screen; how far past `Radius` the shader reads. |
| `Mask` | Where the effect counts (`blMask` in GLSL; bounded masks shrink the drawn area). |
| `Uniforms` | Extra uniforms sent every frame. |
| `Age`, `Progress`, `TimeLeft`, `Opacity`, `Active`, `Following` | State. |

### Methods

`SetDefaults`, `OnSpawn`, `Update` (every frame, after the age moves),
`ShouldDraw`, `DrawDistortion(DistortionMap d)`, `Draw(ScreenCommands cmd)`
(the default draws the shader over the effect's area), `OnKill` and
`Kill()`. `Tween(property, to, ticks, easing, delay, onDone)` animates a number
of the effect.

## Writing the shader

A screen shader is a **GLSL ES 3.00 fragment shader**. Bunny Loader puts a
prelude in front of it, so it only writes `main()`:

```glsl
#include <bl/screen>
#include <bl/noise>
uniform float uOpen;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    if (r > uRadius) discard;

    float edge = blFbm(vec2(atan(d.y, d.x) * 3.0, uTime), 3);
    vec3 col = blScreen(vUV).rgb + uColor.rgb * edge * uOpen * uIntensity;
    fragColor = vec4(col, 1.0);
}
```

`discard` keeps the original pixel. A compile error opens the error panel with
the file and line, and the effect stops drawing until the shader is fixed.
`ScreenGraphics.Shader` has `Status` (`Pending` until it first draws, then
`Ready` or `Failed`) and `Error`.

| Every shader has | |
|---|---|
| `vUV` | This pixel, 0 to 1 over the screen, **y up**. |
| `fragColor` | The output. |
| `blScreen(uv)`, `blSource(uv)` | What is drawn on screen there; the step's source. |
| `uCenter`, `uRadius` | The center (like `vUV`) and the radius as a fraction of the screen **height**. |
| `uIntensity`, `uColor` | `Intensity` × fades; `Color`. |
| `uTime`, `uAge`, `uProgress` | Seconds since boot; seconds of the effect; 0 to 1 over `Duration`. |
| `uAspect`, `uResolution`, `uScreenTexel`, `uSourceTexel` | Screen shape and texel sizes. |

| Include | Gives |
|---|---|
| `bl/screen` | `blFromCenter`, `blToUV`, `blAtCenter`, `blDistance` |
| `bl/math` | `BL_PI`, `BL_TAU`, `blRotate`, `blRemap`, `blSaturate` |
| `bl/noise` | `blHash`, `blValueNoise`, `blNoise`, `blFbm` |
| `bl/color` | `blLuma`, `blRgbToHsv`, `blHsvToRgb`, `blSetLuma`, `blBlend` (`BL_BLEND_*` = `ScreenGraphics.BlendMode`), `blAdjust`, `blLut` |
| `bl/shapes` | `blCircle`, `blRing`, `blFalloff` |
| `bl/mask` | `blMask(vUV)` |

Uniform values: `float`, `int`, `bool`, `float[]` (up to 4), `Vector2/3/4`,
`Color`, `"#rrggbb"` or `ScreenGraphics.Array(4, values)` for
`uniform vec4 uPoints[8];`. **Send every uniform every frame**: a GL program
keeps the last value, so two effects sharing a shader would leak values into
each other.

Shaders from code: `ScreenGraphics.Shader.FromSource(glsl, name)` or
`ScreenGraphics.Shader.FromFile(Mod, "Content/Effects/Rift.frag")`.

## Draw steps: `cmd`

`Draw(ScreenCommands cmd)` records steps; they run in order after every effect
has recorded its own.

```csharp
public override void Draw(ScreenCommands cmd)
{
    ScreenGraphics.Target half = cmd.Target("mymod.half", 0.5f);
    cmd.Blit(ScreenGraphics.Sources.Screen, half, ScreenGraphics.Passes.BlurH, new ScreenDrawOptions { Uniforms = { ["uBlurRadius"] = 4f } });
    var options = new ScreenDrawOptions { Margin = 16 };
    options.Textures["uBlurred"] = half;
    cmd.DrawShader(Shader, options);
}
```

- `cmd.DrawShader([shader], options)`: copies the effect's area from the
  screen and redraws it with the shader.
- `cmd.Blit(source, dest, [shader], options)`: one pass into a target.
- `cmd.Target(name, scale, persistent)`: an off-screen texture covering the
  screen at `scale` (0.5 = half resolution). Temporary ones are reused by
  name; persistent ones keep their content between frames.
- `ScreenGraphics.Sources.Screen` and `.History` (the previous frame, with the
  effects); `ScreenGraphics.Passes.Copy`, `Threshold`, `BlurH`, `BlurV`.
- `ScreenDrawOptions`: `Center`, `Radius`, `Margin`, `Fullscreen`, `Mask`,
  `Source`, `Layer`, `Uniforms`, `Textures`.

## Distortion: `d`

`DrawDistortion(DistortionMap d)` adds shapes to one shared distortion pass
(up to 32 per frame, every effect together): `d.Radial(center, radius,
strength)` (out when positive, in when negative), `d.Swirl(center, radius,
angle)`, `d.Ring(center, ringRadius, width, strength)`, `d.Haze(center,
radius, strength, scale, speed)` and `d.Push(center, radius, direction,
strength)`. Their strength follows the effect's `Intensity` and fades.

## Masks, textures and animation (`ScreenUtils`)

- `ScreenUtils.Mask`: `Circle`, `Ring`, `Rect`, `Gradient` (a `Direction` or a
  vector), `Vignette`, `Texture` (alpha or luma), `Inverted()`, with a
  `Falloff`.
- `ScreenGraphics.Texture.Load(Mod, "Content/Effects/Noise.png", nearest,
  repeat)`: a PNG of the mod for a sampler, a mask or a LUT.
- `ScreenGraphics.Texture.SaveNeutralLut(path, 16)` writes the neutral table
  for `ColorGrade` (ReShade layout: red to the right, blue per slice, green
  downwards). Grade it in a photo editor and ship it.
- `ScreenUtils.Easing` (`Linear`, `InOutSine`, `OutBack`, `OutBounce`...),
  `ScreenUtils.Tween.To(target, "Property", to, ticks, easing)`,
  `ScreenUtils.Curve` (points over 0..1), `ScreenUtils.ColorMath` (`FromHex`,
  `Hsv`, `Lerp`, `Temperature`, `Luma`).
- `ScreenUtils.Space`: `WorldToUV`, `UVToWorld`, `PixelsToUV`, `UVToPixels`,
  `Aspect`, `Size`, `IsOnScreen`. On the phone, pinch zoom changes the
  virtual screen, so world sizes follow the zoom.

## For players and for effect makers

**Settings > General > "Screen Effects"**: On/Off, in the main menu and in
the world, saved in `BunnyLoader.screen.json` in the save folder. Off, every
mod's effects keep living (timers, keys, tweens) but do not draw: do not make
gameplay depend on seeing one.

**`/fx` in the chat**: `list`, `spawn <Effect> [radius] [ticks]`, `kill
[key|Effect]`, `reload` (re-reads every `.frag`), `watch` (re-reads them every
half second), `debug` (outlines of every step and a cost panel), `on`, `off`,
`stats`. From code: `ScreenEffects.HotReload`, `ScreenEffects.Reload()`,
`ScreenEffects.Debug`, `ScreenEffects.Stats`.

## Cost

An effect copies and redraws only its own area. On a Mali-G57 phone an area
of 100 thousand pixels costs ~0.8 ms of GPU per frame; with no live effect
nothing is submitted and the renderer is idle. Prefer a radius or a bounded
mask to `Fullscreen`, displacement through `DrawDistortion` (one pass for
all), blur at half resolution, a `Duration` for short effects and a `Key` for
repeating ones.

## In the Example Mod

| File | What it shows |
|---|---|
| [`ExampleLastPrism`](../../samples/ExampleModCSharp/Content/Items/Weapons/ExampleLastPrism.cs), [`ExampleLastPrismHoldout`](../../samples/ExampleModCSharp/Content/Projectiles/ExampleLastPrismHoldout.cs), [`ExampleLastPrismBeam`](../../samples/ExampleModCSharp/Content/Projectiles/ExampleLastPrismBeam.cs) | tModLoader's Last Prism. Where tModLoader queues ripples on the `WaterDistortion` filter, the beam keeps a `Ripple` where it hits; the beam body is one stretched draw instead of `Utils.DrawLaser`. |
| [`ExampleRiftStaff`](../../samples/ExampleModCSharp/Content/Items/Weapons/Magic/ExampleRiftStaff.cs), [`ExampleRiftProjectile`](../../samples/ExampleModCSharp/Content/Projectiles/ExampleRiftProjectile.cs) | A projectile that carries its effect and, on death, closes it and fires a `Shockwave`, a `Flash` and a `ChromaticAberration`. |
| [`ExampleRiftEffect`](../../samples/ExampleModCSharp/Content/Effects/ExampleRiftEffect.cs) + [`.frag`](../../samples/ExampleModCSharp/Content/Effects/ExampleRiftEffect.frag) | A mod effect with its own shader, a uniform animated by a `Tween`, and distortion. `/fx spawn examplerifteffect`. |
| [`ExampleLowLifePlayer`](../../samples/ExampleModCSharp/Common/Players/ExampleLowLifePlayer.cs) | A `ModPlayer` that tints the screen edges below 25% life and fades the tint out when healed. |

Tests: `tools/tests/csscreen` (presets, keys, follow, targets, textures, the
`.frag`, the game's filters and skies) and `tools/tests/csexfx` (the Example
Mod's effects).
