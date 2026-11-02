using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace BunnyLoader;

public static unsafe class ScreenGraphics
{
    public enum BlendMode
    {
        Normal = 0,
        Multiply = 1,
        Screen = 2,
        Overlay = 3,
        SoftLight = 4,
        Add = 5,
        Color = 6,
        Luminosity = 7,
    }

    public enum ShaderStatus
    {
        Failed = -1,
        Pending = 0,
        Ready = 1,
    }

    public static UniformArray Array(int components, IEnumerable<float> values) => new(components, values.ToArray());

    public sealed class UniformArray
    {
        public int Components { get; }

        public float[] Values { get; }

        public UniformArray(int components, float[] values)
        {
            if (components < 1 || components > 4) throw new ArgumentOutOfRangeException(nameof(components), "ScreenGraphics.Array: components must be 1 to 4");
            Components = components;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }
    }

    public sealed class Shader
    {
        static readonly List<Shader> files = new();

        string source;

        public int Id { get; private set; }

        public string Name { get; }

        public string? Path { get; private set; }

        Shader(string name, string source)
        {
            if (string.IsNullOrEmpty(source) || !source.Contains("main"))
                throw new ArgumentException($"ScreenGraphics.Shader: {name} has no GLSL source with a main()");
            Name = name;
            this.source = source;
            Id = Compile(name, source);
        }

        public ShaderStatus Status => (ShaderStatus)Query(out _);

        public string Error
        {
            get
            {
                Query(out string error);
                return error;
            }
        }

        public static IReadOnlyList<Shader> Files
        {
            get
            {
                lock (files) return files.ToArray();
            }
        }

        public static Shader FromSource(string source, string name = "shader") => new(name, source);

        public static Shader FromFile(Mod mod, string path)
        {
            ArgumentNullException.ThrowIfNull(mod);
            string file = HasExtension(path, ".frag", ".glsl", ".fs") ? path : path + ".frag";
            string? full = mod.ResolveFile(file);
            if (full == null || !File.Exists(full)) throw new FileNotFoundException($"ScreenGraphics.Shader.FromFile: {mod.Name}/{file} not found", file);
            var shader = new Shader($"{mod.Name}/{file}", File.ReadAllText(full)) { Path = full };
            lock (files) files.Add(shader);
            return shader;
        }

        public bool Reload()
        {
            if (Path == null || !File.Exists(Path)) return false;
            string text = File.ReadAllText(Path);
            if (text == source) return false;
            source = text;
            Id = Compile(Name, text);
            return true;
        }

        int Query(out string error)
        {
            byte* buffer = stackalloc byte[2048];
            buffer[0] = 0;
            int state = Host.Native()->ScreenShaderStatus(Id, buffer, 2048);
            error = new string((sbyte*)buffer);
            return state;
        }

        static int Compile(string name, string source)
        {
            byte* n = Host.Utf8(name), s = Host.Utf8(source);
            try { return Host.Native()->ScreenShader(n, s); }
            finally
            {
                Host.FreeUtf8(n);
                Host.FreeUtf8(s);
            }
        }

        public override string ToString() => Name;
    }

    public class Target
    {
        public int Texture { get; }

        public string Name { get; }

        public float Scale { get; }

        public bool Persistent { get; }

        internal Target(int texture, string name, float scale = 1f, bool persistent = false)
        {
            Texture = texture;
            Name = name;
            Scale = scale;
            Persistent = persistent;
        }

        public override string ToString() => Name;
    }

    public sealed class Texture : Target
    {
        public int Width { get; }

        public int Height { get; }

        public string Path { get; }

        Texture(int id, string path, int width, int height) : base(id, path)
        {
            Path = path;
            Width = width;
            Height = height;
        }

        public static Texture Load(Mod mod, string path, bool nearest = false, bool repeat = false)
        {
            ArgumentNullException.ThrowIfNull(mod);
            string file = HasExtension(path, ".png", ".jpg", ".jpeg", ".webp") ? path : path + ".png";
            string? full = mod.ResolveFile(file);
            if (full == null || !File.Exists(full)) throw new FileNotFoundException($"ScreenGraphics.Texture.Load: {mod.Name}/{file} not found", file);
            int width, height;
            byte* p = Host.Utf8(full);
            int id;
            try { id = Host.Native()->ScreenTexture(p, nearest ? 0 : 1, repeat ? 1 : 0, &width, &height); }
            finally { Host.FreeUtf8(p); }
            if (id == 0) throw new InvalidDataException($"ScreenGraphics.Texture.Load: {mod.Name}/{file} could not be loaded");
            return new Texture(id, $"{mod.Name}/{file}", width, height);
        }

        public static void SaveNeutralLut(string path, int size = 16)
        {
            byte* p = Host.Utf8(System.IO.Path.GetFullPath(path));
            try
            {
                if (Host.Native()->ScreenSaveNeutralLut(p, size) == 0)
                    throw new IOException($"ScreenGraphics.Texture.SaveNeutralLut: could not write {path}");
            }
            finally { Host.FreeUtf8(p); }
        }
    }

    public static class Sources
    {
        public static readonly Target Screen = new(-1, "Screen");

        public static readonly Target History = new(-2, "History");
    }

    public static class Passes
    {
        static readonly Dictionary<string, Shader> cache = new();

        static Shader Get(string name, string source)
        {
            lock (cache)
            {
                if (!cache.TryGetValue(name, out Shader? shader))
                    cache[name] = shader = Shader.FromSource(source, "bl/" + name);
                return shader;
            }
        }

        static string Blur(string axis) => $@"
uniform float uBlurRadius;

void main() {{
    vec2 stride = {axis} * max(uBlurRadius, 0.0) / 4.0;
    vec4 sum = blSource(vUV) * 0.2270270270;
    sum += (blSource(vUV + stride * 1.3846153846) + blSource(vUV - stride * 1.3846153846)) * 0.3162162162;
    sum += (blSource(vUV + stride * 3.2307692308) + blSource(vUV - stride * 3.2307692308)) * 0.0702702703;
    fragColor = sum;
}}
";

        public static Shader Copy => Get("Copy", "void main() {\n    fragColor = blSource(vUV);\n}\n");

        public static Shader Threshold => Get("Threshold", @"
#include <bl/color>
uniform float uThreshold;
uniform float uKnee;

void main() {
    vec4 c = blSource(vUV);
    float keep = smoothstep(uThreshold, uThreshold + max(uKnee, 1e-4), blLuma(c.rgb));
    fragColor = vec4(c.rgb * keep, 1.0);
}
");

        public static Shader BlurH => Get("BlurH", Blur("vec2(uSourceTexel.x, 0.0)"));

        public static Shader BlurV => Get("BlurV", Blur("vec2(0.0, uSourceTexel.y)"));
    }

    static bool HasExtension(string path, params string[] extensions) =>
        extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
