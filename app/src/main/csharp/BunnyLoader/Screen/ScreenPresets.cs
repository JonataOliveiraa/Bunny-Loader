using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace BunnyLoader;

public static class ScreenPresets
{
    public static IReadOnlyList<Type> All { get; } = new[]
    {
        typeof(Twirl), typeof(Shockwave), typeof(Blur), typeof(Bloom), typeof(Echo), typeof(BlackHole),
        typeof(HeatHaze), typeof(Tint), typeof(ColorGrade), typeof(Vignette), typeof(Ripple), typeof(ForceField),
        typeof(ChromaticAberration), typeof(Pixelate), typeof(Flash), typeof(Glitch), typeof(ZoomBlur), typeof(Lens),
    };

    static ScreenDrawOptions With(params (string Name, object Value)[] uniforms)
    {
        var options = new ScreenDrawOptions();
        foreach ((string name, object value) in uniforms) options.Uniforms[name] = value;
        return options;
    }

    static bool RoundArea(ModScreenEffect fx) => !fx.Fullscreen && fx.Center != null && fx.Mask == null;

    public class Twirl : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/math>
uniform float uSpin;
uniform float uSpeed;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    if (r > uRadius) discard;

    float f = 1.0 - smoothstep(0.0, uRadius, r);
    float angle = (uSpin + uAge * uSpeed) * f * f * uIntensity;
    fragColor = blScreen(blAtCenter(blRotate(d, angle)));
}
";

        public float Spin { get; set; } = 3;

        public float Speed { get; set; } = 0.8f;

        public override void SetDefaults() => Radius = 96;

        public override void Draw(ScreenCommands cmd) => cmd.DrawShader(Shader, With(("uSpin", Spin), ("uSpeed", Speed)));
    }

    public class Shockwave : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
uniform float uThickness;
uniform float uStrength;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    float front = uRadius * uProgress;
    float width = max(uRadius * uThickness, 1e-4);
    float x = (r - front) / width;
    if (abs(x) > 1.0 || r < 1e-5) discard;

    float wave = sin(x * 3.14159265) * (1.0 - abs(x));
    float fade = (1.0 - uProgress) * uIntensity;
    vec2 offset = (d / r) * wave * width * uStrength * fade;
    fragColor = blScreen(blAtCenter(d - offset));
}
";

        public float Thickness { get; set; } = 0.18f;

        public float Strength { get; set; } = 0.6f;

        public override void SetDefaults()
        {
            Radius = 240;
            Duration = 40;
        }

        public override void Draw(ScreenCommands cmd)
        {
            ScreenDrawOptions options = With(("uThickness", Thickness), ("uStrength", Strength));
            options.Margin = Radius * Thickness;
            cmd.DrawShader(Shader, options);
        }
    }

    public class Blur : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
uniform sampler2D uBlurred;
uniform float uFullscreen;

void main() {
    vec4 base = blScreen(vUV);
    float mask = uFullscreen > 0.5 ? 1.0 : 1.0 - smoothstep(uRadius * 0.6, uRadius, blDistance(vUV));
    if (mask <= 0.0) discard;
    fragColor = vec4(mix(base.rgb, texture(uBlurred, vUV).rgb, mask * clamp(uIntensity, 0.0, 1.0)), 1.0);
}
";

        public float Strength { get; set; } = 4;

        public override void SetDefaults() => Radius = 160;

        public override void Draw(ScreenCommands cmd)
        {
            float margin = Strength * 4;
            ScreenGraphics.Target a = cmd.Target("bl.blur.a", 0.5f);
            ScreenGraphics.Target b = cmd.Target("bl.blur.b", 0.5f);
            ScreenDrawOptions blur = With(("uBlurRadius", Strength));
            blur.Margin = margin;
            cmd.Blit(ScreenGraphics.Sources.Screen, a, ScreenGraphics.Passes.BlurH, blur);
            cmd.Blit(a, b, ScreenGraphics.Passes.BlurV, blur);
            ScreenDrawOptions final = With(("uFullscreen", Fullscreen));
            final.Textures["uBlurred"] = b;
            cmd.DrawShader(Shader, final);
        }
    }

    public class Bloom : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
uniform sampler2D uBloom;
uniform float uFullscreen;

void main() {
    vec4 base = blScreen(vUV);
    float mask = uFullscreen > 0.5 ? 1.0 : 1.0 - smoothstep(uRadius * 0.6, uRadius, blDistance(vUV));
    if (mask <= 0.0) discard;
    vec3 glow = texture(uBloom, vUV).rgb * uColor.rgb * uIntensity * mask;
    fragColor = vec4(base.rgb + glow, 1.0);
}
";

        public float Threshold { get; set; } = 0.65f;

        public float Spread { get; set; } = 6;

        public override void SetDefaults()
        {
            Fullscreen = true;
            Radius = 200;
        }

        public override void Draw(ScreenCommands cmd)
        {
            float margin = Spread * 4;
            ScreenGraphics.Target a = cmd.Target("bl.bloom.a", 0.5f);
            ScreenGraphics.Target b = cmd.Target("bl.bloom.b", 0.5f);
            ScreenDrawOptions threshold = With(("uThreshold", Threshold), ("uKnee", 0.15f));
            threshold.Margin = margin;
            ScreenDrawOptions blur = With(("uBlurRadius", Spread));
            blur.Margin = margin;
            cmd.Blit(ScreenGraphics.Sources.Screen, a, ScreenGraphics.Passes.Threshold, threshold);
            cmd.Blit(a, b, ScreenGraphics.Passes.BlurH, blur);
            cmd.Blit(b, a, ScreenGraphics.Passes.BlurV, blur);
            ScreenDrawOptions final = With(("uFullscreen", Fullscreen));
            final.Textures["uBloom"] = a;
            cmd.DrawShader(Shader, final);
        }
    }

    public class Echo : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
uniform sampler2D uHistory;
uniform float uPersistence;
uniform vec2 uDrift;
uniform float uFullscreen;

void main() {
    vec4 base = blScreen(vUV);
    float mask = uFullscreen > 0.5 ? 1.0 : 1.0 - smoothstep(uRadius * 0.7, uRadius, blDistance(vUV));
    if (mask <= 0.0) discard;
    vec4 previous = texture(uHistory, vUV + uDrift);
    fragColor = vec4(mix(base.rgb, previous.rgb, clamp(uPersistence * mask * uIntensity, 0.0, 0.97)), 1.0);
}
";

        public float Persistence { get; set; } = 0.75f;

        public Vector2 Drift { get; set; }

        public override void SetDefaults() => Radius = 120;

        public override void Draw(ScreenCommands cmd)
        {
            ScreenDrawOptions options = With(("uPersistence", Persistence), ("uDrift", Drift), ("uFullscreen", Fullscreen));
            options.Textures["uHistory"] = ScreenGraphics.Sources.History;
            cmd.DrawShader(Shader, options);
        }
    }

    public class BlackHole : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/shapes>
uniform float uCore;

void main() {
    float r = blDistance(vUV);
    float core = uRadius * uCore;
    if (r > core * 2.2) discard;

    vec4 base = blScreen(vUV);
    float hole = blCircle(r, core, core * 0.2);
    float ring = blRing(r, core * 1.3, core * 0.45, core * 0.25);
    vec3 col = mix(base.rgb, vec3(0.0), hole * clamp(uIntensity, 0.0, 1.0));
    col += uColor.rgb * ring * 0.9 * uIntensity;
    fragColor = vec4(col, 1.0);
}
";

        public float Pull { get; set; } = 0.35f;

        public float Spin { get; set; } = 2.5f;

        public float Core { get; set; } = 0.18f;

        public override void SetDefaults()
        {
            Radius = 140;
            Color = new Color(1f, 0.6f, 0.25f, 1f);
        }

        public override void DrawDistortion(DistortionMap d)
        {
            if (Center is not Vector2 center) return;
            d.Radial(center, Radius, -Pull * Radius);
            d.Swirl(center, Radius, Spin);
        }

        public override void Draw(ScreenCommands cmd) => cmd.DrawShader(Shader, With(("uCore", Core)));
    }

    public class HeatHaze : ModScreenEffect
    {
        public float Strength { get; set; } = 2.5f;

        public float Scale { get; set; } = 40;

        public float Speed { get; set; } = 1.5f;

        public override void SetDefaults() => Radius = 80;

        public override void DrawDistortion(DistortionMap d)
        {
            if (Center is Vector2 center) d.Haze(center, Radius, Strength, Scale, Speed);
        }

        public override void Draw(ScreenCommands cmd) { }
    }

    public sealed class TintPulse
    {
        public float Period { get; set; }

        public float Amount { get; set; } = 0.3f;
    }

    public class Tint : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/color>
#include <bl/mask>
uniform float uBlend;
uniform float uAmount;
uniform float uPreserve;
uniform float uSaturation;
uniform float uContrast;
uniform float uBrightness;
uniform float uGamma;
uniform float uHue;
uniform float uTemperature;
uniform float uPulse;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uPulse, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 base = blScreen(vUV).rgb;
    vec3 graded = blAdjust(base, uSaturation, uContrast, uBrightness, uGamma, uHue, uTemperature);
    vec3 tinted = blBlend(graded, uColor.rgb, int(uBlend + 0.5));
    if (uPreserve > 0.5) tinted = blSetLuma(tinted, blLuma(graded));
    vec3 result = mix(graded, tinted, clamp(uAmount * uColor.a, 0.0, 1.0));
    fragColor = vec4(mix(base, result, m), 1.0);
}
";

        public ScreenGraphics.BlendMode Blend { get; set; } = ScreenGraphics.BlendMode.Color;

        public float Amount { get; set; } = 0.5f;

        public bool PreserveLuminance { get; set; }

        public float Saturation { get; set; } = 1;

        public float Contrast { get; set; } = 1;

        public float Brightness { get; set; }

        public float Gamma { get; set; } = 1;

        public float HueShift { get; set; }

        public float Temperature { get; set; }

        public TintPulse? Pulse { get; set; }

        public override void SetDefaults()
        {
            Fullscreen = true;
            Radius = 300;
        }

        public override void Draw(ScreenCommands cmd)
        {
            float pulse = Pulse is { Period: > 0 } p ? 1 - p.Amount * (0.5f - 0.5f * MathF.Cos(2 * MathF.PI * Age / p.Period)) : 1;
            ScreenDrawOptions options = With(
                ("uBlend", (float)Blend),
                ("uAmount", Color != null ? Amount : 0f),
                ("uPreserve", PreserveLuminance),
                ("uSaturation", Saturation),
                ("uContrast", Contrast),
                ("uBrightness", Brightness),
                ("uGamma", Gamma),
                ("uHue", HueShift / 360f),
                ("uTemperature", Temperature),
                ("uPulse", pulse));
            if (RoundArea(this)) options.Mask = ScreenUtils.Mask.Circle(Center!.Value, Radius, Radius * 0.5f);
            cmd.DrawShader(Shader, options);
        }
    }

    public class ColorGrade : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/color>
#include <bl/mask>
uniform sampler2D uLut;
uniform float uLutSize;
uniform float uAmount;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uAmount, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 base = blScreen(vUV).rgb;
    fragColor = vec4(mix(base, blLut(uLut, base, uLutSize), m), 1.0);
}
";

        public ScreenGraphics.Texture? Lut { get; set; }

        public string? LutPath { get; set; }

        public float Amount { get; set; } = 1;

        public override void SetDefaults() => Fullscreen = true;

        public override void OnSpawn() => ResolveLut();

        void ResolveLut()
        {
            if (Lut == null && LutPath != null)
            {
                if (Mod == null) throw new InvalidOperationException("ColorGrade: LutPath needs a mod; load the texture with ScreenGraphics.Texture.Load and set Lut");
                Lut = ScreenGraphics.Texture.Load(Mod, LutPath);
            }
            if (Lut == null) throw new InvalidOperationException("ColorGrade: set Lut (ScreenGraphics.Texture) or LutPath");
            if (Lut.Width != Lut.Height * Lut.Height)
                throw new InvalidOperationException($"ColorGrade: the LUT must be a strip of size² x size (got {Lut.Width}x{Lut.Height})");
        }

        public override void Draw(ScreenCommands cmd)
        {
            if (Lut == null) ResolveLut();
            ScreenDrawOptions options = With(("uLutSize", (float)Lut!.Height), ("uAmount", Amount));
            options.Textures["uLut"] = Lut;
            cmd.DrawShader(Shader, options);
        }
    }

    public class Vignette : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/mask>
uniform float uStrength;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uStrength, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 base = blScreen(vUV).rgb;
    fragColor = vec4(mix(base, uColor.rgb, m * uColor.a), 1.0);
}
";

        public float Strength { get; set; } = 0.6f;

        public float Start { get; set; } = 0.45f;

        public float Softness { get; set; } = 0.55f;

        public float Roundness { get; set; } = 1;

        public override void SetDefaults()
        {
            Fullscreen = true;
            Color = new Color(0, 0, 0, 255);
        }

        public override void Draw(ScreenCommands cmd)
        {
            ScreenDrawOptions options = With(("uStrength", Strength));
            options.Mask = ScreenUtils.Mask.Vignette(Start, Softness, Roundness);
            cmd.DrawShader(Shader, options);
        }
    }

    public class Ripple : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/math>
uniform float uCount;
uniform float uWavelength;
uniform float uStrength;
uniform float uSpeed;
uniform float uPulse;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    if (r > uRadius || r < 1e-5) discard;

    float wl = max(uWavelength, 1e-5);
    float wave, env;
    if (uPulse > 0.5) {
        float phase = ((uRadius + uCount * wl) * uProgress - r) / wl;
        if (phase < 0.0 || phase > uCount) discard;
        wave = sin(phase * BL_TAU);
        env = sin(phase / uCount * BL_PI);
    } else {
        wave = sin((r / wl - uAge * uSpeed) * BL_TAU);
        env = 1.0;
    }
    env *= 1.0 - r / uRadius;
    vec2 offset = (d / r) * wave * env * uStrength * uIntensity;
    fragColor = blScreen(blAtCenter(d - offset));
}
";

        public float Count { get; set; } = 3;

        public float Wavelength { get; set; } = 24;

        public float Strength { get; set; } = 4;

        public float Speed { get; set; } = 1.5f;

        public override void SetDefaults()
        {
            Radius = 180;
            Duration = 60;
        }

        public override void Draw(ScreenCommands cmd)
        {
            ScreenDrawOptions options = With(
                ("uCount", Count),
                ("uWavelength", ScreenUtils.Space.PixelsToUV(Wavelength)),
                ("uStrength", ScreenUtils.Space.PixelsToUV(Strength)),
                ("uSpeed", Speed),
                ("uPulse", Duration > 0));
            options.Margin = Strength;
            cmd.DrawShader(Shader, options);
        }
    }

    public class ForceField : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/noise>
uniform float uRefraction;
uniform float uRim;
uniform float uFill;
uniform float uShimmer;
uniform float uHit;
uniform vec2 uHitDir;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    float x = r / max(uRadius, 1e-5);
    if (x > 1.0) discard;

    vec2 dir = r > 1e-6 ? d / r : vec2(0.0);
    float bend = x * x * x * uRefraction * uRadius * clamp(uIntensity, 0.0, 1.0);
    vec3 base = blScreen(blAtCenter(d - dir * bend)).rgb;

    float aa = uScreenTexel.y * 1.5 / max(uRadius, 1e-5);
    float edge = 1.0 - smoothstep(1.0 - aa, 1.0, x);
    float rim = smoothstep(1.0 - uRim, 1.0, x);
    float shimmer = blNoise(d / uRadius * 3.0 + vec2(uTime * 0.6, -uTime * 0.4));
    float glow = rim * mix(1.0, 0.4 + shimmer * 1.2, uShimmer);
    glow += uHit * rim * smoothstep(0.2, 1.0, dot(dir, uHitDir)) * 2.0;

    vec3 inside = base + uColor.rgb * (glow + uFill) * uIntensity * uColor.a;
    fragColor = vec4(mix(blScreen(vUV).rgb, inside, edge), 1.0);
}
";

        float hitAge = float.PositiveInfinity;
        Vector2 hitDir;

        public float Refraction { get; set; } = 0.12f;

        public float Rim { get; set; } = 0.3f;

        public float Fill { get; set; } = 0.06f;

        public float Shimmer { get; set; } = 0.8f;

        public float HitDuration { get; set; } = 18;

        public override void SetDefaults()
        {
            Radius = 64;
            Color = new Color(0.3f, 0.75f, 1f, 1f);
        }

        public void Hit(Vector2 from)
        {
            if (Center is not Vector2 center) return;
            float x = from.X - center.X, y = center.Y - from.Y;
            float length = MathF.Sqrt(x * x + y * y);
            if (length <= 0) length = 1;
            hitDir = new Vector2(x / length, y / length);
            hitAge = 0;
        }

        public override void Update() => hitAge++;

        public override void Draw(ScreenCommands cmd)
        {
            float hit = HitDuration > 0 ? MathF.Max(0, 1 - hitAge / HitDuration) : 0;
            ScreenDrawOptions options = With(
                ("uRefraction", Refraction),
                ("uRim", Rim),
                ("uFill", Fill),
                ("uShimmer", Shimmer),
                ("uHit", hit * hit),
                ("uHitDir", hitDir));
            options.Margin = 2;
            cmd.DrawShader(Shader, options);
        }
    }

    public class ChromaticAberration : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/mask>
uniform vec2 uOffset;
uniform float uRadial;

void main() {
    float m = clamp(blMask(vUV) * uIntensity, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec2 off = uRadial > 0.5 ? blFromCenter(vUV) * uOffset.x : uOffset;
    off = blToUV(off) * m;
    vec4 base = blScreen(vUV);
    fragColor = vec4(blScreen(vUV + off).r, base.g, blScreen(vUV - off).b, 1.0);
}
";

        public float Amount { get; set; } = 4;

        public Vector2? Direction { get; set; }

        public override void SetDefaults()
        {
            Fullscreen = true;
            Radius = 160;
        }

        public override void Draw(ScreenCommands cmd)
        {
            float amount = ScreenUtils.Space.PixelsToUV(Amount);
            Vector2 offset;
            if (Direction is Vector2 direction)
            {
                float length = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                if (length <= 0) length = 1;
                offset = new Vector2(direction.X / length * amount, -direction.Y / length * amount);
            }
            else
            {
                float reach = Fullscreen || Center == null ? CornerDistance() : ScreenUtils.Space.PixelsToUV(Radius);
                offset = new Vector2(amount / MathF.Max(reach, 1e-5f), 0);
            }
            ScreenDrawOptions options = With(("uOffset", offset), ("uRadial", Direction == null));
            options.Margin = Amount;
            if (RoundArea(this)) options.Mask = ScreenUtils.Mask.Circle(Center!.Value, Radius, Radius * 0.5f);
            cmd.DrawShader(Shader, options);
        }

        float CornerDistance()
        {
            Vector2 uv = Center is Vector2 c ? ScreenUtils.Space.WorldToUV(c) : new Vector2(0.5f, 0.5f);
            float x = MathF.Max(uv.X, 1 - uv.X) * ScreenUtils.Space.Aspect, y = MathF.Max(uv.Y, 1 - uv.Y);
            return MathF.Sqrt(x * x + y * y);
        }
    }

    public class Pixelate : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/mask>
uniform float uBlock;
uniform vec2 uAnchor;

void main() {
    float m = blMask(vUV) * uIntensity;
    if (m < 0.5) discard;

    vec2 p = vec2(vUV.x * uAspect, vUV.y) - uAnchor;
    vec2 q = (floor(p / uBlock) + 0.5) * uBlock + uAnchor;
    fragColor = blScreen(vec2(q.x / uAspect, q.y));
}
";

        public float Size { get; set; } = 6;

        public override void SetDefaults() => Radius = 120;

        public override void Draw(ScreenCommands cmd)
        {
            float block = ScreenUtils.Space.PixelsToUV(MathF.Max(Size, 1));
            Vector2 origin = ScreenUtils.Space.WorldToUV(Vector2.Zero);
            float Wrap(float v) => v - MathF.Floor(v / block) * block;
            ScreenDrawOptions options = With(("uBlock", block), ("uAnchor", new Vector2(Wrap(origin.X * ScreenUtils.Space.Aspect), Wrap(origin.Y))));
            options.Margin = Size;
            if (RoundArea(this)) options.Mask = ScreenUtils.Mask.Circle(Center!.Value, Radius, 0);
            cmd.DrawShader(Shader, options);
        }
    }

    public class Flash : Tint
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Blend = ScreenGraphics.BlendMode.Add;
            Amount = 0.8f;
            Color = new Color(255, 255, 255, 255);
            Duration = 12;
            FadeOut = 12;
        }
    }

    public class Glitch : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
#include <bl/noise>
#include <bl/mask>
uniform float uStrength;
uniform float uSlices;
uniform float uRate;
uniform float uSplit;
uniform float uShift;

void main() {
    float m = clamp(blMask(vUV) * uIntensity * uStrength, 0.0, 1.0);
    if (m <= 0.0) discard;

    float t = floor(uTime * uRate);
    float row = floor(vUV.y * uSlices * mix(0.5, 1.5, blHash(vec2(t, 1.7))));
    float pick = blHash(vec2(row, t));
    float shift = pick > 1.0 - 0.4 * m ? (blHash(vec2(row, t + 7.1)) - 0.5) * 2.0 * uShift * m : 0.0;
    vec2 uv = vec2(vUV.x + shift, vUV.y);

    vec2 split = vec2((blHash(vec2(t, 3.3)) - 0.5) * 2.0 * uSplit * m, 0.0);
    fragColor = vec4(blScreen(uv + split).r, blScreen(uv).g, blScreen(uv - split).b, 1.0);
}
";

        public float Strength { get; set; } = 0.6f;

        public float Slices { get; set; } = 24;

        public float Rate { get; set; } = 14;

        public float Split { get; set; } = 4;

        public float Shift { get; set; } = 0.06f;

        public override void SetDefaults() => Fullscreen = true;

        public override void Draw(ScreenCommands cmd) =>
            cmd.DrawShader(Shader, With(
                ("uStrength", Strength),
                ("uSlices", Slices),
                ("uRate", Rate),
                ("uSplit", ScreenUtils.Space.PixelsToUV(Split) / ScreenUtils.Space.Aspect),
                ("uShift", Shift)));
    }

    public class ZoomBlur : ModScreenEffect
    {
        public override string? Source => @"
#include <bl/screen>
uniform float uStrength;
uniform float uClear;
uniform float uSamples;

void main() {
    vec2 d = blFromCenter(vUV);
    float r = length(d);
    float m = smoothstep(uClear, uClear * 2.0 + 0.05, r) * clamp(uIntensity, 0.0, 1.0);
    if (m <= 0.0) discard;

    vec3 sum = vec3(0.0);
    float n = 0.0;
    for (int i = 0; i < 24; i++) {
        if (float(i) >= uSamples) break;
        float k = 1.0 - uStrength * m * float(i) / uSamples;
        sum += blScreen(blAtCenter(d * k)).rgb;
        n += 1.0;
    }
    fragColor = vec4(sum / n, 1.0);
}
";

        public float Strength { get; set; } = 0.1f;

        public float Clear { get; set; } = 60;

        public float Samples { get; set; } = 12;

        public override void SetDefaults() => Fullscreen = true;

        public override void Draw(ScreenCommands cmd) =>
            cmd.DrawShader(Shader, With(
                ("uStrength", Strength),
                ("uClear", ScreenUtils.Space.PixelsToUV(Clear)),
                ("uSamples", (float)Math.Clamp(MathF.Round(Samples), 2, 24))));
    }

    public class Lens : ModScreenEffect
    {
        public float Strength { get; set; } = 16;

        public override void SetDefaults() => Radius = 100;

        public override void DrawDistortion(DistortionMap d)
        {
            if (Center is Vector2 center) d.Radial(center, Radius, Strength);
        }

        public override void Draw(ScreenCommands cmd) { }
    }
}
