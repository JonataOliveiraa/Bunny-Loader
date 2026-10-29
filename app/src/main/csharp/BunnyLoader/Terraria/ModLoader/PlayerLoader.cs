using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class PlayerLoader
{
    static readonly List<ModPlayer> Players = new();
    static readonly ConcurrentDictionary<string, ModPlayer[]> Overriders = new();
    static readonly EntityInstances<ModPlayer[]> Instances = new();
    static ModPlayer[] all = Array.Empty<ModPlayer>();

    internal static void Add(ModPlayer player)
    {
        lock (Players)
        {
            if (Players.Exists(p => p.TypeEquals(player)))
                throw new InvalidOperationException($"{player.FullName}: this mod already has a ModPlayer named {player.Name}");
            player.Index = (ushort)Players.Count;
            Players.Add(player);
            all = Players.ToArray();
            Overriders.Clear();
        }
        PlayerHooks.Install(player.GetType());
    }

    internal static ModPlayer[] Templates => all;

    internal static ModPlayer[] Of(Player player)
    {
        ModPlayer[] templates = all;
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

    internal static ModPlayer[] Hooked(string method) =>
        Overriders.TryGetValue(method, out ModPlayer[]? list)
            ? list
            : Overriders.GetOrAdd(method, name => all.Where(p => ItemHooks.Overrides(p.GetType(), typeof(ModPlayer), name)).ToArray());

    internal delegate void RefCall<TState, TValue>(ModPlayer player, TState state, ref TValue value);

    internal static void Each<TState, TValue>(Player player, string method, TState state, ref TValue value, RefCall<TState, TValue> call)
    {
        ModPlayer[] hooked = Hooked(method);
        if (hooked.Length == 0) return;
        ModPlayer[] instances = Of(player);
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { call(m, state, ref value); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{method}", e); }
        }
    }

    internal static void Each<TState>(Player player, string method, TState state, Action<ModPlayer, TState> call)
    {
        ModPlayer[] hooked = Hooked(method);
        if (hooked.Length == 0) return;
        ModPlayer[] instances = Of(player);
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { call(m, state); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{method}", e); }
        }
    }

    internal static bool All<TState>(Player player, string method, TState state, Func<ModPlayer, TState, bool> call)
    {
        ModPlayer[] hooked = Hooked(method);
        if (hooked.Length == 0) return true;
        ModPlayer[] instances = Of(player);
        bool result = true;
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { result &= call(m, state); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{method}", e); }
        }
        return result;
    }

    internal static bool? Nullable<TState>(Player player, string method, TState state, Func<ModPlayer, TState, bool?> call)
    {
        ModPlayer[] hooked = Hooked(method);
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
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{method}", e); }
        }
        return result;
    }

    internal static float Product<TState>(Player player, string method, TState state, Func<ModPlayer, TState, float> call)
    {
        ModPlayer[] hooked = Hooked(method);
        if (hooked.Length == 0) return 1f;
        ModPlayer[] instances = Of(player);
        float result = 1f;
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { result *= call(m, state); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{method}", e); }
        }
        return result;
    }

    internal static void Each(Player player, string method, Action<ModPlayer> call)
    {
        ModPlayer[] hooked = Hooked(method);
        if (hooked.Length == 0) return;
        ModPlayer[] instances = Of(player);
        foreach (ModPlayer template in hooked)
        {
            ModPlayer m = instances[template.Index];
            try { call(m); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{method}", e); }
        }
    }

    internal static bool All(Player player, string method, Func<ModPlayer, bool> call)
    {
        bool result = true;
        Each(player, method, m => result &= call(m));
        return result;
    }

    internal static string KeyOf(ModPlayer player) => $"{player.Mod.Uid}/{player.Name}";
}
