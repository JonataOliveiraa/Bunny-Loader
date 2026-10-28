using System;
using System.Collections.Generic;

namespace BunnyLoader.Interop;

public static unsafe class Layouts
{
    public static List<string> Verify()
    {
        var bad = new List<string>();
        foreach (var (cls, size) in BunnyLoader.Generated.StructLayouts.All)
        {
            int real;
            try { real = Host.Native()->ClassValueSize(cls.Handle); }
            catch (TypeLoadException) { continue; }
            if (real != size) bad.Add($"{cls.FullName}: {size} bytes here, {real} in the game");
        }
        return bad;
    }
}
