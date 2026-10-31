using System;
using System.Runtime.CompilerServices;
using BunnyLoader.Interop;

namespace Terraria;

public readonly unsafe partial struct Lang
{
    static MethodRef localizedDuration;

    static readonly int[] LocalizedDurationLayout = { 8, -1, -1, -1 };

    public static string LocalizedDuration(TimeSpan time, bool abbreviated, bool showAllAvailableUnits)
    {
        ref readonly var m = ref GameInterop.Method(ref localizedDuration, Class,
            "string LocalizedDuration(TimeSpan time, bool abbreviated, bool showAllAvailableUnits)", LocalizedDurationLayout);
        CallFrame f;
        Unsafe.SkipInit(out f);
        f.X[0] = time.Ticks;
        f.X[1] = abbreviated ? 1 : 0;
        f.X[2] = showAllAvailableUnits ? 1 : 0;
        f.X[3] = (long)m.Info;
        f.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &f);
        return GameString.Read((nint)f.Out[0]) ?? "";
    }
}
