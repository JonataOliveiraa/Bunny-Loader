using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BunnyLoader.Compiler;

// A porta do app do catalogo (catalog/src/main/cpp/CompilerHost.cpp): texto
// UTF-8 na ida e na volta, nada de excecao atravessando para o nativo.
public static unsafe class NativeEntry
{
    /**
     * Compila o projeto de `entry` (caminho do .cs de entrada) contra as
     * referencias do runtime em `runtimeDir`. A resposta e o
     * BuildResult.ToReport(); o chamador a libera com free().
     */
    [UnmanagedCallersOnly]
    static byte* Build(byte* entry, byte* runtimeDir, int force)
    {
        var text = new StringBuilder();
        try
        {
            string entryPath = Marshal.PtrToStringUTF8((IntPtr)entry)!;
            string runtime = Marshal.PtrToStringUTF8((IntPtr)runtimeDir)!;
            BuildResult r = ModCompiler.Build(Path.GetDirectoryName(entryPath)!, Path.GetFileName(entryPath),
                ModBuild.References(runtime), force != 0);
            text.Append(r.ToReport());
        }
        catch (Exception e)
        {
            text.Clear();
            text.Append("status failed\nerror ").Append(BuildResult.OneLine(e.GetType().Name + ": " + e.Message)).Append('\n');
        }
        return (byte*)Marshal.StringToCoTaskMemUTF8(text.ToString());
    }
}
