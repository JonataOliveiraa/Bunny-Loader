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
        internal int Index { get; init; }

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

        internal string? Source { get; set; }

        internal string? Error { get; set; }

        public string Name { get; init; } = "";

        public string TechniqueName { get; init; } = "";

        public int Registers { get; internal set; }

        public IReadOnlyDictionary<int, string> Samplers { get; init; } = new Dictionary<int, string>();

        public override string ToString() => $"{TechniqueName}.{Name}";
    }

    static readonly Dictionary<string, FxcEffect> loaded = new(StringComparer.Ordinal);

    int id;
    DateTime written;
    Dictionary<string, Parameter> parameters = new();
    List<Pass> passes = new();
    readonly Dictionary<Pass, ScreenGraphics.Shader> shaders = new();

    public string Name { get; }

    internal int NativeId => id;

    public string? Path { get; }

    public int Version { get; private set; }

    public IReadOnlyCollection<Parameter> Parameters => parameters.Values;

    public IReadOnlyList<Pass> Passes => passes;

    FxcEffect(string name, string? path, byte[] data)
    {
        Name = name;
        Path = path;
        if (path != null) written = File.GetLastWriteTimeUtc(path);
        id = Open(name, data);
        ReadInfo();
    }

    public static FxcEffect FromBytes(byte[] data, string name = "effect")
    {
        ArgumentNullException.ThrowIfNull(data);
        return new FxcEffect(name, null, data);
    }

    public static FxcEffect Load(Mod mod, string path)
    {
        ArgumentNullException.ThrowIfNull(mod);
        string file = path.EndsWith(".fxc", StringComparison.OrdinalIgnoreCase) ? path : path + ".fxc";
        string? full = mod.ResolveFile(file);
        if (full == null || !File.Exists(full)) throw new FileNotFoundException($"FxcEffect.Load: {mod.Name}/{file} not found", file);
        return LoadFile(full, $"{mod.Name}/{file}");
    }

    internal static FxcEffect LoadFile(string full, string name)
    {
        lock (loaded)
        {
            if (loaded.TryGetValue(full, out FxcEffect? cached)) return cached;
            var effect = new FxcEffect(name, full, File.ReadAllBytes(full));
            loaded[full] = effect;
            return effect;
        }
    }

    public static long[] SpriteDrawStats() => FxcGameEffect.Stats();

    public static IReadOnlyList<FxcEffect> Files
    {
        get
        {
            lock (loaded) return loaded.Values.ToArray();
        }
    }

    public static int ReloadAll()
    {
        int changed = 0;
        foreach (FxcEffect effect in Files)
        {
            try
            {
                if (effect.Reload()) changed++;
            }
            catch (Exception e) { Log.Warn($"fxc: {effect.Name} was not reloaded: {e.Message}"); }
        }
        return changed;
    }

    public bool Reload()
    {
        if (Path == null || !File.Exists(Path)) return false;
        DateTime now = File.GetLastWriteTimeUtc(Path);
        if (now == written) return false;
        written = now;
        int fresh = Open(Name, File.ReadAllBytes(Path));
        int old = id;
        id = fresh;
        shaders.Clear();
        ReadInfo(true);
        Host.Native()->FxcFree(old);
        Version++;
        return true;
    }

    static int Open(string name, byte[] data)
    {
        byte* error = stackalloc byte[2048];
        error[0] = 0;
        int result;
        fixed (byte* bytes = data)
            result = Host.Native()->FxcLoad(bytes, data.Length, error, 2048);
        if (result <= 0) throw new InvalidDataException($"FxcEffect: {name} could not be read: {new string((sbyte*)error)}");
        return result;
    }

    void ReadInfo(bool reloaded = false)
    {
        string text = Text((out_, capacity) => Host.Native()->FxcInfo(id, out_, capacity));
        var techniques = new Dictionary<int, string>();
        var samplers = new Dictionary<(int, int), Dictionary<int, string>>();
        var foundParameters = new Dictionary<string, Parameter>();
        var foundPasses = new List<Pass>();
        int index = 0;
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] f = line.Split('\t');
            switch (f[0])
            {
                case "A" when f.Length >= 7:
                    foundParameters[f[1]] = new Parameter
                    {
                        Index = index,
                        Name = f[1],
                        IsSampler = f[2] == "4",
                        Rows = int.Parse(f[4]),
                        Columns = int.Parse(f[5]),
                        Elements = int.Parse(f[6]),
                    };
                    index++;
                    break;
                case "T" when f.Length >= 3:
                    techniques[int.Parse(f[1])] = f[2];
                    break;
                case "P" when f.Length >= 4:
                {
                    int t = int.Parse(f[1]), p = int.Parse(f[2]);
                    var map = new Dictionary<int, string>();
                    samplers[(t, p)] = map;
                    foundPasses.Add(new Pass { Technique = t, Index = p, Name = f[3], TechniqueName = techniques.GetValueOrDefault(t, ""), Samplers = map });
                    break;
                }
                case "S" when f.Length >= 5:
                    if (samplers.TryGetValue((int.Parse(f[1]), int.Parse(f[2])), out var into)) into[int.Parse(f[3])] = f[4];
                    break;
            }
        }
        foreach (Pass pass in foundPasses)
        {
            string source = Text((out_, capacity) => Host.Native()->FxcPassSource(id, pass.Technique, pass.Index, out_, capacity));
            if (source.StartsWith('\0'))
            {
                pass.Error = source[1..];
                Log.Warn($"fxc: {Name}: pass {pass} cannot be used: {pass.Error}");
                continue;
            }
            pass.Source = source;
            pass.Registers = Math.Max(Host.Native()->FxcCommit(id, pass.Technique, pass.Index, null, 0) / 4, 0);
        }
        parameters = foundParameters;
        passes = foundPasses;
        Log.Info($"fxc: {Name}: {passes.Count} pass(es), {parameters.Count} parameter(s){(reloaded ? " (reloaded)" : "")}");
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
        foreach (Pass pass in passes)
            if (pass.Name == name || pass.ToString() == name) return pass;
        throw new KeyNotFoundException($"FxcEffect {Name}: no pass '{name}' (passes: {string.Join(", ", passes)})");
    }

    public bool Has(string parameter) => parameters.ContainsKey(parameter);

    public string Source(string pass) => SourceOf(FindPass(pass));

    string SourceOf(Pass pass) =>
        pass.Source ?? throw new InvalidOperationException($"FxcEffect {Name}: pass {pass} could not be converted: {pass.Error}");

    public ScreenGraphics.Shader Shader(string pass) => ShaderOf(FindPass(pass));

    internal ScreenGraphics.Shader ShaderOf(Pass pass)
    {
        if (shaders.TryGetValue(pass, out var shader)) return shader;
        shader = ScreenGraphics.Shader.FromSource(SourceOf(pass), $"{Name}:{pass}");
        shaders[pass] = shader;
        return shader;
    }

    public FxcEffect SetValue(string name, ReadOnlySpan<float> values)
    {
        if (values.IsEmpty || !parameters.TryGetValue(name, out Parameter? parameter) || parameter.IsSampler) return this;
        fixed (float* v = values)
            Host.Native()->FxcSetParam(id, parameter.Index, v, values.Length);
        return this;
    }

    public FxcEffect SetValue(string name, float[] values) => SetValue(name, (ReadOnlySpan<float>)values);

    public FxcEffect SetValue(string name, float value) => SetValue(name, new ReadOnlySpan<float>(&value, 1));

    public FxcEffect SetValue(string name, bool value) => SetValue(name, value ? 1f : 0f);

    public FxcEffect SetValue(string name, int value) => SetValue(name, (float)value);

    public FxcEffect SetValue(string name, Vector2 value)
    {
        float* v = stackalloc float[] { value.X, value.Y };
        return SetValue(name, new ReadOnlySpan<float>(v, 2));
    }

    public FxcEffect SetValue(string name, Vector3 value)
    {
        float* v = stackalloc float[] { value.X, value.Y, value.Z };
        return SetValue(name, new ReadOnlySpan<float>(v, 3));
    }

    public FxcEffect SetValue(string name, Vector4 value)
    {
        float* v = stackalloc float[] { value.X, value.Y, value.Z, value.W };
        return SetValue(name, new ReadOnlySpan<float>(v, 4));
    }

    public FxcEffect SetValue(string name, Color value) => SetValue(name, new Vector4(value.R / 255f, value.G / 255f, value.B / 255f, value.A / 255f));

    public FxcEffect SetValue(string name, Vector2[] values)
    {
        float[] flat = new float[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            flat[i * 2] = values[i].X;
            flat[i * 2 + 1] = values[i].Y;
        }
        return SetValue(name, (ReadOnlySpan<float>)flat);
    }

    public ScreenGraphics.UniformArray Commit(string pass)
    {
        Pass p = FindPass(pass);
        float[] registers = new float[Math.Max(p.Registers, 1) * 4];
        Commit(p, registers);
        return new ScreenGraphics.UniformArray(4, registers);
    }

    internal void Commit(Pass pass, float[] registers)
    {
        fixed (float* r = registers)
            Host.Native()->FxcCommit(id, pass.Technique, pass.Index, r, registers.Length);
    }

    public static string SamplerUniform(int register) => "ps_s" + register;
}
