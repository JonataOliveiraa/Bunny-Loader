using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using BunnyLoader.Compiler;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace BunnyLoader;

// Por onde o nativo entra no C#. Os dois metodos sao [UnmanagedCallersOnly]:
// o DotnetHost pega o ponteiro de cada um uma vez e chama direto. Nenhuma
// excecao pode sair daqui (derrubaria o processo do jogo).
internal static unsafe class Host
{
    internal static NativeApi* Api;

    static readonly List<Mod> LoadedMods = new();

    static readonly List<Mod> Created = new();

    internal static Mod[] CreatedMods
    {
        get { lock (Created) return Created.ToArray(); }
    }

    internal static Mod? FindMod(string name)
    {
        lock (Created) return Created.Find(m => m.Name == name);
    }

    internal static Mod[] Mods
    {
        get { lock (LoadedMods) return LoadedMods.ToArray(); }
    }

    [UnmanagedCallersOnly]
    static int Initialize(NativeApi* api)
    {
        if (api == null || api->Size < (uint)sizeof(NativeApi)) return 0;
        Api = api;
        api->OnContentReady(&ModContent.ContentReady);
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
        var mods = new List<Mod>();
        try
        {
            string file = Marshal.PtrToStringUTF8((IntPtr)path)!;
            string folder = Path.GetDirectoryName(file)!;
            AddModFolder(folder);
            string[] dependencies = ManifestField(uid, 3).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (ModBuild.IsSourceEntry(file))
            {
                string? built = FromSources(uid, file, dependencies);
                if (built == null) return 0;
                file = built;
            }
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(Mod).IsAssignableFrom(type)) continue;
                var mod = (Mod)Activator.CreateInstance(type)!;
                mod.Uid = uid;
                mod.Folder = folder;
                mod.Logger = new ModLogger(mod.Name);
                mod.ManifestId = ManifestField(uid, 0);
                mod.DisplayName = ManifestField(uid, 1) is { Length: > 0 } display ? display : mod.Name;
                mod.Version = Version.TryParse(ManifestField(uid, 2), out Version? version) ? version : new Version(0, 0);
                mod.Dependencies = dependencies;
                mods.Add(mod);
                ModContent.Add(mod);
                lock (Created) Created.Add(mod);
            }
            if (mods.Count == 0)
            {
                Log.Error($"mod {uid}: no class extends Terraria.ModLoader.Mod");
                return 0;
            }
            foreach (Mod mod in mods) LocalizationLoader.Load(mod);
            foreach (Mod mod in mods) mod.loading = true;
            try
            {
                mods[0].Autoload(assembly);
                foreach (Mod mod in mods) mod.Load();
            }
            finally
            {
                foreach (Mod mod in mods) mod.loading = false;
            }
            lock (LoadedMods) LoadedMods.AddRange(mods);
            return 1;
        }
        catch (Exception e)
        {
            Log.Error($"mod {uid}: {e}");
            lock (Created)
            {
                foreach (Mod mod in mods) Created.Remove(mod);
            }
            return 0;
        }
    }

    // Um mod com fontes ("entry": "MeuMod.cs"): o bin/MeuMod.dll do pacote,
    // se o selo dele bate com as fontes e com esta API (o caso do que veio
    // compilado do catalogo); senao, compila agora. null se nao compilou.
    static string? FromSources(string uid, string entry, string[] dependencies)
    {
        string project = Path.GetDirectoryName(entry)!;
        string name = Path.GetFileName(entry);
        List<string> references = ModBuild.References(AppContext.BaseDirectory);
        List<string> modReferences = DependencyAssemblies(dependencies);
        string api = ModBuild.ApiVersion(references, modReferences);
        if (ModBuild.IsCurrent(project, name, api, out string reason)) return ModBuild.DllPath(project, name);
        Log.Info($"mod {uid}: compiling {name} ({reason})");
        return Compile(uid, project, name, references, modReferences);
    }

    static List<string> DependencyAssemblies(string[] dependencies)
    {
        var found = new List<string>();
        foreach (Mod mod in CreatedMods)
        {
            if (Array.IndexOf(dependencies, mod.Uid) < 0 && Array.IndexOf(dependencies, mod.ManifestId) < 0) continue;
            string location = mod.GetType().Assembly.Location;
            if (location.Length > 0 && !found.Contains(location)) found.Add(location);
        }
        return found;
    }

    static string ManifestField(string uid, int field)
    {
        byte* id = Utf8(uid);
        try
        {
            int size = 256;
            while (true)
            {
                byte[] managed = new byte[size];
                fixed (byte* p = managed)
                {
                    int length = Native()->ModManifestField(id, field, p, size);
                    if (length < 0) return "";
                    if (length < size) return Marshal.PtrToStringUTF8((IntPtr)p, length);
                    size = length + 1;
                }
            }
        }
        finally { FreeUtf8(id); }
    }

    // Separado do FromSources: o Roslyn so carrega quando ha o que compilar.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static string? Compile(string uid, string project, string name, List<string> references, List<string> modReferences)
    {
        BuildResult r = ModCompiler.Build(project, name, references, force: true, modReferences: modReferences);
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
