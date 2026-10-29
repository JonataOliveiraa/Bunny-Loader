using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class PlayerLoader
{
    static readonly List<ModPlayer> Players = new();
    static readonly Dictionary<string, ModPlayer[]> Overriders = new();
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
            lock (Overriders) Overriders.Clear();
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

    internal static ModPlayer[] Hooked(string method)
    {
        lock (Overriders)
        {
            if (Overriders.TryGetValue(method, out ModPlayer[]? list)) return list;
            list = all.Where(p => ItemHooks.Overrides(p.GetType(), typeof(ModPlayer), method)).ToArray();
            Overriders[method] = list;
            return list;
        }
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
