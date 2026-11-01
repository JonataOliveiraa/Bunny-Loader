using System.Runtime.InteropServices;

namespace BunnyLoader;

public static unsafe class HookProfile
{
    public readonly record struct Row(string Name, ulong Calls, ulong SelfNs, ulong TotalNs);

    public static bool Enabled => Header(out _, out _);

    public static double BiasNs
    {
        get
        {
            Header(out ulong bias, out _);
            return bias / 1000.0;
        }
    }

    public static double ClockNs
    {
        get
        {
            Header(out _, out ulong clock);
            return clock / 1000.0;
        }
    }

    public static Row[] Snapshot()
    {
        ulong* values = stackalloc ulong[3];
        int count = Host.Native()->HookProfile(-1, values, null);
        if (values[0] == 0) return System.Array.Empty<Row>();
        var rows = new Row[count];
        for (int i = 0; i < count; i++)
        {
            byte* name = null;
            Host.Native()->HookProfile(i, values, &name);
            rows[i] = new Row(Marshal.PtrToStringUTF8((System.IntPtr)name) ?? "?", values[0], values[1], values[2]);
        }
        return rows;
    }

    static bool Header(out ulong bias, out ulong clock)
    {
        ulong* values = stackalloc ulong[3];
        Host.Native()->HookProfile(-1, values, null);
        bias = values[1];
        clock = values[2];
        return values[0] != 0;
    }
}
