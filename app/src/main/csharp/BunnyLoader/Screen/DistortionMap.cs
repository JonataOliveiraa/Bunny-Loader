using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Core;

namespace BunnyLoader;

public sealed class DistortionMap
{
    public const int Max = 32;

    internal const string Source = @"
#include <bl/screen>
#include <bl/math>
#include <bl/noise>
uniform vec4 uShapeA[32];
uniform vec4 uShapeB[32];
uniform float uShapeCount;

vec2 shapeOffset(vec4 a, vec4 b, vec2 uv) {
    vec2 d = uv - a.yz;
    d.x *= uAspect;
    float r = length(d);
    if (r >= a.w) return vec2(0.0);

    float x = r / a.w;
    float fall = (1.0 - x * x) * (1.0 - x * x);
    vec2 dir = r > 1e-6 ? d / r : vec2(0.0);
    int type = int(a.x + 0.5);
    vec2 o = vec2(0.0);
    if (type == 1) {
        o = dir * b.x * fall;
    } else if (type == 2) {
        o = d - blRotate(d, -b.x * fall);
    } else if (type == 3) {
        float t = (r - b.y) / max(b.z, 1e-5);
        if (abs(t) < 1.0) o = dir * b.x * sin(t * BL_PI) * (1.0 - abs(t));
    } else if (type == 4) {
        vec2 p = d * b.y + vec2(0.0, -uTime * b.z);
        o = (vec2(blNoise(p), blNoise(p + 17.3)) - 0.5) * 2.0 * b.x * fall;
    } else if (type == 5) {
        o = vec2(b.y, b.z) * b.x * fall;
    }
    o.x /= uAspect;
    return o;
}

void main() {
    vec2 total = vec2(0.0);
    for (int i = 0; i < 32; i++) {
        if (float(i) >= uShapeCount) break;
        total += shapeOffset(uShapeA[i], uShapeB[i], vUV);
    }
    if (dot(total, total) < 1e-12) discard;
    fragColor = blScreen(vUV - total);
}
";

    static ScreenGraphics.Shader? shader;
    static bool warned;

    readonly List<float> a = new();
    readonly List<float> b = new();
    float[]? box;
    ScreenEffects.Layer layer;
    float scale = 1;

    internal DistortionMap() { }

    public int Count => a.Count / 4;

    internal void Begin(ModScreenEffect effect)
    {
        scale = effect.Intensity * effect.Opacity;
        if (Count == 0) layer = effect.Layer;
    }

    public void Radial(Vector2 center, float radius, float strength) =>
        Add(1, center, radius, ScreenUtils.Space.PixelsToUV(strength), 0, 0);

    public void Swirl(Vector2 center, float radius, float angle) => Add(2, center, radius, angle, 0, 0);

    public void Ring(Vector2 center, float ringRadius, float width, float strength) =>
        Add(3, center, ringRadius + width, ScreenUtils.Space.PixelsToUV(strength), ScreenUtils.Space.PixelsToUV(ringRadius), ScreenUtils.Space.PixelsToUV(width));

    public void Haze(Vector2 center, float radius, float strength, float scale = 40, float speed = 1.5f) =>
        Add(4, center, radius, ScreenUtils.Space.PixelsToUV(strength), scale, speed);

    public void Push(Vector2 center, float radius, Vector2 direction, float strength)
    {
        float length = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (length <= 0) length = 1;
        Add(5, center, radius, ScreenUtils.Space.PixelsToUV(strength), direction.X / length, -direction.Y / length);
    }

    void Add(int type, Vector2 center, float radius, float strength, float p1, float p2)
    {
        if (Count >= Max)
        {
            if (!warned) Log.Warn($"DistortionMap: more than {Max} shapes in a frame; the rest is ignored");
            warned = true;
            return;
        }
        Vector2 uv = ScreenUtils.Space.WorldToUV(center);
        float r = ScreenUtils.Space.PixelsToUV(radius);
        a.AddRange(new[] { type, uv.X, uv.Y, r });
        b.AddRange(new[] { strength * scale, p1, p2, 0 });
        float rx = r / ScreenUtils.Space.Aspect;
        float[] shape = { uv.X - rx, uv.Y - r, uv.X + rx, uv.Y + r };
        box = box == null ? shape : new[] { MathF.Min(box[0], shape[0]), MathF.Min(box[1], shape[1]), MathF.Max(box[2], shape[2]), MathF.Max(box[3], shape[3]) };
    }

    internal ScreenDraw? Build()
    {
        if (Count == 0) return null;
        shader ??= ScreenGraphics.Shader.FromSource(Source, "bl/DistortionMap");
        var draw = new ScreenDraw { Shader = shader.Id, Layer = layer, Box = box! };
        draw.Uniforms.Add(("uShapeA", 4, a.ToArray()));
        draw.Uniforms.Add(("uShapeB", 4, b.ToArray()));
        draw.Uniforms.Add(("uShapeCount", 1, new float[] { Count }));
        return draw;
    }
}
