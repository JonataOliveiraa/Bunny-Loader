using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public static class SystemLoader
{
    static readonly List<ModSystem> Systems = new();
    static readonly Dictionary<string, ModSystem[]> ByHook = new();
    static int generation;

    sealed class HookList
    {
        internal readonly string Name;
        ModSystem[] systems = Array.Empty<ModSystem>();
        int built = -1;

        internal HookList(string name) => Name = name;

        internal ModSystem[] Get() => Volatile.Read(ref built) == Volatile.Read(ref generation) ? systems : Build();

        ModSystem[] Build()
        {
            lock (Systems)
            {
                ModSystem[] found = With(Name);
                systems = found;
                Volatile.Write(ref built, generation);
                return found;
            }
        }
    }

    static class Lists
    {
        internal static readonly HookList OnModUnload = new(nameof(ModSystem.OnModUnload));
        internal static readonly HookList OnLocalizationsLoaded = new(nameof(ModSystem.OnLocalizationsLoaded));
        internal static readonly HookList ResizeArrays = new(nameof(ModSystem.ResizeArrays));
        internal static readonly HookList PostSetupRecipes = new(nameof(ModSystem.PostSetupRecipes));
        internal static readonly HookList PostWorldLoad = new(nameof(ModSystem.PostWorldLoad));
        internal static readonly HookList OnWorldLoad = new(nameof(ModSystem.OnWorldLoad));
        internal static readonly HookList OnWorldUnload = new(nameof(ModSystem.OnWorldUnload));
        internal static readonly HookList ClearWorld = new(nameof(ModSystem.ClearWorld));
        internal static readonly HookList PreSaveAndQuit = new(nameof(ModSystem.PreSaveAndQuit));
        internal static readonly HookList PreUpdateEntities = new(nameof(ModSystem.PreUpdateEntities));
        internal static readonly HookList PreUpdatePlayers = new(nameof(ModSystem.PreUpdatePlayers));
        internal static readonly HookList PostUpdatePlayers = new(nameof(ModSystem.PostUpdatePlayers));
        internal static readonly HookList PreUpdateNPCs = new(nameof(ModSystem.PreUpdateNPCs));
        internal static readonly HookList PostUpdateNPCs = new(nameof(ModSystem.PostUpdateNPCs));
        internal static readonly HookList PreUpdateGores = new(nameof(ModSystem.PreUpdateGores));
        internal static readonly HookList PostUpdateGores = new(nameof(ModSystem.PostUpdateGores));
        internal static readonly HookList PreUpdateProjectiles = new(nameof(ModSystem.PreUpdateProjectiles));
        internal static readonly HookList PostUpdateProjectiles = new(nameof(ModSystem.PostUpdateProjectiles));
        internal static readonly HookList PreUpdateItems = new(nameof(ModSystem.PreUpdateItems));
        internal static readonly HookList PostUpdateItems = new(nameof(ModSystem.PostUpdateItems));
        internal static readonly HookList PreUpdateDusts = new(nameof(ModSystem.PreUpdateDusts));
        internal static readonly HookList PostUpdateDusts = new(nameof(ModSystem.PostUpdateDusts));
        internal static readonly HookList PreUpdateTime = new(nameof(ModSystem.PreUpdateTime));
        internal static readonly HookList PostUpdateTime = new(nameof(ModSystem.PostUpdateTime));
        internal static readonly HookList PreUpdateWorld = new(nameof(ModSystem.PreUpdateWorld));
        internal static readonly HookList PostUpdateWorld = new(nameof(ModSystem.PostUpdateWorld));
        internal static readonly HookList PreUpdateInvasions = new(nameof(ModSystem.PreUpdateInvasions));
        internal static readonly HookList PostUpdateInvasions = new(nameof(ModSystem.PostUpdateInvasions));
        internal static readonly HookList PostUpdateEverything = new(nameof(ModSystem.PostUpdateEverything));
        internal static readonly HookList PostUpdateInput = new(nameof(ModSystem.PostUpdateInput));
        internal static readonly HookList ResetNearbyTileEffects = new(nameof(ModSystem.ResetNearbyTileEffects));
        internal static readonly HookList UpdateUI = new(nameof(ModSystem.UpdateUI));
        internal static readonly HookList TileCountsAvailable = new(nameof(ModSystem.TileCountsAvailable));
        internal static readonly HookList ModifyTimeRate = new(nameof(ModSystem.ModifyTimeRate));
        internal static readonly HookList CanWorldBePlayed = new(nameof(ModSystem.CanWorldBePlayed));
        internal static readonly HookList ModifyScreenPosition = new(nameof(ModSystem.ModifyScreenPosition));
        internal static readonly HookList ModifyTransformMatrix = new(nameof(ModSystem.ModifyTransformMatrix));
        internal static readonly HookList ModifySunLightColor = new(nameof(ModSystem.ModifySunLightColor));
        internal static readonly HookList ModifyLightingBrightness = new(nameof(ModSystem.ModifyLightingBrightness));
        internal static readonly HookList PostDrawTiles = new(nameof(ModSystem.PostDrawTiles));
        internal static readonly HookList ModifyInterfaceLayers = new(nameof(ModSystem.ModifyInterfaceLayers));
        internal static readonly HookList PostDrawInterface = new(nameof(ModSystem.PostDrawInterface));
        internal static readonly HookList PreDrawMapIconOverlay = new(nameof(ModSystem.PreDrawMapIconOverlay));
        internal static readonly HookList PostDrawFullscreenMap = new(nameof(ModSystem.PostDrawFullscreenMap));
        internal static readonly HookList HijackGetData = new(nameof(ModSystem.HijackGetData));
        internal static readonly HookList HijackSendData = new(nameof(ModSystem.HijackSendData));
        internal static readonly HookList NetSend = new(nameof(ModSystem.NetSend));
    }

    internal static void Add(ModSystem system)
    {
        lock (Systems)
        {
            Systems.Add(system);
            ByHook.Clear();
            Volatile.Write(ref generation, generation + 1);
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

    internal static ModSystem? ByKey(string key)
    {
        lock (Systems)
        {
            foreach (ModSystem system in Systems)
                if (KeyOf(system) == key) return system;
            return null;
        }
    }

    static void Run(HookList hook, Action<ModSystem> action)
    {
        foreach (ModSystem system in hook.Get())
        {
            try { action(system); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.{hook.Name}", e); }
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

    public static void OnModUnload() => Run(Lists.OnModUnload, s => s.OnModUnload());

    internal static bool Localized { get; private set; }

    public static void OnLocalizationsLoaded()
    {
        Localized = true;
        Run(Lists.OnLocalizationsLoaded, s => s.OnLocalizationsLoaded());
    }

    public static void ResizeArrays() => Run(Lists.ResizeArrays, s => s.ResizeArrays());

    public static void PostSetupRecipes() => Run(Lists.PostSetupRecipes, s => s.PostSetupRecipes());

    public static void PostWorldLoad() => Run(Lists.PostWorldLoad, s => s.PostWorldLoad());

    public static void OnWorldLoad() => Run(Lists.OnWorldLoad, s => s.OnWorldLoad());

    public static void OnWorldUnload() => Run(Lists.OnWorldUnload, s => s.OnWorldUnload());

    public static void ClearWorld() => Run(Lists.ClearWorld, s => s.ClearWorld());

    public static void PreSaveAndQuit() => Run(Lists.PreSaveAndQuit, s => s.PreSaveAndQuit());

    public static void PreUpdateEntities() => Run(Lists.PreUpdateEntities, s => s.PreUpdateEntities());

    public static void PreUpdatePlayers() => Run(Lists.PreUpdatePlayers, s => s.PreUpdatePlayers());

    public static void PostUpdatePlayers() => Run(Lists.PostUpdatePlayers, s => s.PostUpdatePlayers());

    public static void PreUpdateNPCs() => Run(Lists.PreUpdateNPCs, s => s.PreUpdateNPCs());

    public static void PostUpdateNPCs() => Run(Lists.PostUpdateNPCs, s => s.PostUpdateNPCs());

    public static void PreUpdateGores() => Run(Lists.PreUpdateGores, s => s.PreUpdateGores());

    public static void PostUpdateGores() => Run(Lists.PostUpdateGores, s => s.PostUpdateGores());

    public static void PreUpdateProjectiles() => Run(Lists.PreUpdateProjectiles, s => s.PreUpdateProjectiles());

    public static void PostUpdateProjectiles() => Run(Lists.PostUpdateProjectiles, s => s.PostUpdateProjectiles());

    public static void PreUpdateItems() => Run(Lists.PreUpdateItems, s => s.PreUpdateItems());

    public static void PostUpdateItems() => Run(Lists.PostUpdateItems, s => s.PostUpdateItems());

    public static void PreUpdateDusts() => Run(Lists.PreUpdateDusts, s => s.PreUpdateDusts());

    public static void PostUpdateDusts() => Run(Lists.PostUpdateDusts, s => s.PostUpdateDusts());

    public static void PreUpdateTime() => Run(Lists.PreUpdateTime, s => s.PreUpdateTime());

    public static void PostUpdateTime() => Run(Lists.PostUpdateTime, s => s.PostUpdateTime());

    public static void PreUpdateWorld() => Run(Lists.PreUpdateWorld, s => s.PreUpdateWorld());

    public static void PostUpdateWorld() => Run(Lists.PostUpdateWorld, s => s.PostUpdateWorld());

    public static void PreUpdateInvasions() => Run(Lists.PreUpdateInvasions, s => s.PreUpdateInvasions());

    public static void PostUpdateInvasions() => Run(Lists.PostUpdateInvasions, s => s.PostUpdateInvasions());

    public static void PostUpdateEverything() => Run(Lists.PostUpdateEverything, s => s.PostUpdateEverything());

    public static void PostUpdateInput() => Run(Lists.PostUpdateInput, s => s.PostUpdateInput());

    public static void ResetNearbyTileEffects() => Run(Lists.ResetNearbyTileEffects, s => s.ResetNearbyTileEffects());

    public static void UpdateUI(GameTime gameTime)
    {
        foreach (ModSystem system in Lists.UpdateUI.Get())
        {
            try { system.UpdateUI(gameTime); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.UpdateUI", e); }
        }
    }

    public static void TileCountsAvailable(ReadOnlySpan<int> tileCounts)
    {
        foreach (ModSystem system in Lists.TileCountsAvailable.Get())
        {
            try { system.TileCountsAvailable(tileCounts); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.TileCountsAvailable", e); }
        }
    }

    public static void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate)
    {
        foreach (ModSystem system in Lists.ModifyTimeRate.Get())
        {
            try { system.ModifyTimeRate(ref timeRate, ref tileUpdateRate, ref eventUpdateRate); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyTimeRate", e); }
        }
    }

    public static bool CanWorldBePlayed(PlayerFileData playerData, WorldFileData worldData, out ModSystem? rejector)
    {
        rejector = null;
        foreach (ModSystem system in Lists.CanWorldBePlayed.Get())
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

    public static void ModifyScreenPosition() => Run(Lists.ModifyScreenPosition, s => s.ModifyScreenPosition());

    public static void ModifyTransformMatrix(ref SpriteViewMatrix Transform)
    {
        foreach (ModSystem system in Lists.ModifyTransformMatrix.Get())
        {
            try { system.ModifyTransformMatrix(ref Transform); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyTransformMatrix", e); }
        }
    }

    public static void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
    {
        if (Main.gameMenu) return;
        foreach (ModSystem system in Lists.ModifySunLightColor.Get())
        {
            try { system.ModifySunLightColor(ref tileColor, ref backgroundColor); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifySunLightColor", e); }
        }
    }

    public static void ModifyLightingBrightness(ref float scale)
    {
        foreach (ModSystem system in Lists.ModifyLightingBrightness.Get())
        {
            try { system.ModifyLightingBrightness(ref scale); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyLightingBrightness", e); }
        }
    }

    public static void PostDrawTiles() => Run(Lists.PostDrawTiles, s => s.PostDrawTiles());

    public static void ModifyInterfaceLayers(List<Terraria.UI.GameInterfaceLayer> layers)
    {
        foreach (ModSystem system in Lists.ModifyInterfaceLayers.Get())
        {
            try { system.ModifyInterfaceLayers(layers); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyInterfaceLayers", e); }
        }
    }

    public static void PostDrawInterface(SpriteBatch spriteBatch)
    {
        foreach (ModSystem system in Lists.PostDrawInterface.Get())
        {
            try { system.PostDrawInterface(spriteBatch); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.PostDrawInterface", e); }
        }
    }

    public static void PreDrawMapIconOverlay(IReadOnlyList<IMapLayer> layers, MapOverlayDrawContext mapOverlayDrawContext)
    {
        foreach (ModSystem system in Lists.PreDrawMapIconOverlay.Get())
        {
            try { system.PreDrawMapIconOverlay(layers, mapOverlayDrawContext); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.PreDrawMapIconOverlay", e); }
        }
    }

    public static void PostDrawFullscreenMap(ref string mouseText)
    {
        foreach (ModSystem system in Lists.PostDrawFullscreenMap.Get())
        {
            try { system.PostDrawFullscreenMap(ref mouseText); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.PostDrawFullscreenMap", e); }
        }
    }

    internal static bool HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber)
    {
        bool hijacked = false;
        long readerPos = reader.BaseStream.Position;
        long biggestReaderPos = readerPos;
        foreach (ModSystem system in Lists.HijackGetData.Get())
        {
            try
            {
                if (system.HijackGetData(ref messageType, ref reader, playerNumber))
                {
                    hijacked = true;
                    biggestReaderPos = Math.Max(reader.BaseStream.Position, biggestReaderPos);
                }
            }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.HijackGetData", e); }
            reader.BaseStream.Position = readerPos;
        }
        if (hijacked) reader.BaseStream.Position = biggestReaderPos;
        return hijacked;
    }

    internal static bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
    {
        bool result = false;
        foreach (ModSystem system in Lists.HijackSendData.Get())
        {
            try { result |= system.HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.HijackSendData", e); }
        }
        return result;
    }

    internal static ModSystem[] NetSystems() => Lists.NetSend.Get();

    internal static void NetSend(ModSystem system, BinaryWriter writer)
    {
        try { system.NetSend(writer); }
        catch (Exception e) { ItemHooks.Report($"{system.FullName}.NetSend", e); }
    }

    internal static void NetReceive(ModSystem system, BinaryReader reader)
    {
        try { system.NetReceive(reader); }
        catch (Exception e) { ItemHooks.Report($"{system.FullName}.NetReceive", e); }
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
