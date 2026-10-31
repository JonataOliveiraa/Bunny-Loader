using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria.IO;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public static class SystemLoader
{
    static readonly List<ModSystem> Systems = new();
    static readonly Dictionary<string, ModSystem[]> ByHook = new();

    internal static void Add(ModSystem system)
    {
        lock (Systems)
        {
            Systems.Add(system);
            ByHook.Clear();
        }
        SystemHooks.Install(system.GetType());
    }

    internal static ModSystem[] Of(Mod mod)
    {
        lock (Systems) return Systems.Where(s => s.Mod == mod).ToArray();
    }

    internal static ModSystem[] With(string hook)
    {
        lock (Systems)
        {
            if (ByHook.TryGetValue(hook, out ModSystem[]? cached)) return cached;
            ModSystem[] found = Systems.Where(s => ItemHooks.Overrides(s.GetType(), typeof(ModSystem), hook)).ToArray();
            ByHook[hook] = found;
            return found;
        }
    }

    internal static string KeyOf(ModSystem system) => $"{system.Mod.Uid}/{system.Name}";

    static void Run(string hook, Action<ModSystem> action)
    {
        foreach (ModSystem system in With(hook))
        {
            try { action(system); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.{hook}", e); }
        }
    }

    internal static void OnModLoad(Mod mod)
    {
        foreach (ModSystem system in Of(mod))
        {
            if (!ItemHooks.Overrides(system.GetType(), typeof(ModSystem), nameof(ModSystem.OnModLoad))) continue;
            try { system.OnModLoad(); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.OnModLoad", e); }
        }
    }

    public static void OnModUnload() => Run(nameof(ModSystem.OnModUnload), s => s.OnModUnload());

    internal static bool Localized { get; private set; }

    public static void OnLocalizationsLoaded()
    {
        Localized = true;
        Run(nameof(ModSystem.OnLocalizationsLoaded), s => s.OnLocalizationsLoaded());
    }

    public static void ResizeArrays() => Run(nameof(ModSystem.ResizeArrays), s => s.ResizeArrays());

    public static void PostSetupRecipes() => Run(nameof(ModSystem.PostSetupRecipes), s => s.PostSetupRecipes());

    public static void OnWorldLoad() => Run(nameof(ModSystem.OnWorldLoad), s => s.OnWorldLoad());

    public static void OnWorldUnload() => Run(nameof(ModSystem.OnWorldUnload), s => s.OnWorldUnload());

    public static void ClearWorld() => Run(nameof(ModSystem.ClearWorld), s => s.ClearWorld());

    public static void PreSaveAndQuit() => Run(nameof(ModSystem.PreSaveAndQuit), s => s.PreSaveAndQuit());

    public static void PreUpdateEntities() => Run(nameof(ModSystem.PreUpdateEntities), s => s.PreUpdateEntities());

    public static void PreUpdatePlayers() => Run(nameof(ModSystem.PreUpdatePlayers), s => s.PreUpdatePlayers());

    public static void PostUpdatePlayers() => Run(nameof(ModSystem.PostUpdatePlayers), s => s.PostUpdatePlayers());

    public static void PreUpdateNPCs() => Run(nameof(ModSystem.PreUpdateNPCs), s => s.PreUpdateNPCs());

    public static void PostUpdateNPCs() => Run(nameof(ModSystem.PostUpdateNPCs), s => s.PostUpdateNPCs());

    public static void PreUpdateGores() => Run(nameof(ModSystem.PreUpdateGores), s => s.PreUpdateGores());

    public static void PostUpdateGores() => Run(nameof(ModSystem.PostUpdateGores), s => s.PostUpdateGores());

    public static void PreUpdateProjectiles() => Run(nameof(ModSystem.PreUpdateProjectiles), s => s.PreUpdateProjectiles());

    public static void PostUpdateProjectiles() => Run(nameof(ModSystem.PostUpdateProjectiles), s => s.PostUpdateProjectiles());

    public static void PreUpdateItems() => Run(nameof(ModSystem.PreUpdateItems), s => s.PreUpdateItems());

    public static void PostUpdateItems() => Run(nameof(ModSystem.PostUpdateItems), s => s.PostUpdateItems());

    public static void PreUpdateDusts() => Run(nameof(ModSystem.PreUpdateDusts), s => s.PreUpdateDusts());

    public static void PostUpdateDusts() => Run(nameof(ModSystem.PostUpdateDusts), s => s.PostUpdateDusts());

    public static void PreUpdateTime() => Run(nameof(ModSystem.PreUpdateTime), s => s.PreUpdateTime());

    public static void PostUpdateTime() => Run(nameof(ModSystem.PostUpdateTime), s => s.PostUpdateTime());

    public static void PreUpdateWorld() => Run(nameof(ModSystem.PreUpdateWorld), s => s.PreUpdateWorld());

    public static void PostUpdateWorld() => Run(nameof(ModSystem.PostUpdateWorld), s => s.PostUpdateWorld());

    public static void PreUpdateInvasions() => Run(nameof(ModSystem.PreUpdateInvasions), s => s.PreUpdateInvasions());

    public static void PostUpdateInvasions() => Run(nameof(ModSystem.PostUpdateInvasions), s => s.PostUpdateInvasions());

    public static void PostUpdateEverything() => Run(nameof(ModSystem.PostUpdateEverything), s => s.PostUpdateEverything());

    public static void PostUpdateInput() => Run(nameof(ModSystem.PostUpdateInput), s => s.PostUpdateInput());

    public static void ResetNearbyTileEffects() => Run(nameof(ModSystem.ResetNearbyTileEffects), s => s.ResetNearbyTileEffects());

    public static void UpdateUI(GameTime gameTime)
    {
        foreach (ModSystem system in With(nameof(ModSystem.UpdateUI)))
        {
            try { system.UpdateUI(gameTime); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.UpdateUI", e); }
        }
    }

    public static void TileCountsAvailable(ReadOnlySpan<int> tileCounts)
    {
        foreach (ModSystem system in With(nameof(ModSystem.TileCountsAvailable)))
        {
            try { system.TileCountsAvailable(tileCounts); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.TileCountsAvailable", e); }
        }
    }

    public static void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate)
    {
        foreach (ModSystem system in With(nameof(ModSystem.ModifyTimeRate)))
        {
            try { system.ModifyTimeRate(ref timeRate, ref tileUpdateRate, ref eventUpdateRate); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyTimeRate", e); }
        }
    }

    public static bool CanWorldBePlayed(PlayerFileData playerData, WorldFileData worldData, out ModSystem? rejector)
    {
        rejector = null;
        foreach (ModSystem system in With(nameof(ModSystem.CanWorldBePlayed)))
        {
            try
            {
                if (system.CanWorldBePlayed(playerData, worldData)) continue;
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{system.FullName}.CanWorldBePlayed", e);
                continue;
            }
            rejector = system;
            return false;
        }
        return true;
    }

    internal static void SaveWorldData(ModSystem system, TagCompound tag)
    {
        try { system.SaveWorldData(tag); }
        catch (Exception e) { ItemHooks.Report($"{system.FullName}.SaveWorldData", e); }
    }

    internal static void LoadWorldData(ModSystem system, TagCompound tag)
    {
        try { system.LoadWorldData(tag); }
        catch (Exception e) { ItemHooks.Report($"{system.FullName}.LoadWorldData", e); }
    }

    internal static void SaveWorldHeader(ModSystem system, TagCompound tag)
    {
        try { system.SaveWorldHeader(tag); }
        catch (Exception e) { ItemHooks.Report($"{system.FullName}.SaveWorldHeader", e); }
    }
}
