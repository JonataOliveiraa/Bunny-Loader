using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using BunnyLoader.Compiler;

namespace BunnyLoader;

// O console C# do Editor (script/api/Console.cpp). O trecho compila numa
// thread do pool (o Roslyn leva de centenas de ms a alguns segundos no
// celular, e o jogo nao para) e roda na thread do jogo, no ConsoleTick de um
// quadro seguinte, como um hook. A saida vai ao console pelo nativo:
// 'r' o valor, 'x' o erro.
//
// Cada trecho e um assembly pequeno no contexto padrao, que fica carregado
// (o Mono nao descarrega contextos com seguranca); uns KB por execucao.
internal static unsafe class DevConsole
{
    static readonly ConcurrentQueue<SnippetResult> Ready = new();
    static int serial;
    static string[]? usings;

    // Abertos em todo trecho, se existem (Terraria.ModLoader so existe com
    // a API de conteudo; um using de namespace que nao existe e erro).
    static readonly string[] Candidates =
    {
        "System", "System.Collections.Generic", "System.Linq",
        "Microsoft.Xna.Framework", "Terraria", "Terraria.ID", "Terraria.ModLoader", "BunnyLoader",
    };

    public static void Submit(string code)
    {
        int n = Interlocked.Increment(ref serial);
        List<string> references = References();
        string[] open = Usings();
        Task.Run(() =>
        {
            SnippetResult result;
            try
            {
                result = Compile(code, references, open, n);
            }
            catch (Exception e)
            {
                result = new SnippetResult();
                result.Errors.Add(e.Message);
            }
            Ready.Enqueue(result);
        });
    }

    // Separado: o Roslyn so carrega quando o console e usado.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static SnippetResult Compile(string code, List<string> references, string[] open, int n) =>
        SnippetCompiler.Compile(code, references, open, typeof(ConsoleGlobals).FullName!, n);

    // Thread do jogo: roda o que ja compilou.
    public static void Tick()
    {
        while (Ready.TryDequeue(out SnippetResult? result)) Run(result);
    }

    static void Run(SnippetResult result)
    {
        if (result.Assembly == null)
        {
            if (result.Errors.Count == 0) Print('x', "C#: did not compile");
            foreach (string error in result.Errors) Print('x', "C# " + error);
            return;
        }
        try
        {
            using var stream = new MemoryStream(result.Assembly);
            Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            MethodInfo run = assembly.GetType(SnippetCompiler.TypeName)!.GetMethod(SnippetCompiler.MethodName)!;
            object? value = run.Invoke(null, null);
            if (!ReferenceEquals(value, ConsoleGlobals.NoValue)) Print('r', Format(value));
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            Print('x', e.InnerException.ToString());
        }
        catch (Exception e)
        {
            Print('x', e.ToString());
        }
    }

    internal static string Format(object? value) => value switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        bool b => b ? "true" : "false",
        _ => value.ToString() ?? "",
    };

    internal static void Print(char kind, string text)
    {
        byte* utf8 = Host.Utf8(text);
        try
        {
            Host.Native()->ConsolePrint(kind, utf8);
        }
        finally
        {
            Host.FreeUtf8(utf8);
        }
    }

    // A API e o runtime (os mesmos dos mods com fontes) e os mods C#
    // carregados: o trecho pode usar as classes deles.
    static List<string> References()
    {
        List<string> list = ModBuild.References(AppContext.BaseDirectory);
        foreach (Mod mod in Host.Mods)
        {
            string path = mod.GetType().Assembly.Location;
            if (path.Length > 0 && !list.Contains(path)) list.Add(path);
        }
        return list;
    }

    static string[] Usings()
    {
        if (usings != null) return usings;
        var present = new HashSet<string>(typeof(Host).Assembly.GetTypes().Select(t => t.Namespace ?? ""));
        return usings = Candidates.Where(ns => ns.StartsWith("System", StringComparison.Ordinal) || present.Contains(ns)).ToArray();
    }
}
