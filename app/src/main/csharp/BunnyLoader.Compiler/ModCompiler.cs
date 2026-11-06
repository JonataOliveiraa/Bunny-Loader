using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;

namespace BunnyLoader.Compiler;

public sealed class BuildResult
{
    public bool Success { get; init; }
    public bool Compiled { get; init; }
    public string DllPath { get; init; } = "";
    public string Reason { get; init; } = "";
    public int Files { get; init; }
    public long Lines { get; init; }
    public long Milliseconds { get; init; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

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

public static class ModCompiler
{
    const int MaxMessages = 40;

    public static string RoslynVersion =>
        typeof(CSharpCompilation).Assembly.GetName().Version?.ToString() ?? "?";

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
                concurrentBuild: false,
                nullableContextOptions: NullableContextOptions.Enable));

        using var dll = new MemoryStream();
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
        foreach (string warning in NetModeWarnings(trees))
        {
            if (result.Warnings.Count >= MaxMessages) break;
            result.Warnings.Add(warning);
        }
        return result;
    }

    public static IEnumerable<string> NetModeWarnings(IEnumerable<SyntaxTree> trees)
    {
        foreach (SyntaxTree tree in trees)
        {
            foreach (BinaryExpressionSyntax test in tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                if (!test.IsKind(SyntaxKind.EqualsExpression) && !test.IsKind(SyntaxKind.NotEqualsExpression)) continue;
                if (!(IsNetMode(test.Left) && IsServer(test.Right)) && !(IsNetMode(test.Right) && IsServer(test.Left))) continue;
                FileLinePositionSpan span = test.GetLocation().GetLineSpan();
                string use = test.IsKind(SyntaxKind.EqualsExpression) ? "(Main.netMode & NetmodeID.Server) != 0" : "(Main.netMode & NetmodeID.Server) == 0";
                yield return $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): BL0001: '{test}' does not treat a phone host as the server, because its netMode is 3. To mean \"I am the server\", use {use}. To mean \"I do not render\", the test is fine.";
            }
        }
    }

    static bool IsNetMode(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "netMode" } or IdentifierNameSyntax { Identifier.ValueText: "netMode" };

    static bool IsServer(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Server", Expression: var owner } && owner.ToString().EndsWith("NetmodeID", StringComparison.Ordinal)
        || expression is LiteralExpressionSyntax { Token.ValueText: "2" };

    static string Format(Diagnostic d)
    {
        FileLinePositionSpan span = d.Location.GetLineSpan();
        string where = d.Location.IsInSource
            ? $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): "
            : "";
        return $"{where}{d.Id}: {d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
