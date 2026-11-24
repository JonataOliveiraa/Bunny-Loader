using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader;
using System.Diagnostics;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsEntry;

public sealed class CsEntryMod : Mod
{
    const int Rounds = 2, Max = 60000;
    const double WorldMs = 10000;

    static readonly long[] stamps = new long[Max];
    static readonly bool[] inWorld = new bool[Max];
    static readonly float[] updateMs = new float[Max], drawMs = new float[Max];
    static readonly short[] modes = new short[Max];
    static float lastDraw;
    static int count, round, gc0;
    static long playAt, callbackStart, callbackEnd, loadStart, loadEnd, firstWorld, alloc0, mainAlloc0, mainAllocWorld, callbackAlloc, gcAtWorld;
    static bool measuring, finished;
    static FieldInfo? autoCleaned;
    [ThreadStatic] static string? bracket;
    static readonly Dictionary<string, (int Calls, double Ms)> defaultsBy = new();
    static int mainThread;
    static Dictionary<string, HookProfile.Row> profileBefore = new();
    static Dictionary<string, HookProfile.Row>? atWorld, afterUpdate, afterDraw;

    static Dictionary<string, HookProfile.Row> Profile()
    {
        var map = new Dictionary<string, HookProfile.Row>(StringComparer.Ordinal);
        foreach (HookProfile.Row row in HookProfile.Snapshot())
            map[row.Name] = map.TryGetValue(row.Name, out var old) ? new HookProfile.Row(row.Name, old.Calls + row.Calls, old.SelfNs + row.SelfNs, old.TotalNs + row.TotalNs) : row;
        return map;
    }

    static ILog Log => ModContent.GetInstance<CsEntryMod>().Logger;

    public override void Load()
    {
        On_WorldGen.playWorld += PlayWorld;
        On_WorldGen.playWorldCallBack += CallBack;
        On_WorldFile.LoadWorld += LoadWorld;
        On_Main.DoUpdate += DoUpdate;
        On_Main.DoDraw += DoDraw;
        On_Item.SetDefaults += SetDefaults;
        On_Player.Spawn += (orig, self, context) => Within("Player.Spawn", () => orig(self, context));
        On_LocalUser.LoadThreadstate += (orig, self) => Within("LocalUser.LoadThreadstate", () => orig(self));
        Terraria.GameContent.Creative.On_CreativePowerManager.SyncSplitScreenPlayer += (orig, index) => Within("SyncSplitScreenPlayer", () => orig(index));
        mainThread = Environment.CurrentManagedThreadId;
    }

    static void Within(string name, Action body)
    {
        string? outer = bracket;
        bracket = name;
        try { body(); }
        finally { bracket = outer; }
    }

    static void SetDefaults(On_Item.orig_SetDefaults orig, Item self, int type, Terraria.GameContent.Items.ItemVariant variant)
    {
        if (!measuring)
        {
            orig(self, type, variant);
            return;
        }
        long start = Stopwatch.GetTimestamp();
        orig(self, type, variant);
        string key = (bracket ?? "-") + (Environment.CurrentManagedThreadId == mainThread ? "@main" : "@other") + (firstWorld == 0 ? " loading" : " world");
        lock (defaultsBy)
        {
            defaultsBy.TryGetValue(key, out var old);
            defaultsBy[key] = (old.Calls + 1, old.Ms + Ms(start, Stopwatch.GetTimestamp()));
        }
    }

    static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        long start = Stopwatch.GetTimestamp();
        orig(self, gameTime);
        lastDraw = (float)Ms(start, Stopwatch.GetTimestamp());
        if (measuring && afterUpdate != null && afterDraw == null) afterDraw = Profile();
    }

    static void Top(string label, Dictionary<string, HookProfile.Row>? from, Dictionary<string, HookProfile.Row>? to, int take)
    {
        if (from == null || to == null) return;
        var rows = new List<(string Name, ulong Calls, double Self, double Total)>();
        foreach (var row in to.Values)
        {
            from.TryGetValue(row.Name, out var old);
            ulong calls = row.Calls - old.Calls;
            if (calls == 0) continue;
            rows.Add((row.Name, calls, (row.SelfNs - old.SelfNs) / 1e6, (row.TotalNs - old.TotalNs) / 1e6));
        }
        Log.Info($"csentry round={round + 1} {label}: {rows.Count} hooks entered, self {rows.Sum(r => r.Self):F0}ms");
        foreach (var r in rows.OrderByDescending(r => r.Self).Take(take))
            Log.Info($"csentry round={round + 1} {label} {r.Name} calls={r.Calls} self={r.Self:F1}ms total={r.Total:F1}ms");
    }

    static double Ms(long from, long to) => from == 0 || to == 0 ? -1 : (to - from) * 1000.0 / Stopwatch.Frequency;

    static void Begin(string by)
    {
        measuring = true;
        if (autoCleaned == null)
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetType("CsAuto.CsAutoMod") is Type type)
                    autoCleaned = type.GetField("cleaned", BindingFlags.NonPublic | BindingFlags.Static);
        Log.Info($"csentry tombstone scan of csauto {(autoCleaned != null ? "skipped" : "not found")}");
        count = 0;
        playAt = Stopwatch.GetTimestamp();
        callbackStart = callbackEnd = loadStart = loadEnd = firstWorld = 0;
        gc0 = GC.CollectionCount(0);
        if (HookProfile.Enabled) profileBefore = Profile();
        atWorld = afterUpdate = afterDraw = null;
        lock (defaultsBy) defaultsBy.Clear();
        alloc0 = GC.GetTotalAllocatedBytes(true);
        mainAlloc0 = GC.GetAllocatedBytesForCurrentThread();
        mainAllocWorld = callbackAlloc = gcAtWorld = 0;
        Log.Info($"csentry round={round + 1} begin by={by}");
    }

    static void PlayWorld(On_WorldGen.orig_playWorld orig)
    {
        if (!measuring && round < Rounds) Begin("playWorld");
        orig();
    }

    static void CallBack(On_WorldGen.orig_playWorldCallBack orig, BunnyLoader.Interop.GameObject context)
    {
        callbackStart = Stopwatch.GetTimestamp();
        long before = GC.GetAllocatedBytesForCurrentThread();
        try { Within("playWorldCallBack", () => orig(context)); }
        finally
        {
            callbackEnd = Stopwatch.GetTimestamp();
            callbackAlloc = GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }

    static void LoadWorld(On_WorldFile.orig_LoadWorld orig, bool canCheckFileState)
    {
        if (loadStart == 0) loadStart = Stopwatch.GetTimestamp();
        try { Within("LoadWorld", () => orig(canCheckFileState)); }
        finally { loadEnd = Stopwatch.GetTimestamp(); }
    }

    static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, GameTime gameTime)
    {
        if (measuring && !Main.gameMenu && firstWorld == 0 && HookProfile.Enabled) atWorld = Profile();
        long start = Stopwatch.GetTimestamp();
        orig(self, gameTime);
        if (finished || !measuring) return;
        if (atWorld != null && afterUpdate == null) afterUpdate = Profile();
        long now = Stopwatch.GetTimestamp();
        bool world = !Main.gameMenu;
        if (world) autoCleaned?.SetValue(null, true);
        if (count < Max)
        {
            stamps[count] = now;
            inWorld[count] = world;
            updateMs[count] = (float)Ms(start, now);
            drawMs[count] = lastDraw;
            modes[count] = (short)(world ? -1 : Main.menuMode);
            lastDraw = 0;
            count++;
        }
        if (!world) return;
        if (firstWorld == 0)
        {
            firstWorld = now;
            mainAllocWorld = GC.GetAllocatedBytesForCurrentThread();
            gcAtWorld = GC.CollectionCount(0) - gc0;
        }
        if (Ms(firstWorld, now) < WorldMs) return;
        try { Report(); }
        catch (Exception e) { Log.Info($"csentry threw {e}"); }
        measuring = false;
        round++;
        if (round < Rounds) WorldGen.SaveAndQuit();
        else
        {
            finished = true;
            Log.Info("csentry END");
        }
    }

    static string Frames(bool world, double fromMs, double toMs)
    {
        int n = 0, over33 = 0, over50 = 0, over100 = 0;
        double sum = 0, worst = 0;
        long origin = world ? firstWorld : playAt;
        for (int i = 1; i < count; i++)
        {
            if (inWorld[i] != world || inWorld[i - 1] != world) continue;
            double at = Ms(origin, stamps[i]);
            if (at < fromMs || at >= toMs) continue;
            double d = Ms(stamps[i - 1], stamps[i]);
            n++;
            sum += d;
            if (d > worst) worst = d;
            if (d > 33.4) over33++;
            if (d > 50) over50++;
            if (d > 100) over100++;
        }
        if (n == 0) return "frames=0";
        double fps = sum > 0 ? n * 1000.0 / sum : 0;
        return $"frames={n} fps={fps:F1} avg={sum / n:F1}ms worst={worst:F0}ms >33={over33} >50={over50} >100={over100}";
    }

    static void Report()
    {
        long gc = GC.CollectionCount(0) - gc0;
        double mb = (GC.GetTotalAllocatedBytes(true) - alloc0) / 1048576.0;
        double total = Ms(playAt, firstWorld);
        Log.Info($"csentry round={round + 1} toWorld={total:F0}ms callback={Ms(callbackStart, callbackEnd):F0}ms loadWorld={Ms(loadStart, loadEnd):F0}ms beforeCallback={Ms(playAt, callbackStart):F0}ms afterCallback={Ms(callbackEnd, firstWorld):F0}ms gc0={gc} alloc={mb:F1}MB");
        double main = GC.GetAllocatedBytesForCurrentThread();
        Log.Info($"csentry round={round + 1} alloc: game thread loading={(mainAllocWorld - mainAlloc0) / 1048576.0:F1}MB world10s={(main - mainAllocWorld) / 1048576.0:F1}MB callback thread={callbackAlloc / 1048576.0:F1}MB gc0 before world={gcAtWorld}");
        Log.Info($"csentry round={round + 1} loading {Frames(false, 0, double.MaxValue)}");
        Log.Info($"csentry round={round + 1} world0-2s {Frames(true, 0, 2000)}");
        Log.Info($"csentry round={round + 1} world2-10s {Frames(true, 2000, WorldMs)}");
        var order = new int[Math.Max(count - 1, 0)];
        for (int i = 0; i < order.Length; i++) order[i] = i + 1;
        Array.Sort(order, (a, b) => (stamps[b] - stamps[b - 1]).CompareTo(stamps[a] - stamps[a - 1]));
        for (int k = 0; k < Math.Min(6, order.Length); k++)
        {
            int i = order[k];
            Log.Info($"csentry round={round + 1} slow#{k + 1} gap={Ms(stamps[i - 1], stamps[i]):F0}ms at={Ms(playAt, stamps[i]):F0}ms update={updateMs[i]:F0}ms draw={drawMs[i]:F0}ms mode={modes[i]}");
        }
        lock (defaultsBy)
            foreach (var pair in defaultsBy.OrderByDescending(p => p.Value.Calls))
                Log.Info($"csentry round={round + 1} SetDefaults in {pair.Key}: calls={pair.Value.Calls} ms={pair.Value.Ms:F1}");
        if (HookProfile.Enabled)
        {
            Top("loading", profileBefore, atWorld, 12);
            Top("firstUpdate", atWorld, afterUpdate, 12);
            Top("firstDraw", afterUpdate, afterDraw, 12);
            Top("all", profileBefore, Profile(), 10);
        }
        Log.Info($"csentry round={round + 1} worstFrame done worldAt={total:F0}ms");
    }
}
