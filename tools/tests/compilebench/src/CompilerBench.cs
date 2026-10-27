using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

// O compilador do C# (Roslyn) medido com mods gerados, do tamanho de um mod
// comum ate o de um mod enorme. Sem nada do jogo: o CompileBench chama isto
// no celular, e o mesmo arquivo roda no PC como referencia.
public static class CompilerBench
{
    // Um "pedaco": uma classe com hooks, metodos com LINQ, lambdas, switch,
    // strings interpoladas e record structs (~140 linhas).
    const int MethodsPerUnit = 7;

    public sealed record Tier(string Name, int Files, int UnitsPerFile, bool Tiny = false);

    public static readonly Tier[] Tiers =
    {
        new("tiny (1 file, a single hook)", 1, 0, Tiny: true),
        new("small (5 files)", 5, 1),
        new("medium (30 files)", 30, 1),
        new("large (150 files)", 150, 1),
        new("huge (400 files)", 400, 1),
        new("one heavy file (200 classes)", 1, 200),
        new("giant (1200 files)", 1200, 1),
    };

    public static void RunAll(Action<string> log, string referenceDir)
    {
        var total = Stopwatch.StartNew();
        log($"compile references from {referenceDir}");
        long t0 = Stopwatch.GetTimestamp();
        List<MetadataReference> refs = Directory.GetFiles(referenceDir, "*.dll")
            .Where(IsManaged)
            .Select(f => (MetadataReference)MetadataReference.CreateFromFile(f))
            .ToList();
        log($"compile references: {refs.Count} assemblies in {Ms(t0)} ms");

        // A primeira compilacao paga o JIT do proprio Roslyn: e o que cada
        // instalacao pagaria num processo novo.
        Run(log, Tiers[0], refs, "cold");
        Run(log, Tiers[0], refs, "warm");
        for (int i = 1; i < Tiers.Length; i++) Run(log, Tiers[i], refs, "warm");
        log($"compile FIM total {total.ElapsedMilliseconds} ms");
    }

    static bool IsManaged(string path)
    {
        try { AssemblyName.GetAssemblyName(path); return true; }
        catch { return false; }
    }

    static long Ms(long since) => (long)Stopwatch.GetElapsedTime(since).TotalMilliseconds;

    static bool stackLogged;

    static void Run(Action<string> log, Tier tier, List<MetadataReference> refs, string label)
    {
        try
        {
            GC.Collect();
            long rssBefore = ReadStatusKb("VmRSS");

            long t0 = Stopwatch.GetTimestamp();
            List<(string Name, string Text)> files = Generate(tier);
            long lines = files.Sum(f => f.Text.AsSpan().Count('\n'));
            long gen = Ms(t0);

            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            long t1 = Stopwatch.GetTimestamp();
            List<SyntaxTree> trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Text, parseOptions, f.Name)).ToList();
            long parse = Ms(t1);

            long t2 = Stopwatch.GetTimestamp();
            var compilation = CSharpCompilation.Create(
                "Generated" + Guid.NewGuid().ToString("N"),
                trees,
                refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    concurrentBuild: true,
                    nullableContextOptions: NullableContextOptions.Enable));
            using var dll = new MemoryStream();
            // Sem PDB, sem checksum: o padrao (SHA256) passa pela criptografia
            // do .NET, que no Android depende de Java e falha aqui
            // ("CS8113: Invalid hash algorithm name").
            var result = compilation.Emit(dll, options: new EmitOptions(pdbChecksumAlgorithm: default(System.Security.Cryptography.HashAlgorithmName)));
            long compile = Ms(t2);
            long rssPeak = ReadStatusKb("VmRSS");
            long heap = GC.GetTotalMemory(false);

            var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            string run = "";
            if (result.Success)
            {
                // Carregar e rodar o .dll gerado prova que ele funciona. Fora
                // do jogo (no PC) o BunnyLoader.dll nao esta carregado: falha
                // aqui, e os tempos de compilacao seguem valendo.
                long t3 = Stopwatch.GetTimestamp();
                try
                {
                    Assembly assembly = Assembly.Load(dll.ToArray());
                    object? value = assembly.GetType("Generated.Entry")?.GetMethod("Run")?.Invoke(null, null);
                    run = $" load+run={Ms(t3)} ms (result {value})";
                }
                catch (Exception e)
                {
                    run = $" load+run=failed ({(e.InnerException ?? e).GetType().Name})";
                }
            }
            log($"compile {label} {tier.Name}: files={files.Count} lines={lines} gen={gen} ms parse={parse} ms " +
                $"compile+emit={compile} ms total={parse + compile} ms dll={dll.Length / 1024} KB errors={errors.Count}" +
                $"{run} heap={heap / 1048576} MB rss={rssBefore / 1024}->{rssPeak / 1024} MB");
            foreach (var e in errors.Take(3)) log($"compile   error: {e}");
        }
        catch (Exception e)
        {
            log($"compile {label} {tier.Name}: FAILED {e.GetType().Name}: {e.Message}");
            if (!stackLogged)
            {
                stackLogged = true;
                foreach (string line in e.ToString().Split('\n').Take(40)) log("compile   " + line.TrimEnd());
            }
        }
    }

    // VmRSS/VmHWM do processo (Linux/Android), em KB; 0 fora do Linux.
    static long ReadStatusKb(string key)
    {
        try
        {
            foreach (string line in File.ReadLines("/proc/self/status"))
            {
                if (!line.StartsWith(key + ":")) continue;
                string digits = new string(line.Where(char.IsDigit).ToArray());
                return long.Parse(digits);
            }
        }
        catch { }
        return 0;
    }

    // ---- os mods gerados ----

    static List<(string, string)> Generate(Tier tier)
    {
        var files = new List<(string, string)>();
        if (tier.Tiny)
        {
            files.Add(("TinyMod.cs", Tiny));
            files.Add(("Entry.cs", EntryFor(0)));
            return files;
        }
        int unit = 0;
        for (int f = 0; f < tier.Files; f++)
        {
            var sb = new StringBuilder(Header);
            for (int u = 0; u < tier.UnitsPerFile; u++) sb.Append(Unit(unit++));
            files.Add(($"Content/Feature{f}.cs", sb.ToString()));
        }
        files.Add(("Entry.cs", EntryFor(unit)));
        return files;
    }

    const string Header = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using BunnyLoader;
        using Microsoft.Xna.Framework;
        using Terraria;
        using Terraria.ID;

        """;

    const string Tiny = """
        using BunnyLoader;
        using Terraria;
        using Terraria.ID;

        namespace Generated
        {
            public class MinisharkTurbo : Mod
            {
                public override void Load()
                {
                    On_Item.SetDefaults += (orig, self, type, variant) =>
                    {
                        orig(self, type, variant);
                        if (type == ItemID.Minishark)
                        {
                            self.useTime = 4;
                            self.useAnimation = 4;
                        }
                    };
                    Log.Info("Minishark Turbo: active");
                }
            }
        }

        """;

    static string EntryFor(int units)
    {
        var sb = new StringBuilder("namespace Generated\n{\n    public static class Entry\n    {\n");
        sb.Append("        public static int Run()\n        {\n            int sum = 0;\n");
        sb.Append("            var points = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2> { new(1, 2), new(5, 3), new(9, 8) };\n");
        for (int i = 0; i < Math.Min(units, 20); i++)
            sb.Append($"            sum += Generated.Part{i}.Feature{i}.Method0({i}, 1.5f, points);\n");
        sb.Append("            return sum;\n        }\n    }\n}\n");
        return sb.ToString();
    }

    static string Unit(int i)
    {
        var sb = new StringBuilder();
        sb.Append($$"""
            namespace Generated.Part{{i}}
            {
                public sealed class Feature{{i}}
                {
                    static readonly Dictionary<int, string> Names = new() { [{{i}}] = "feature{{i}}", [{{i + 1}}] = "next" };
                    static int counter;

                    public static void Install()
                    {
                        On_Item.SetDefaults += (orig, self, type, variant) =>
                        {
                            orig(self, type, variant);
                            if (type == ItemID.Minishark + {{i % 7}})
                            {
                                self.damage += {{i % 13}};
                                self.useTime = Math.Max(2, self.useTime - 1);
                            }
                        };
                        On_Player.Update += (orig, self, index) =>
                        {
                            orig(self, index);
                            if (index == Main.myPlayer && self.statLife < self.statLifeMax2 / {{2 + i % 3}}) counter++;
                        };
                    }

            """);
        for (int j = 0; j < MethodsPerUnit; j++)
        {
            sb.Append($$"""
                        public static int Method{{j}}(int x, float y, List<Vector2> points)
                        {
                            int acc = x + {{j}};
                            foreach (Vector2 p in points.Where(v => v.X > {{j}}).OrderBy(v => v.Y).Take({{3 + j}}))
                                acc += (int)(p.X * y) ^ {{i + j}};
                            switch (acc % {{5 + j}})
                            {
                                case 0: acc += {{j * 3}}; break;
                                case 1: acc -= {{i % 11}}; break;
                                default: acc = acc * 2 - 1; break;
                            }
                            string text = $"feature {{i}}.{{j}}: {acc} {Names.GetValueOrDefault({{i}})}";
                            var data = new Data{{j}}(acc, y * {{j + 1}}, text);
                            return data.A + data.C.Length + (int)data.B;
                        }

                        public readonly record struct Data{{j}}(int A, float B, string C);

                """);
        }
        sb.Append("    }\n}\n\n");
        return sb.ToString();
    }
}
