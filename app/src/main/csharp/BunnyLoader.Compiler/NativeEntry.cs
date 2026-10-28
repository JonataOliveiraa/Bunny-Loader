using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BunnyLoader.Compiler;

public static unsafe class NativeEntry
{
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
