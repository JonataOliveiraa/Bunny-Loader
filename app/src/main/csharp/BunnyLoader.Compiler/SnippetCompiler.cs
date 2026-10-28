using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace BunnyLoader.Compiler;

public sealed class SnippetResult
{
    public byte[]? Assembly { get; init; }
    public long Milliseconds { get; init; }
    public List<string> Errors { get; } = new();
}

public static class SnippetCompiler
{
    public const string TypeName = "__BunnyConsole";
    public const string MethodName = "Run";

    const int MaxErrors = 10;

    public static SnippetResult Compile(string code, IReadOnlyList<string> references, IReadOnlyList<string> usings,
        string globalsType, int serial)
    {
        var watch = Stopwatch.StartNew();
        var metadata = references.Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();

        EmitResult? asExpression = null, asStatements = null;
        byte[]? dll = TryEmit(Wrap(code, usings, globalsType, expression: true), metadata, serial, ref asExpression);
        if (dll == null) dll = TryEmit(Wrap(code, usings, globalsType, expression: false), metadata, serial, ref asStatements);
        var result = new SnippetResult { Assembly = dll, Milliseconds = watch.ElapsedMilliseconds };
        if (dll == null)
        {
            bool isExpression = !SyntaxFactory.ParseExpression(code).ContainsDiagnostics;
            EmitResult? shown = isExpression ? asExpression : asStatements;
            if (shown != null)
            {
                foreach (Diagnostic d in shown.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(MaxErrors))
                    result.Errors.Add(Format(d));
            }
        }
        return result;
    }

    static string Wrap(string code, IReadOnlyList<string> usings, string globalsType, bool expression)
    {
        var text = new System.Text.StringBuilder();
        foreach (string u in usings) text.Append("using ").Append(u).Append(";\n");
        text.Append("using static ").Append(globalsType).Append(";\n");
        text.Append("public static class ").Append(TypeName).Append("\n{\n");
        text.Append("    public static object? ").Append(MethodName).Append("()\n    {\n");
        if (expression)
        {
            text.Append("        return (object?)(\n#line 1 \"console\"\n").Append(code).Append("\n#line default\n        );\n");
        }
        else
        {
            text.Append("#line 1 \"console\"\n").Append(code).Append("\n;\n#line default\n");
            text.Append("        return ").Append(globalsType).Append(".NoValue;\n");
        }
        text.Append("    }\n}\n");
        return text.ToString();
    }

    static byte[]? TryEmit(string source, List<MetadataReference> metadata, int serial, ref EmitResult? emitted)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            $"BunnyConsole{serial}",
            new[] { tree },
            metadata,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Disable));
        using var dll = new MemoryStream();
        emitted = compilation.Emit(dll);
        return emitted.Success ? dll.ToArray() : null;
    }

    static string Format(Diagnostic d)
    {
        FileLinePositionSpan span = d.Location.GetMappedLineSpan();
        string where = d.Location.IsInSource && span.Path == "console"
            ? $"({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): "
            : "";
        return $"{where}{d.Id}: {d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
