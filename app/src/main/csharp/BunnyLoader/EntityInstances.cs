using System;
using System.Collections.Generic;
using BunnyLoader.Interop;

namespace BunnyLoader;

internal sealed unsafe class EntityInstances<T> where T : class
{
    const int SweepEvery = 512;

    readonly Dictionary<IntPtr, Entry> entries = new();
    int added;

    struct Entry
    {
        public uint Handle;
        public T Value;
    }

    public int Count
    {
        get { lock (entries) return entries.Count; }
    }

    public T? Get(IntPtr entity)
    {
        if (entity == IntPtr.Zero) return null;
        lock (entries)
        {
            if (!entries.TryGetValue(entity, out Entry entry)) return null;
            NativeApi* api = Host.Native();
            if (api->GcHandleTarget(entry.Handle) == entity) return entry.Value;
            Remove(api, entity, entry);
            return null;
        }
    }

    public void Set(IntPtr entity, T? value)
    {
        if (entity == IntPtr.Zero) return;
        NativeApi* api = Host.Native();
        lock (entries)
        {
            if (entries.TryGetValue(entity, out Entry old))
            {
                if (value != null && api->GcHandleTarget(old.Handle) == entity)
                {
                    entries[entity] = new Entry { Handle = old.Handle, Value = value };
                    return;
                }
                Remove(api, entity, old);
            }
            if (value == null) return;
            uint handle = api->GcHandleNewWeak(entity);
            if (handle == 0) return;
            entries[entity] = new Entry { Handle = handle, Value = value };
            if (++added % SweepEvery == 0) Sweep(api);
        }
    }

    void Remove(NativeApi* api, IntPtr entity, Entry entry)
    {
        entries.Remove(entity);
        api->GcHandleFree(entry.Handle);
    }

    void Sweep(NativeApi* api)
    {
        List<IntPtr>? dead = null;
        foreach ((IntPtr entity, Entry entry) in entries)
        {
            if (api->GcHandleTarget(entry.Handle) != entity) (dead ??= new List<IntPtr>()).Add(entity);
        }
        if (dead == null) return;
        foreach (IntPtr entity in dead) Remove(api, entity, entries[entity]);
    }
}
