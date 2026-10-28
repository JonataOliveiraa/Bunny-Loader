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

    public TOrig? Chain => Volatile.Read(ref chain);

    public int Count => hooks.Count;

    public void Add(THook hook)
    {
        lock (gate)
        {
            if (!installed)
            {
                install();
                installed = true;
            }
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

public static class HookErrors
{
    public static void Report(string method, Exception error) => Log.Error($"hook {method}: {error}");
}
