using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace BunnyLoader;

internal static class ScreenDebug
{
    const int Max = 32;
    const int Lines = 8;

    const string Source = @"
#include <bl/color>
uniform vec4 uBoxes[32];
uniform vec4 uCircles[32];
uniform float uCount;

void main() {
    vec2 px = vUV * uResolution;
    for (int i = 0; i < 32; i++) {
        if (float(i) >= uCount) break;
        vec4 c = uCircles[i];
        vec3 color = c.w < 0.0 ? vec3(1.0) : blHsvToRgb(vec3(c.w, 0.7, 1.0));
        if (c.z > 0.0) {
            vec2 d = (vUV - c.xy) * vec2(uAspect, 1.0);
            if (abs(length(d) - c.z) * uResolution.y < 1.25) {
                fragColor = vec4(color, 1.0);
                return;
            }
        }
        vec4 b = uBoxes[i] * uResolution.xyxy;
        vec2 lo = px - b.xy, hi = b.zw - px;
        float edge = min(min(lo.x, lo.y), min(hi.x, hi.y));
        if (edge > -1.0 && edge < 1.25 && mod(floor((px.x + px.y) / 6.0), 2.0) < 1.0) {
            fragColor = vec4(color, 1.0);
            return;
        }
    }
    discard;
}
";

    static bool enabled;
    static ScreenGraphics.Shader? shader;
    static readonly List<float> boxes = new();
    static readonly List<float> circles = new();
    static (long At, ScreenStats Stats)? mark;
    static string summary = "";
    static int frames;

    public static bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            mark = null;
            summary = "";
            if (enabled) ItemHooks.Once("ScreenDebug", () => On_Main.DrawInterface_15_InvasionProgressBars += DrawPanel);
        }
    }

    static float Hue(ModScreenEffect fx)
    {
        string text = fx.GetType().Name + ":" + (fx.Key ?? "");
        uint h = 0;
        foreach (char c in text) h = unchecked(h * 31 + c);
        return h % 1000 / 1000f;
    }

    public static void Track(ModScreenEffect fx, List<ScreenDraw> draws, int from)
    {
        float hue = Hue(fx);
        for (int i = from; i < draws.Count; i++)
        {
            ScreenDraw d = draws[i];
            if (d.Dest != 0) continue;
            float radius = fx.Center == null ? 0 : fx.Fullscreen ? ScreenUtils.Space.PixelsToUV(6) : d.Radius;
            Add(d.Box, d.Center, radius, hue);
        }
    }

    public static void Decorate(ScreenFrame frame, ScreenDraw? distortion)
    {
        if (distortion != null) Add(distortion.Box, new float[] { 0, 0 }, 0, -1);
        int count = boxes.Count / 4;
        if (count == 0) return;
        shader ??= ScreenGraphics.Shader.FromSource(Source, "bl/ScreenDebug");
        var draw = new ScreenDraw { Shader = shader.Id, Layer = ScreenEffects.Layer.Screen };
        draw.Uniforms.Add(("uBoxes", 4, boxes.ToArray()));
        draw.Uniforms.Add(("uCircles", 4, circles.ToArray()));
        draw.Uniforms.Add(("uCount", 1, new float[] { count }));
        frame.Draws.Add(draw);
        boxes.Clear();
        circles.Clear();
    }

    static void Add(float[] box, float[] center, float radius, float hue)
    {
        if (boxes.Count / 4 >= Max) return;
        boxes.AddRange(box);
        circles.AddRange(new[] { center[0], center[1], radius, hue });
    }

    static string Text(string key, params object[] args) => Language.GetTextValue("BunnyLoader." + key, args);

    static string Summary()
    {
        if (frames++ % 30 != 0 && summary.Length > 0) return summary;
        var now = (At: Stopwatch.GetTimestamp(), Stats: ScreenStats.Read());
        var last = mark;
        mark = now;
        string text = Text("FxPanelTitle", ScreenEffects.Active.Count);
        if (last is { } m)
        {
            ScreenStats a = m.Stats, b = now.Stats;
            long worldFrames = Math.Max(b.WorldFrames - a.WorldFrames, 0);
            long samples = b.GpuSamples - a.GpuSamples;
            double seconds = Math.Max((now.At - m.At) / (double)Stopwatch.Frequency, 1e-3);
            text += "  ·  " + (worldFrames > 0 ? ((b.Passes - a.Passes) / (double)worldFrames).ToString("0.0") : "0") + " " + Text("FxPanelPasses");
            if (samples > 0) text += $"  ·  GPU {(b.GpuNs - a.GpuNs) / (double)samples / 1e6:0.00} ms";
            text += $"  ·  {worldFrames / seconds:0} fps";
        }
        if (!ScreenEffects.Enabled) text += "  ·  " + Text("FxPanelOff");
        if (ScreenEffects.HotReload) text += "  ·  " + Text("FxPanelHotReload");
        return summary = text;
    }

    static void DrawPanel(On_Main.orig_DrawInterface_15_InvasionProgressBars orig)
    {
        orig();
        if (!enabled || Main.gameMenu) return;
        try
        {
            SpriteBatch batch = Main.spriteBatch;
            float x = MathF.Round(Main.screenWidth / 2f);
            float y = MathF.Round(Main.screenHeight * 0.13f);
            void Draw(string text, float at, Color color) => Utils.DrawBorderString(batch, text, new Vector2(x, at), color, 0.8f, 0.5f, 0, -1);
            Draw(Summary(), y, new Color(255, 255, 255, 255));
            IReadOnlyList<ModScreenEffect> active = ScreenEffects.Active;
            for (int i = 0; i < active.Count && i < Lines; i++)
            {
                y += 20;
                Draw(Describe(active[i]), y, ScreenUtils.ColorMath.Hsv(Hue(active[i]) * 360, 0.45f, 1));
            }
            if (active.Count > Lines) Draw("+" + (active.Count - Lines), y + 20, new Color(255, 255, 255, 255));
        }
        catch (Exception e) { ItemHooks.Report("ScreenDebug", e); }
    }

    internal static string Describe(ModScreenEffect fx)
    {
        string text = fx.GetType().Name;
        if (fx.Key != null) text += $" [{fx.Key}]";
        text += fx.Fullscreen || fx.Center == null ? "  " + Text("FxPanelScreen") : $"  r {MathF.Round(fx.Radius)}";
        text += $"  {fx.Age / 60:0.0} s";
        if (fx.Duration > 0) text += $" / {fx.Duration / 60:0.0} s";
        float strength = fx.Intensity * fx.Opacity;
        if (strength < 0.999f) text += $"  x{strength:0.00}";
        if (fx.Shader?.Status == ScreenGraphics.ShaderStatus.Failed) text += "  " + Text("FxShaderFailed");
        return text;
    }
}
