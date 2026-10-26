using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using BunnyLoader.Interop;

namespace BunnyLoader;

// Por onde o nativo entra no C#. Os dois metodos sao [UnmanagedCallersOnly]:
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
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Marshal.PtrToStringUTF8((IntPtr)path)!);
            int count = 0;
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(Mod).IsAssignableFrom(type)) continue;
                var mod = (Mod)Activator.CreateInstance(type)!;
                mod.Uid = uid;
                LoadedMods.Add(mod);
                mod.Load();
                count++;
            }
            if (count == 0)
            {
                Log.Error($"mod {uid}: no class extends BunnyLoader.Mod");
                return 0;
            }
            return 1;
        }
        catch (Exception e)
        {
            Log.Error($"mod {uid}: {e}");
            return 0;
        }
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
