using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;

namespace BunnyLoader;

internal static class StartupProfile
{
    [ThreadStatic] static long last;
    [ThreadStatic] static long lastJit;
    [ThreadStatic] static long lastCount;
    [ThreadStatic] static long lastIl;

    internal static readonly Dictionary<string, long[]> Groups = new();

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    internal static int Line([CallerLineNumber] int line = 0) => line;

    internal static void Begin()
    {
        last = Stopwatch.GetTimestamp();
        lastJit = JitInfo.GetCompilationTime(false).Ticks;
        lastCount = JitInfo.GetCompiledMethodCount(false);
        lastIl = JitInfo.GetCompiledILBytes(false);
    }

    internal static void Mark(string label)
    {
        long now = Stopwatch.GetTimestamp();
        long jit = JitInfo.GetCompilationTime(false).Ticks;
        long count = JitInfo.GetCompiledMethodCount(false);
        long il = JitInfo.GetCompiledILBytes(false);
        Log.Info($"prof {label}: {Ms(now - last):F1} ms, jit {(jit - lastJit) / 10000.0:F1} ms, {count - lastCount} methods, {(il - lastIl) / 1024} KB IL");
        last = Stopwatch.GetTimestamp();
        lastJit = jit;
        lastCount = count;
        lastIl = il;
    }

    internal static void Total(string label)
    {
        Log.Info($"prof TOTAL {label}: jit {JitInfo.GetCompilationTime(false).TotalMilliseconds:F1} ms, {JitInfo.GetCompiledMethodCount(false)} methods, {JitInfo.GetCompiledILBytes(false) / 1024} KB IL, mem {GC.GetTotalMemory(false) / 1024 / 1024} MB");
    }

    const string JitProfLib = "/data/data/com.bunnyloader/files/libjitprof.so";
    [System.Runtime.InteropServices.DllImport(JitProfLib)] static extern void jitprof_start();
    [System.Runtime.InteropServices.DllImport(JitProfLib)] static extern void jitprof_dump([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path);
    internal static void JitStart() { try { jitprof_start(); Log.Info("prof jit profiler on"); } catch (Exception e) { Log.Error("prof " + e.Message); } }
    internal static void JitDump() { try { jitprof_dump("/data/data/com.bunnyloader/files/jitprof.txt"); Log.Info("prof jit dump done"); } catch (Exception e) { Log.Error("prof " + e.Message); } }

    internal static long Start() => Stopwatch.GetTimestamp();

    internal static void Add(string group, long start, long jitStart)
    {
        long ms = Stopwatch.GetTimestamp() - start;
        long jit = JitInfo.GetCompilationTime(false).Ticks - jitStart;
        lock (Groups)
        {
            if (!Groups.TryGetValue(group, out long[]? g)) Groups[group] = g = new long[3];
            g[0] += ms;
            g[1] += jit;
            g[2]++;
        }
    }

    internal static void Dump()
    {
        lock (Groups)
        {
            foreach (var pair in Groups.OrderByDescending(p => p.Value[0]).Take(40))
                Log.Info($"prof group {pair.Key}: {Ms(pair.Value[0]):F1} ms, jit {pair.Value[1] / 10000.0:F1} ms, {pair.Value[2]}x");
            Groups.Clear();
        }
    }
}
