using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using BunnyLoader.Compiler;
using BunnyLoader.Interop;

namespace BunnyLoader;

// Por onde o nativo entra no C#. Os metodos sao [UnmanagedCallersOnly]:
// o DotnetHost pega o ponteiro de cada um uma vez e chama direto. Nenhuma
// excecao pode sair daqui (derrubaria o processo do jogo).
public static unsafe class Host
{
    internal static NativeApi* Api;

    static readonly List<Mod> LoadedMods = new();

    public static IReadOnlyList<Mod> Mods => LoadedMods;

    [UnmanagedCallersOnly]
    static int Initialize(NativeApi* api)
    {
        if (api == null || api->Size < (uint)sizeof(NativeApi)) return 0;
        Api = api;
        try
        {
            List<string> bad = Layouts.Verify();
            foreach (string line in bad) Log.Error($"struct layout differs from the game: {line}");
        }
        catch (Exception e)
        {
            Log.Error($"struct layout check failed: {e}");
        }
        return 1;
    }

    [UnmanagedCallersOnly]
    static int LoadMod(byte* id, byte* path)
    {
        string uid = Marshal.PtrToStringUTF8((IntPtr)id) ?? "";
        try
        {
            string file = Marshal.PtrToStringUTF8((IntPtr)path)!;
            string folder = Path.GetDirectoryName(file)!;
            AddModFolder(folder);
            if (ModBuild.IsSourceEntry(file))
            {
                string? built = FromSources(uid, file);
                if (built == null) return 0;
                file = built;
            }
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
            var mods = new List<Mod>();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(Mod).IsAssignableFrom(type)) continue;
                var mod = (Mod)Activator.CreateInstance(type)!;
                mod.Uid = uid;
                mod.Folder = folder;
                mods.Add(mod);
            }
            if (mods.Count == 0)
            {
                Log.Error($"mod {uid}: no class extends BunnyLoader.Mod");
                return 0;
            }
            // Como no tModLoader: o conteudo entra antes do Load. Ele pertence
            // ao primeiro Mod do assembly.
            mods[0].Autoload(assembly);
            foreach (Mod mod in mods)
            {
                LoadedMods.Add(mod);
                mod.Load();
            }
            return 1;
        }
        catch (Exception e)
        {
            Log.Error($"mod {uid}: {e}");
            return 0;
        }
    }

    // O console C# do Editor (script/api/Console.cpp): o codigo vem da thread
    // do jogo e compila fora dela; o ConsoleTick, a cada quadro depois do
    // primeiro codigo, roda o que ja compilou.
    [UnmanagedCallersOnly]
    static void ConsoleSubmit(byte* code)
    {
        try
        {
            DevConsole.Submit(Marshal.PtrToStringUTF8((IntPtr)code) ?? "");
        }
        catch (Exception e)
        {
            Log.Error($"console: {e}");
        }
    }

    [UnmanagedCallersOnly]
    static void ConsoleTick()
    {
        try
        {
            DevConsole.Tick();
        }
        catch (Exception e)
        {
            Log.Error($"console: {e}");
        }
    }

    // Um mod com fontes ("entry": "MeuMod.cs"): o bin/MeuMod.dll do pacote,
    // se o selo dele bate com as fontes e com esta API (o caso do que veio
    // compilado do catalogo); senao, compila agora. null se nao compilou.
    static string? FromSources(string uid, string entry)
    {
        string project = Path.GetDirectoryName(entry)!;
        string name = Path.GetFileName(entry);
        List<string> references = ModBuild.References(AppContext.BaseDirectory);
        string api = ModBuild.ApiVersion(references);
        if (ModBuild.IsCurrent(project, name, api, out string reason)) return ModBuild.DllPath(project, name);
        Log.Info($"mod {uid}: compiling {name} ({reason})");
        return Compile(uid, project, name, references);
    }

    // Separado do FromSources: o Roslyn so carrega quando ha o que compilar.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static string? Compile(string uid, string project, string name, List<string> references)
    {
        BuildResult r = ModCompiler.Build(project, name, references, force: true);
        foreach (string w in r.Warnings) Log.Warn($"mod {uid}: {w}");
        if (!r.Success)
        {
            foreach (string e in r.Errors) Log.Error($"mod {uid}: {e}");
            Log.Error($"mod {uid}: {name} did not compile ({r.Errors.Count} error(s))");
            return null;
        }
        Log.Info($"mod {uid}: compiled {r.Files} file(s), {r.Lines} lines in {r.Milliseconds} ms");
        return r.DllPath;
    }

    // As pastas dos mods C# carregados. Uma biblioteca que um mod traz junto
    // (content/Algo.dll) e achada ali quando o mod a usa: o contexto padrao
    // so conhece as do runtime (assets/dotnet).
    static readonly List<string> ModFolders = new();

    static void AddModFolder(string folder)
    {
        lock (ModFolders)
        {
            if (ModFolders.Count == 0) AssemblyLoadContext.Default.Resolving += ResolveFromMods;
            if (!ModFolders.Contains(folder)) ModFolders.Add(folder);
        }
    }

    static Assembly? ResolveFromMods(AssemblyLoadContext context, AssemblyName name)
    {
        string[] folders;
        lock (ModFolders) folders = ModFolders.ToArray();
        foreach (string folder in folders)
        {
            string candidate = Path.Combine(folder, name.Name + ".dll");
            if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
        }
        return null;
    }

    // Texto UTF-8 terminado em zero para o nativo, numa area que o chamador
    // libera com FreeUtf8.
    internal static byte* Utf8(string text) => (byte*)Marshal.StringToCoTaskMemUTF8(text);

    internal static void FreeUtf8(byte* text) => Marshal.FreeCoTaskMem((IntPtr)text);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NativeApi* Native()
    {
        if (Api == null) throw new InvalidOperationException("BunnyLoader native API is not initialized");
        return Api;
    }
}
