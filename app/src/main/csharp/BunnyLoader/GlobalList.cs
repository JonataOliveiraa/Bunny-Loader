using System;
using System.Collections.Generic;
using System.Threading;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace BunnyLoader;

internal sealed class GlobalList<TEntity, TGlobal> where TEntity : struct, IGameObject where TGlobal : GlobalType<TEntity, TGlobal>
{
    readonly object gate = new();
    readonly EntityInstances<TGlobal?[]> perEntity = new();
    TGlobal[] templates = Array.Empty<TGlobal>();

    public int Count => Volatile.Read(ref templates).Length;

    public void Add(TGlobal global)
    {
        global.ValidateInstanceFields();
        lock (gate)
        {
            var grown = new TGlobal[templates.Length + 1];
            Array.Copy(templates, grown, templates.Length);
            global.StaticIndex = (short)templates.Length;
            grown[^1] = global;
            Volatile.Write(ref templates, grown);
        }
    }

    public TGlobal?[]? Begin(TEntity entity)
    {
        TGlobal[] all = Volatile.Read(ref templates);
        if (all.Length == 0) return null;
        var slots = new TGlobal?[all.Length];
        foreach (TGlobal global in all) slots[global.StaticIndex] = Instantiate(global, entity, false);
        perEntity.Set(entity.Pointer, slots);
        return slots;
    }

    public void Finish(TEntity entity, TGlobal?[]? slots)
    {
        if (slots == null) return;
        TGlobal[] all = Volatile.Read(ref templates);
        bool any = false;
        foreach (TGlobal global in all)
        {
            if (global.StaticIndex >= slots.Length) continue;
            slots[global.StaticIndex] ??= Instantiate(global, entity, true);
            any |= slots[global.StaticIndex] != null;
        }
        if (!any) perEntity.Set(entity.Pointer, null);
    }

    public TGlobal?[]? Slots(TEntity entity) => Count == 0 ? null : perEntity.Get(entity.Pointer);

    public IReadOnlyList<TGlobal> Of(TEntity entity)
    {
        var found = new List<TGlobal>();
        TGlobal?[]? slots = Slots(entity);
        if (slots == null) return found;
        foreach (TGlobal? global in slots)
        {
            if (global != null) found.Add(global);
        }
        return found;
    }

    public bool TryGet<T>(TEntity entity, out T result) where T : TGlobal
    {
        T? template = ModContent.GetInstance<T>();
        return TryGet(entity, template == null ? (short)-1 : template.StaticIndex, out result);
    }

    public bool TryGet<T>(TEntity entity, short index, out T result) where T : TGlobal
    {
        TGlobal?[]? slots = index < 0 ? null : Slots(entity);
        if (slots != null && index < slots.Length && slots[index] is T found)
        {
            result = found;
            return true;
        }
        result = null!;
        return false;
    }

    public T Get<T>(TEntity entity) where T : TGlobal =>
        TryGet(entity, out T result) ? result : throw new KeyNotFoundException($"{typeof(T).Name} does not apply to this {typeof(TEntity).Name}");

    public T Get<T>(TEntity entity, T baseInstance) where T : TGlobal =>
        TryGet(entity, baseInstance.StaticIndex, out T result) ? result
            : throw new KeyNotFoundException($"{typeof(T).Name} does not apply to this {typeof(TEntity).Name}");

    public void Clear(TEntity entity)
    {
        if (Count > 0) perEntity.Set(entity.Pointer, null);
    }

    public void Clone(TEntity from, TEntity to)
    {
        if (Count == 0) return;
        TGlobal?[]? source = perEntity.Get(from.Pointer);
        if (source == null)
        {
            perEntity.Set(to.Pointer, null);
            return;
        }
        var copy = new TGlobal?[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            TGlobal? global = source[i];
            if (global == null) continue;
            try
            {
                copy[i] = global.InstancePerEntity ? global.Clone(from, to) : global;
            }
            catch (Exception e)
            {
                Log.Error($"{global.FullName}: Clone threw: {e}");
            }
        }
        perEntity.Set(to.Pointer, copy);
    }

    static TGlobal? Instantiate(TGlobal global, TEntity entity, bool late)
    {
        try
        {
            return global.AppliesToEntity(entity, late) ? global.NewInstance(entity) : null;
        }
        catch (Exception e)
        {
            Log.Error($"{global.FullName}: {(late ? "late " : "")}AppliesToEntity/NewInstance threw: {e}");
            return null;
        }
    }
}
