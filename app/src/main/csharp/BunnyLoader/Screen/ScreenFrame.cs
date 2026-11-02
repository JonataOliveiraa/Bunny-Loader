using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BunnyLoader;

internal sealed class ScreenDraw
{
    public int Shader;
    public ScreenEffects.Layer Layer;
    public int Dest;
    public int Source;
    public float[] Box = { 0, 0, 1, 1 };
    public float[] Center = { 0.5f, 0.5f };
    public float Radius;
    public float Intensity = 1;
    public float Progress;
    public float Age;
    public float[] Color = { 1, 1, 1, 1 };
    public readonly List<(string Name, int Components, float[] Values)> Uniforms = new();
    public readonly List<(string Name, int Texture)> Textures = new();
}

internal sealed unsafe class ScreenFrame
{
    readonly Dictionary<string, ScreenGraphics.Target> targets = new();

    public readonly List<ScreenDraw> Draws = new();

    public bool History;

    public ScreenGraphics.Target Target(string name, float scale, bool persistent)
    {
        if (!(scale > 0 && scale <= 1)) throw new ArgumentOutOfRangeException(nameof(scale), "cmd.Target: scale must be in (0, 1]");
        string key = name + "@" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!targets.TryGetValue(key, out ScreenGraphics.Target? target))
            targets[key] = target = new ScreenGraphics.Target(targets.Count + 1, name, scale, persistent);
        return target;
    }

    public void Submit()
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            w.Write(Draws.Count);
            w.Write(targets.Count);
            w.Write(History ? 1 : 0);
            foreach (ScreenDraw d in Draws)
            {
                w.Write(d.Shader);
                w.Write((int)d.Layer);
                w.Write(d.Dest);
                w.Write(d.Source);
                for (int i = 0; i < 4; i++) w.Write(d.Box[i]);
                w.Write(d.Center[0]);
                w.Write(d.Center[1]);
                w.Write(d.Radius);
                w.Write(d.Intensity);
                w.Write(d.Progress);
                w.Write(d.Age);
                for (int i = 0; i < 4; i++) w.Write(d.Color[i]);
                w.Write(d.Uniforms.Count);
                w.Write(d.Textures.Count);
                foreach ((string name, int components, float[] values) in d.Uniforms)
                {
                    WriteText(w, name);
                    w.Write(components);
                    w.Write(values.Length);
                    foreach (float v in values) w.Write(v);
                }
                foreach ((string name, int texture) in d.Textures)
                {
                    WriteText(w, name);
                    w.Write(texture);
                }
            }
            foreach (ScreenGraphics.Target t in targets.Values)
            {
                w.Write(t.Texture);
                w.Write(t.Scale);
                w.Write(t.Persistent ? 1 : 0);
                WriteText(w, t.Name);
            }
        }
        byte[] data = stream.ToArray();
        fixed (byte* p = data) Host.Native()->ScreenSubmit(p, data.Length);
    }

    public static void SubmitEmpty() => Host.Native()->ScreenSubmit(null, 0);

    static void WriteText(BinaryWriter w, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        w.Write(bytes.Length);
        w.Write(bytes);
    }
}
