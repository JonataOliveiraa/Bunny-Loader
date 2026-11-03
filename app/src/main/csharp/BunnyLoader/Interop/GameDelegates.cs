using System;
using System.Collections.Generic;

namespace BunnyLoader.Interop;

public static unsafe class GameDelegates
{
    sealed class Entry
    {
        internal required Delegate Handler;
        internal required IntPtr Target;
        internal required GameHandle Game;
    }

    static readonly object Gate = new();
    static readonly Dictionary<IntPtr, Delegate> ByTarget = new();
    static readonly Dictionary<(string Event, IntPtr Owner), List<Entry>> ByEvent = new();
    static readonly HashSet<string> Reported = new();

    public static void Report(string what, Exception e)
    {
        bool first;
        lock (Gate) first = Reported.Add(what + "|" + e.GetType().FullName);
        if (first) Log.Error($"{what} threw: {e}");
    }

    public static T? Get<T>(IntPtr target) where T : Delegate
    {
        lock (Gate) return ByTarget.TryGetValue(target, out Delegate? handler) ? handler as T : null;
    }

    public static IntPtr ParamClass(IntPtr method, int parameter) => Host.Native()->ParamClass(method, parameter);

    public static IntPtr New(Delegate handler, IntPtr klass, IntPtr fn, out IntPtr target)
    {
        IntPtr owner;
        IntPtr game = Host.Native()->NewDelegate(klass, fn, &owner);
        if (game == IntPtr.Zero) throw new InvalidOperationException($"{handler.GetType().Name}: the game delegate could not be created");
        lock (Gate) ByTarget[owner] = handler;
        target = owner;
        return game;
    }

    public static void Add(Delegate? handler, string name, IntPtr owner, IntPtr klass, IntPtr fn, Action<IntPtr> add)
    {
        if (handler == null) return;
        IntPtr game = New(handler, klass, fn, out IntPtr target);
        var entry = new Entry { Handler = handler, Target = target, Game = new GameHandle(game) };
        lock (Gate)
        {
            if (!ByEvent.TryGetValue((name, owner), out List<Entry>? list)) ByEvent[(name, owner)] = list = new List<Entry>();
            list.Add(entry);
        }
        try { add(game); }
        catch
        {
            Forget(name, owner, entry);
            throw;
        }
    }

    public static void Remove(Delegate? handler, string name, IntPtr owner, Action<IntPtr> remove)
    {
        if (handler == null) return;
        Entry? entry = null;
        lock (Gate)
        {
            if (ByEvent.TryGetValue((name, owner), out List<Entry>? list))
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i].Handler.Equals(handler))
                    {
                        entry = list[i];
                        break;
                    }
        }
        if (entry == null) return;
        remove(entry.Game.Target);
        Forget(name, owner, entry);
    }

    static void Forget(string name, IntPtr owner, Entry entry)
    {
        lock (Gate)
        {
            ByTarget.Remove(entry.Target);
            if (ByEvent.TryGetValue((name, owner), out List<Entry>? list))
            {
                list.Remove(entry);
                if (list.Count == 0) ByEvent.Remove((name, owner));
            }
        }
        entry.Game.Dispose();
    }
}
