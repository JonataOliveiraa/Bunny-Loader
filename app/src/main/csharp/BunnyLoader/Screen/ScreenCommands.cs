using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BunnyLoader;

public sealed class ScreenDrawOptions
{
    public Vector2? Center { get; set; }

    public float? Radius { get; set; }

    public float? Margin { get; set; }

    public bool? Fullscreen { get; set; }

    public Dictionary<string, object> Uniforms { get; } = new();

    public Dictionary<string, ScreenGraphics.Target> Textures { get; } = new();

    public ScreenUtils.Mask? Mask { get; set; }

    public ScreenGraphics.Target? Source { get; set; }

    public ScreenEffects.Layer? Layer { get; set; }
}

public sealed class ScreenCommands
{
    readonly ModScreenEffect effect;
    readonly ScreenFrame frame;

    internal ScreenCommands(ModScreenEffect effect, ScreenFrame frame)
    {
        this.effect = effect;
        this.frame = frame;
    }

    public ScreenGraphics.Target Target(string name, float scale = 1, bool persistent = false) =>
        frame.Target(persistent ? effect.GetType().Name + ":" + name : name, scale, persistent);

    public void DrawShader(ScreenDrawOptions? options = null) => DrawShader(effect.Shader, options);

    public void DrawShader(ScreenGraphics.Shader? shader, ScreenDrawOptions? options = null) =>
        Push(shader, null, options ?? new ScreenDrawOptions(), null, "DrawShader");

    public void Blit(ScreenGraphics.Target source, ScreenGraphics.Target dest, ScreenGraphics.Shader? shader = null, ScreenDrawOptions? options = null)
    {
        if (dest == null || dest.Texture <= 0) throw new ArgumentException("cmd.Blit: dest must be a cmd.Target(...)");
        Push(shader ?? ScreenGraphics.Passes.Copy, dest, options ?? new ScreenDrawOptions(), source, "Blit");
    }

    void Push(ScreenGraphics.Shader? shader, ScreenGraphics.Target? dest, ScreenDrawOptions options, ScreenGraphics.Target? source, string what)
    {
        ModScreenEffect fx = effect;
        if (shader == null)
            throw new InvalidOperationException($"cmd.{what}: {fx.GetType().Name} has no shader (put a .frag next to its .cs, override Source or pass a ScreenGraphics.Shader)");

        Vector2? center = options.Center ?? fx.Center;
        float radius = options.Radius ?? fx.Radius;
        float margin = options.Margin ?? fx.CaptureMargin;
        bool fullscreen = options.Fullscreen ?? fx.Fullscreen;
        Vector2 uv = center is Vector2 c ? ScreenUtils.Space.WorldToUV(c) : new Vector2(0.5f, 0.5f);
        float r = ScreenUtils.Space.PixelsToUV(radius);
        ScreenUtils.Mask mask = options.Mask ?? fx.Mask ?? ScreenUtils.Mask.None();
        float[] box = fullscreen || center == null ? new float[] { 0, 0, 1, 1 } : Box(uv, r + ScreenUtils.Space.PixelsToUV(margin));
        if (mask.Bounds() is float[] limit)
            box = new[] { MathF.Max(box[0], limit[0]), MathF.Max(box[1], limit[1]), MathF.Min(box[2], limit[2]), MathF.Min(box[3], limit[3]) };

        var draw = new ScreenDraw
        {
            Shader = shader.Id,
            Layer = options.Layer ?? fx.Layer,
            Dest = dest?.Texture ?? 0,
            Source = TextureOf(options.Source ?? source),
            Box = box,
            Center = new[] { uv.X, uv.Y },
            Radius = r,
            Intensity = fx.Intensity * fx.Opacity,
            Progress = fx.Progress,
            Age = fx.Age / 60f,
            Color = fx.Color is Color color ? ScreenUtils.ColorMath.ToFloats(color) : new float[] { 1, 1, 1, 1 },
        };
        var uniforms = new Dictionary<string, object>();
        foreach (var map in new[] { mask.ToUniforms(), fx.Uniforms, options.Uniforms })
            foreach (var pair in map)
                uniforms[pair.Key] = pair.Value;
        foreach (var pair in uniforms)
            if (ToUniform(pair.Value) is (int components, float[] values))
                draw.Uniforms.Add((pair.Key, components, values));
        var textures = new Dictionary<string, ScreenGraphics.Target>(options.Textures);
        if (mask.TextureSource is ScreenGraphics.Target maskTexture) textures["uMaskTexture"] = maskTexture;
        foreach (var pair in textures)
            draw.Textures.Add((pair.Key, TextureOf(pair.Value)));
        frame.Draws.Add(draw);
    }

    int TextureOf(ScreenGraphics.Target? target)
    {
        if (target == null) return 0;
        if (target == ScreenGraphics.Sources.History) frame.History = true;
        return target.Texture;
    }

    static float[] Box(Vector2 uv, float r)
    {
        float rx = r / ScreenUtils.Space.Aspect;
        return new[] { uv.X - rx, uv.Y - r, uv.X + rx, uv.Y + r };
    }

    public static (int Components, float[] Values)? ToUniform(object? value) => value switch
    {
        null => null,
        ScreenGraphics.UniformArray array => (array.Components, array.Values),
        float f => (1, new[] { f }),
        double d => (1, new[] { (float)d }),
        int i => (1, new float[] { i }),
        bool b => (1, new float[] { b ? 1 : 0 }),
        Vector2 v => (2, new[] { v.X, v.Y }),
        Vector3 v => (3, new[] { v.X, v.Y, v.Z }),
        Vector4 v => (4, new[] { v.X, v.Y, v.Z, v.W }),
        Color c => (4, ScreenUtils.ColorMath.ToFloats(c)),
        string hex => (4, ScreenUtils.ColorMath.ToFloats(ScreenUtils.ColorMath.FromHex(hex))),
        float[] a when a.Length is > 0 and <= 4 => (a.Length, (float[])a.Clone()),
        _ => null,
    };
}
