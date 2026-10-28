using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace BunnyLoader.Compiler;

public sealed class BuildResult
{
    /** O .dll serve: compilado agora ou reaproveitado. */
    public bool Success { get; init; }
    /** false quando o bin/ ja estava em dia e nada foi compilado. */
    public bool Compiled { get; init; }
    public string DllPath { get; init; } = "";
    public string Reason { get; init; } = "";
    public int Files { get; init; }
    public long Lines { get; init; }
    public long Milliseconds { get; init; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    /**
     * O resultado em texto, uma linha por item (o app do catalogo e o
     * tools/mods/csbuild leem assim):
     *
     *   status compiled|reused|failed
     *   reason ...
     *   stats <ms> <files> <lines>
     *   error ...      (uma por erro)
     *   warning ...    (uma por aviso)
     */
    public string ToReport()
    {
        var text = new System.Text.StringBuilder();
        text.Append("status ").Append(!Success ? "failed" : Compiled ? "compiled" : "reused").Append('\n');
        text.Append("reason ").Append(OneLine(Reason)).Append('\n');
        text.Append("stats ").Append(Milliseconds).Append(' ').Append(Files).Append(' ').Append(Lines).Append('\n');
        foreach (string e in Errors) text.Append("error ").Append(OneLine(e)).Append('\n');
        foreach (string w in Warnings) text.Append("warning ").Append(OneLine(w)).Append('\n');
        return text.ToString();
    }

    internal static string OneLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}

// O Roslyn sobre um projeto de mod (ver ModBuild). As opcoes sao as do
// .csproj da documentacao: C# mais novo, nullable ligado, unsafe permitido,
// Release, deterministico (as mesmas fontes dao o mesmo .dll no celular e no
// PC).
public static class ModCompiler
{
    const int MaxMessages = 40;

    public static string RoslynVersion =>
        typeof(CSharpCompilation).Assembly.GetName().Version?.ToString() ?? "?";

    /**
     * Compila `projectDir` (a pasta do `entryFile`) contra `references`, a nao
     * ser que o bin/ ja esteja em dia; `force` compila de qualquer jeito.
     */
    public static BuildResult Build(string projectDir, string entryFile, IReadOnlyList<string> references, bool force = false,
                                    IReadOnlyList<string>? modReferences = null)
    {
        var watch = Stopwatch.StartNew();
        modReferences ??= Array.Empty<string>();
        string api = ModBuild.ApiVersion(references, modReferences);
        List<string> sources = ModBuild.Sources(projectDir);
        List<string> libraries = ModBuild.Libraries(projectDir);
        string dllPath = ModBuild.DllPath(projectDir, entryFile);
        if (sources.Count == 0)
        {
            var empty = new BuildResult { Success = false, DllPath = dllPath, Reason = "no .cs files" };
            empty.Errors.Add($"no .cs files in {projectDir}");
            return empty;
        }
        string reason = "forced";
        if (!force && ModBuild.IsCurrent(projectDir, entryFile, api, out reason))
        {
            return new BuildResult
            {
                Success = true, Compiled = false, DllPath = dllPath, Reason = reason,
                Files = sources.Count, Milliseconds = watch.ElapsedMilliseconds,
            };
        }

        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        long lines = 0;
        var trees = new List<SyntaxTree>(sources.Count);
        foreach (string relative in sources)
        {
            string text = File.ReadAllText(Path.Combine(projectDir, relative));
            lines += text.AsSpan().Count('\n') + 1;
            // O caminho relativo, e nao o do aparelho: entra no .dll (CallerFilePath)
            // e precisa ser o mesmo em todo lugar.
            trees.Add(CSharpSyntaxTree.ParseText(text, parse, relative));
        }
        var metadata = references.Concat(modReferences).Concat(libraries.Select(l => Path.Combine(projectDir, l)))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var compilation = CSharpCompilation.Create(
            ModBuild.AssemblyName(entryFile),
            trees,
            metadata,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true,
                deterministic: true,
                concurrentBuild: true,
                nullableContextOptions: NullableContextOptions.Enable));

        using var dll = new MemoryStream();
        // Sem PDB. O deterministico usa SHA-256: no Android, a criptografia do
        // .NET precisa da libSystem.Security.Cryptography.Native.Android
        // carregada pelo Java (DotnetRuntime.kt, CompilerRuntime.kt).
        EmitResult emitted = compilation.Emit(dll);

        if (emitted.Success)
        {
            byte[] bytes = dll.ToArray();
            ModBuild.WriteAtomic(dllPath, bytes);
            ModBuild.WriteStamp(projectDir, entryFile, new ModBuild.Stamp(api, $"roslyn {RoslynVersion}",
                ModBuild.Fingerprint(projectDir, sources, libraries, api), ModBuild.Sha256(bytes)));
        }
        var result = new BuildResult
        {
            Success = emitted.Success, Compiled = true, DllPath = dllPath, Reason = reason,
            Files = sources.Count, Lines = lines, Milliseconds = watch.ElapsedMilliseconds,
        };
        foreach (Diagnostic d in emitted.Diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Error && result.Errors.Count < MaxMessages) result.Errors.Add(Format(d));
            else if (d.Severity == DiagnosticSeverity.Warning && result.Warnings.Count < MaxMessages) result.Warnings.Add(Format(d));
        }
        return result;
    }

    /** `Items/Sword.cs(12,5): CS0103: The name 'x' does not exist...` (a lista ja diz se e erro ou aviso). */
    static string Format(Diagnostic d)
    {
        FileLinePositionSpan span = d.Location.GetLineSpan();
        string where = d.Location.IsInSource
            ? $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): "
            : "";
        return $"{where}{d.Id}: {d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
