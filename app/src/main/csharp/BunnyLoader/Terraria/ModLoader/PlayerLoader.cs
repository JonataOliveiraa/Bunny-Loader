using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BunnyLoader;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class PlayerLoader
{
    static readonly List<ModPlayer> Players = new();
    static readonly ConcurrentDictionary<string, PlayerHook> Handles = new();
    static readonly EntityInstances<ModPlayer[]> Instances = new();
    static ModPlayer[] all = Array.Empty<ModPlayer>();
    static int version;

    static readonly PlayerHook HookModifyDrawLayerOrdering = Hook(nameof(ModPlayer.ModifyDrawLayerOrdering));
    static readonly PlayerHook HookHideDrawLayers = Hook(nameof(ModPlayer.HideDrawLayers));

    internal static void Add(ModPlayer player)
    {
        lock (Players)
        {
            if (Players.Exists(p => p.TypeEquals(player)))
                throw new InvalidOperationException($"{player.FullName}: this mod already has a ModPlayer named {player.Name}");
            player.Index = (ushort)Players.Count;
            Players.Add(player);
            Volatile.Write(ref all, Players.ToArray());
            Interlocked.Increment(ref version);
        }
        PlayerHooks.Install(player.GetType());
    }

    internal static ModPlayer[] Templates => Volatile.Read(ref all);

    internal static int Version => Volatile.Read(ref version);

    internal static PlayerHook Hook(string method) => Handles.GetOrAdd(method, static name => new PlayerHook(name));

    internal static ModPlayer[] Overriding(string method) =>
        Templates.Where(p => ItemHooks.Overrides(p.GetType(), typeof(ModPlayer), method)).ToArray();

    internal static ModPlayer[] Of(Player player)
    {
        ModPlayer[] templates = Templates;
        IntPtr pointer = player.Pointer;
        long epoch = GameGc.Collections();
        ModPlayer[]? recent = RecentInstance<ModPlayer[]>.Get(pointer, epoch, 0);
        if (recent != null && recent.Length == templates.Length) return recent;
        ModPlayer[] found = Lookup(player, templates);
        RecentInstance<ModPlayer[]>.Set(pointer, epoch, 0, found);
        return found;
    }

    static ModPlayer[] Lookup(Player player, ModPlayer[] templates)
    {
        ModPlayer[]? instances = Instances.Get(player.Pointer);
        if (instances != null && instances.Length == templates.Length) return instances;
        int old = instances?.Length ?? 0;
        var grown = new ModPlayer[templates.Length];
        if (instances != null) Array.Copy(instances, grown, old);
        for (int i = old; i < templates.Length; i++) grown[i] = templates[i].NewInstance(player);
        Instances.Set(player.Pointer, grown);
        for (int i = old; i < grown.Length; i++)
        {
            ModPlayer m = grown[i];
            try { m.Initialize(); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.Initialize", e); }
        }
        return grown;
    }

    internal static void ModifyDrawLayerOrdering(IDictionary<PlayerDrawLayer, PlayerDrawLayer.Position> positions)
    {
        foreach (ModPlayer player in HookModifyDrawLayerOrdering.Hooked)
        {
            try { player.ModifyDrawLayerOrdering(positions); }
            catch (Exception e) { ItemHooks.Report($"{player.FullName}.ModifyDrawLayerOrdering", e); }
        }
    }

    internal static bool OrdersDrawLayers => HookModifyDrawLayerOrdering.Hooked.Length > 0;

    internal static void HideDrawLayers(Terraria.DataStructures.PlayerDrawSet drawInfo)
    {
        if (HookHideDrawLayers.Hooked.Length == 0) return;
        Each(drawInfo.drawPlayer, HookHideDrawLayers, drawInfo, static (m, i) => m.HideDrawLayers(i));
    }

    internal static ModPlayer[] Hooked(string method) => Hook(method).Hooked;

    internal delegate void RefCall<TState, TValue>(ModPlayer player, TState state, ref TValue value);

    internal delegate bool RefPredicate<TState, TValue>(ModPlayer player, TState state, ref TValue value);

    internal static void Each<TState, TValue>(Player player, string method, TState state, ref TValue value, RefCall<TState, TValue> call) =>
        Each(player, Hook(method), state, ref value, call);

    internal static void Each<TState, TValue>(Player player, PlayerHook hook, TState state, ref TValue value, RefCall<TState, TValue> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return;
        ModPlayer[] instances = Of(player);
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { call(m, state, ref value); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
    }

    internal static void Each<TState>(Player player, string method, TState state, Action<ModPlayer, TState> call) =>
        Each(player, Hook(method), state, call);

    internal static void Each<TState>(Player player, PlayerHook hook, TState state, Action<ModPlayer, TState> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return;
        ModPlayer[] instances = Of(player);
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { call(m, state); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
    }

    internal static bool All<TState>(Player player, string method, TState state, Func<ModPlayer, TState, bool> call) =>
        All(player, Hook(method), state, call);

    internal static bool All<TState>(Player player, PlayerHook hook, TState state, Func<ModPlayer, TState, bool> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return true;
        ModPlayer[] instances = Of(player);
        bool result = true;
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { result &= call(m, state); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
        return result;
    }

    internal static bool All<TState, TValue>(Player player, PlayerHook hook, TState state, ref TValue value, RefPredicate<TState, TValue> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return true;
        ModPlayer[] instances = Of(player);
        bool result = true;
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { result &= call(m, state, ref value); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
        return result;
    }

    internal static bool? Nullable<TState>(Player player, string method, TState state, Func<ModPlayer, TState, bool?> call) =>
        Nullable(player, Hook(method), state, call);

    internal static bool? Nullable<TState>(Player player, PlayerHook hook, TState state, Func<ModPlayer, TState, bool?> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return null;
        ModPlayer[] instances = Of(player);
        bool? result = null;
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try
            {
                bool? value = call(m, state);
                if (value.HasValue && result != false) result = value.Value;
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
        return result;
    }

    internal static float Product<TState>(Player player, string method, TState state, Func<ModPlayer, TState, float> call) =>
        Product(player, Hook(method), state, call);

    internal static float Product<TState>(Player player, PlayerHook hook, TState state, Func<ModPlayer, TState, float> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return 1f;
        ModPlayer[] instances = Of(player);
        float result = 1f;
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { result *= call(m, state); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
        return result;
    }

    internal static void Each(Player player, string method, Action<ModPlayer> call) => Each(player, Hook(method), call);

    internal static void Each(Player player, PlayerHook hook, Action<ModPlayer> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return;
        ModPlayer[] instances = Of(player);
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { call(m); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
    }

    internal static bool All(Player player, string method, Func<ModPlayer, bool> call) => All(player, Hook(method), call);

    internal static bool All(Player player, PlayerHook hook, Func<ModPlayer, bool> call)
    {
        ModPlayer[] hooked = hook.Hooked;
        if (hooked.Length == 0) return true;
        ModPlayer[] instances = Of(player);
        bool result = true;
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { result &= call(m); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{hook.Name}", e); }
        }
        return result;
    }

    internal static string KeyOf(ModPlayer player) => $"{player.Mod.Uid}/{player.Name}";
}

internal sealed class PlayerHook
{
    sealed class Snapshot
    {
        internal readonly int Version;
        internal readonly ModPlayer[] List;

        internal Snapshot(int version, ModPlayer[] list)
        {
            Version = version;
            List = list;
        }
    }

    Snapshot? snapshot;

    internal string Name { get; }

    internal PlayerHook(string name) => Name = name;

    internal ModPlayer[] Hooked
    {
        get
        {
            Snapshot? current = Volatile.Read(ref snapshot);
            int version = PlayerLoader.Version;
            if (current != null && current.Version == version) return current.List;
            ModPlayer[] list = PlayerLoader.Overriding(Name);
            Volatile.Write(ref snapshot, new Snapshot(version, list));
            return list;
        }
    }
}

internal sealed class RecentInstance<T> where T : class
{
    [ThreadStatic] static RecentInstance<T>? current;

    IntPtr pointer;
    long epoch = -1;
    int generation;
    T? value;

    internal static T? Get(IntPtr pointer, long epoch, int generation)
    {
        RecentInstance<T>? recent = current;
        if (recent == null || epoch < 0 || pointer == IntPtr.Zero) return null;
        return recent.pointer == pointer && recent.epoch == epoch && recent.generation == generation ? recent.value : null;
    }

    internal static void Set(IntPtr pointer, long epoch, int generation, T value)
    {
        if (epoch < 0 || pointer == IntPtr.Zero) return;
        RecentInstance<T> recent = current ??= new RecentInstance<T>();
        recent.pointer = pointer;
        recent.epoch = epoch;
        recent.generation = generation;
        recent.value = value;
    }
}
