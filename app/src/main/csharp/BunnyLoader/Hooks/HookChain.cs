using System;
using System.Collections.Generic;
using System.Threading;

namespace BunnyLoader.Hooks;

public sealed class HookChain<THook, TOrig> where THook : System.Delegate where TOrig : System.Delegate
{
    readonly object gate = new();
    readonly List<THook> hooks = new();
    readonly Func<THook, TOrig, TOrig> wrap;
    readonly TOrig native;
    readonly Action install;
    bool installed;
    TOrig? chain;

    public HookChain(Func<THook, TOrig, TOrig> wrap, TOrig native, Action install)
    {
        this.wrap = wrap;
        this.native = native;
        this.install = install;
    }

    public TOrig? Chain => HookGate.Open ? Volatile.Read(ref chain) : null;

    public int Count => hooks.Count;

    public void Add(THook hook)
    {
        lock (gate)
        {
            if (!installed)
            {
                Type? outer = HookGates.Installing;
                HookGates.Installing = typeof(THook);
                try { install(); }
                finally { HookGates.Installing = outer; }
                installed = true;
            }
            HookGates.Subscribed(typeof(THook), hook);
            hooks.Add(hook);
            Rebuild();
        }
    }

    public void Remove(THook hook)
    {
        lock (gate)
        {
            if (hooks.Remove(hook)) Rebuild();
        }
    }

    void Rebuild()
    {
        if (hooks.Count == 0)
        {
            Volatile.Write(ref chain, null);
            return;
        }
        TOrig next = native;
        foreach (THook hook in hooks) next = wrap(hook, next);
        Volatile.Write(ref chain, next);
    }
}

internal static unsafe class HookGate
{
    static volatile bool open;

    [ThreadStatic] internal static bool Loading;

    internal static bool Open => open || Loading || (open = Host.Native()->IsCoreReady() != 0);
}

public static class HookErrors
{
    static readonly Dictionary<string, int> Seen = new();

    public static void Report(string method, Exception error)
    {
        string key = method + "|" + error.GetType().FullName + "|" + error.TargetSite?.Name;
        int count;
        lock (Seen)
        {
            Seen.TryGetValue(key, out count);
            Seen[key] = ++count;
        }
        if (count == 1) Log.Error($"hook {method}: {error}");
        else if ((count & (count - 1)) == 0) Log.Warn($"hook {method}: {error.GetType().Name} again ({count} times): {error.Message}");
    }
}
