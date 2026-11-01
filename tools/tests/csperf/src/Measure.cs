using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BunnyLoader;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;

namespace CsPerf;

public static class Measure
{
    const int Capacity = 4096;

    static ILog log = null!;
    static string tag = "";
    static readonly double[] updates = new double[Capacity], draws = new double[Capacity], gaps = new double[Capacity];
    static readonly long[] frameAllocs = new long[Capacity];
    static readonly bool[] frameGc = new bool[Capacity];
    static int count;
    static bool active;
    static long lastDraw, drawAllocStart;
    static int lastGcCount;
    static long allocStart, threadAllocStart, startTicks;
    static int gen0, gen1, gen2;
    static Dictionary<string, HookProfile.Row> before = new();
    static string windowName = "";

    public static void Install(ILog logger, string name)
    {
        log = logger;
        tag = name;
        On_Main.DoUpdate += DoUpdate;
        On_Main.DoDraw += DoDraw;
    }

    public static void Info(string text) => log.Info($"{tag} {text}");

    static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, GameTime time)
    {
        long t0 = Stopwatch.GetTimestamp();
        orig(self, time);
        if (active && count < Capacity) updates[count] = Ms(Stopwatch.GetTimestamp() - t0);
    }

    static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime time)
    {
        long t0 = Stopwatch.GetTimestamp();
        orig(self, time);
        long t1 = Stopwatch.GetTimestamp();
        if (active && count < Capacity)
        {
            draws[count] = Ms(t1 - t0);
            gaps[count] = lastDraw == 0 ? 0 : Ms(t0 - lastDraw);
            long alloc = GC.GetAllocatedBytesForCurrentThread();
            frameAllocs[count] = alloc - drawAllocStart;
            drawAllocStart = alloc;
            int gcs = GC.CollectionCount(0);
            frameGc[count] = gcs != lastGcCount;
            lastGcCount = gcs;
            count++;
        }
        lastDraw = t0;
    }

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    static Dictionary<string, HookProfile.Row> Profile()
    {
        var map = new Dictionary<string, HookProfile.Row>(StringComparer.Ordinal);
        foreach (HookProfile.Row row in HookProfile.Snapshot())
        {
            if (map.TryGetValue(row.Name, out var old))
                map[row.Name] = new HookProfile.Row(row.Name, old.Calls + row.Calls, old.SelfNs + row.SelfNs, old.TotalNs + row.TotalNs);
            else
                map[row.Name] = row;
        }
        return map;
    }

    public static void Begin(string name)
    {
        windowName = name;
        before = Profile();
        count = 0;
        lastDraw = 0;
        gen0 = GC.CollectionCount(0);
        gen1 = GC.CollectionCount(1);
        gen2 = GC.CollectionCount(2);
        lastGcCount = gen0;
        startTicks = Stopwatch.GetTimestamp();
        threadAllocStart = GC.GetAllocatedBytesForCurrentThread();
        allocStart = GC.GetTotalAllocatedBytes(true);
        drawAllocStart = threadAllocStart;
        active = true;
    }

    public static Dictionary<string, (double PerFrame, double PerCallUs, double MsPerFrame)> End(params string[] focus)
    {
        long alloc = GC.GetTotalAllocatedBytes(true) - allocStart;
        long threadAlloc = GC.GetAllocatedBytesForCurrentThread() - threadAllocStart;
        int g0 = GC.CollectionCount(0) - gen0, g1 = GC.CollectionCount(1) - gen1, g2 = GC.CollectionCount(2) - gen2;
        double seconds = (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;
        active = false;
        int frames = Math.Max(1, count);
        var after = Profile();

        string Stats(double[] values, int skip)
        {
            var list = values.Take(count).Skip(skip).OrderBy(v => v).ToArray();
            if (list.Length == 0) return "-";
            return $"{list.Average():0.00} ms (p90 {list[(int)(list.Length * 0.9)]:0.00}, max {list[^1]:0.00})";
        }

        Info($"[{windowName}] {count} frames in {seconds:0.0} s: frame {Stats(gaps, 1)}, DoUpdate {Stats(updates, 0)}, DoDraw {Stats(draws, 0)}");
        Info($"[{windowName}] managed alloc: {alloc / (double)frames:0} B/frame total, {threadAlloc / (double)frames:0} B/frame on the game thread; GC gen0={g0} gen1={g1} gen2={g2}");
        var gcFrames = Enumerable.Range(1, Math.Max(0, count - 1)).Where(i => frameGc[i]).ToArray();
        if (gcFrames.Length > 0)
        {
            double gcGap = gcFrames.Average(i => gaps[i]);
            double gcDraw = gcFrames.Average(i => draws[i]);
            double gcUpdate = gcFrames.Average(i => updates[i]);
            Info($"[{windowName}] frames with a GC: {gcFrames.Length}, frame {gcGap:0.00} ms, DoUpdate {gcUpdate:0.00} ms, DoDraw {gcDraw:0.00} ms");
        }
        var bigAlloc = frameAllocs.Take(count).Skip(1).DefaultIfEmpty(0).Max();
        Info($"[{windowName}] largest per-frame alloc on the game thread: {bigAlloc} B");

        double bias = HookProfile.BiasNs;
        var rows = new List<(string Name, double PerFrame, double SelfMs, double PerCallUs, double RawUs)>();
        foreach (var (name, row) in after)
        {
            before.TryGetValue(name, out var b);
            double calls = row.Calls - b.Calls;
            if (calls <= 0 || name.StartsWith("(profiler", StringComparison.Ordinal)) continue;
            double self = row.SelfNs - b.SelfNs;
            double correction = name.StartsWith("(callback)", StringComparison.Ordinal) ? bias / 2 : bias;
            double corrected = Math.Max(0, self - correction * calls);
            rows.Add((name, calls / frames, corrected / 1e6 / frames, corrected / calls / 1000, self / calls / 1000));
        }
        double total = rows.Sum(r => r.SelfMs);
        double entries = rows.Sum(r => r.PerFrame);
        Info($"[{windowName}] C# self time: {total:0.000} ms/frame in {rows.Count} hooks, {entries:0} entries/frame (bias {bias:0} ns/entry subtracted, clock {HookProfile.ClockNs:0} ns)");
        foreach (var r in rows.OrderByDescending(r => r.SelfMs).Take(20))
            Info($"[{windowName}]   {r.SelfMs:0.000} ms/frame, {r.PerFrame:0.0}/frame, {r.PerCallUs:0.00} us/call (raw {r.RawUs:0.00}): {r.Name}");
        var result = new Dictionary<string, (double, double, double)>(StringComparer.Ordinal);
        foreach (string f in focus)
        {
            var r = rows.FirstOrDefault(x => x.Name == f);
            result[f] = (r.PerFrame, r.PerCallUs, r.SelfMs);
            if (r.Name == null) Info($"[{windowName}] focus {f}: no calls");
            else Info($"[{windowName}] focus {f}: {r.PerFrame:0.0}/frame, {r.PerCallUs:0.00} us/call (raw {r.RawUs:0.00}), {r.SelfMs:0.000} ms/frame");
        }
        return result;
    }
}
