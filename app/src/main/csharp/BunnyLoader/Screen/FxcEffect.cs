using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace BunnyLoader;

public sealed unsafe class FxcEffect
{
    public sealed class Parameter
    {
        public string Name { get; init; } = "";

        public int Rows { get; init; }

        public int Columns { get; init; }

        public int Elements { get; init; }

        public bool IsSampler { get; init; }
    }

    public sealed class Pass
    {
        internal int Technique { get; init; }

        internal int Index { get; init; }

        public string Name { get; init; } = "";

        public string TechniqueName { get; init; } = "";

        public IReadOnlyDictionary<int, string> Samplers { get; init; } = new Dictionary<int, string>();

        public override string ToString() => $"{TechniqueName}.{Name}";
    }

    readonly int id;
    readonly Dictionary<string, Parameter> parameters = new();
    readonly List<Pass> passes = new();
    readonly Dictionary<Pass, ScreenGraphics.Shader> shaders = new();
    readonly Dictionary<Pass, string> sources = new();

    public string Name { get; }

    public IReadOnlyCollection<Parameter> Parameters => parameters.Values;

    public IReadOnlyList<Pass> Passes => passes;

    FxcEffect(string name, byte[] data)
    {
        Name = name;
        byte* error = stackalloc byte[2048];
        error[0] = 0;
        fixed (byte* bytes = data)
            id = Host.Native()->FxcLoad(bytes, data.Length, error, 2048);
        if (id <= 0) throw new InvalidDataException($"FxcEffect: {name} could not be read: {new string((sbyte*)error)}");
        ReadInfo();
    }

    public static FxcEffect FromBytes(byte[] data, string name = "effect")
    {
        ArgumentNullException.ThrowIfNull(data);
        return new FxcEffect(name, data);
    }

    public static FxcEffect Load(Mod mod, string path)
    {
        ArgumentNullException.ThrowIfNull(mod);
        string file = path.EndsWith(".fxc", StringComparison.OrdinalIgnoreCase) ? path : path + ".fxc";
        string? full = mod.ResolveFile(file);
        if (full == null || !File.Exists(full)) throw new FileNotFoundException($"FxcEffect.Load: {mod.Name}/{file} not found", file);
        return new FxcEffect($"{mod.Name}/{file}", File.ReadAllBytes(full));
    }

    void ReadInfo()
    {
        string text = Text((out_, capacity) => Host.Native()->FxcInfo(id, out_, capacity));
        var techniques = new Dictionary<int, string>();
        var samplers = new Dictionary<(int, int), Dictionary<int, string>>();
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] f = line.Split('\t');
            switch (f[0])
            {
                case "A" when f.Length >= 7:
                    parameters[f[1]] = new Parameter
                    {
                        Name = f[1],
                        IsSampler = f[2] == "4",
                        Rows = int.Parse(f[4]),
                        Columns = int.Parse(f[5]),
                        Elements = int.Parse(f[6]),
                    };
                    break;
                case "T" when f.Length >= 3:
                    techniques[int.Parse(f[1])] = f[2];
                    break;
                case "P" when f.Length >= 4:
                {
                    int t = int.Parse(f[1]), p = int.Parse(f[2]);
                    var map = new Dictionary<int, string>();
                    samplers[(t, p)] = map;
                    passes.Add(new Pass { Technique = t, Index = p, Name = f[3], TechniqueName = techniques.GetValueOrDefault(t, ""), Samplers = map });
                    break;
                }
                case "S" when f.Length >= 5:
                    if (samplers.TryGetValue((int.Parse(f[1]), int.Parse(f[2])), out var into)) into[int.Parse(f[3])] = f[4];
                    break;
            }
        }
    }

    delegate int TextCall(byte* buffer, int capacity);

    static string Text(TextCall call)
    {
        int need = Math.Abs(call(null, 0));
        byte[] buffer = new byte[Math.Max(need, 1)];
        fixed (byte* b = buffer)
        {
            int got = call(b, buffer.Length);
            string text = Encoding.UTF8.GetString(buffer, 0, Math.Max(Math.Abs(got) - 1, 0));
            return got < 0 ? "\0" + text : text;
        }
    }

    public Pass FindPass(string name)
    {
        Pass? pass = passes.FirstOrDefault(p => p.Name == name) ?? passes.FirstOrDefault(p => p.ToString() == name);
        return pass ?? throw new KeyNotFoundException($"FxcEffect {Name}: no pass '{name}' (passes: {string.Join(", ", passes)})");
    }

    public bool Has(string parameter) => parameters.ContainsKey(parameter);

    public string Source(string pass) => SourceOf(FindPass(pass));

    string SourceOf(Pass pass)
    {
        if (sources.TryGetValue(pass, out string? cached)) return cached;
        string text = Text((out_, capacity) => Host.Native()->FxcPassSource(id, pass.Technique, pass.Index, out_, capacity));
        if (text.StartsWith('\0')) throw new InvalidOperationException($"FxcEffect {Name}: pass {pass} could not be converted: {text[1..]}");
        sources[pass] = text;
        return text;
    }

    public ScreenGraphics.Shader Shader(string pass)
    {
        Pass p = FindPass(pass);
        if (shaders.TryGetValue(p, out var shader)) return shader;
        shader = ScreenGraphics.Shader.FromSource(SourceOf(p), $"{Name}:{p}");
        shaders[p] = shader;
        return shader;
    }

    public FxcEffect SetValue(string name, params float[] values)
    {
        if (values == null || values.Length == 0 || !parameters.ContainsKey(name)) return this;
        byte[] n = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* np = n)
        fixed (float* vp = values)
            Host.Native()->FxcSetParam(id, np, vp, values.Length);
        return this;
    }

    public FxcEffect SetValue(string name, float value) => SetValue(name, new[] { value });

    public FxcEffect SetValue(string name, bool value) => SetValue(name, new[] { value ? 1f : 0f });

    public FxcEffect SetValue(string name, int value) => SetValue(name, new float[] { value });

    public FxcEffect SetValue(string name, Vector2 value) => SetValue(name, new[] { value.X, value.Y });

    public FxcEffect SetValue(string name, Vector3 value) => SetValue(name, new[] { value.X, value.Y, value.Z });

    public FxcEffect SetValue(string name, Vector4 value) => SetValue(name, new[] { value.X, value.Y, value.Z, value.W });

    public FxcEffect SetValue(string name, Color value) => SetValue(name, new[] { value.R / 255f, value.G / 255f, value.B / 255f, value.A / 255f });

    public ScreenGraphics.UniformArray Commit(string pass)
    {
        Pass p = FindPass(pass);
        float[] registers = new float[1024];
        int count;
        fixed (float* r = registers)
            count = Host.Native()->FxcCommit(id, p.Technique, p.Index, r, registers.Length);
        if (count <= 0) return new ScreenGraphics.UniformArray(4, new float[4]);
        return new ScreenGraphics.UniformArray(4, registers.AsSpan(0, Math.Min(count, registers.Length)).ToArray());
    }

    public static string SamplerUniform(int register) => "ps_s" + register;
}
