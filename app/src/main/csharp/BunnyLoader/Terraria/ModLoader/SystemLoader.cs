using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    internal static ModSystem? ByKey(string key)
    {
        lock (Systems)
        {
            foreach (ModSystem system in Systems)
                if (KeyOf(system) == key) return system;
            return null;
        }
    }

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

    public static void ModifyScreenPosition() => Run(nameof(ModSystem.ModifyScreenPosition), s => s.ModifyScreenPosition());

    public static void ModifyTransformMatrix(ref SpriteViewMatrix Transform)
    {
        foreach (ModSystem system in With(nameof(ModSystem.ModifyTransformMatrix)))
        {
            try { system.ModifyTransformMatrix(ref Transform); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyTransformMatrix", e); }
        }
    }

    public static void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
    {
        if (Main.gameMenu) return;
        foreach (ModSystem system in With(nameof(ModSystem.ModifySunLightColor)))
        {
            try { system.ModifySunLightColor(ref tileColor, ref backgroundColor); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifySunLightColor", e); }
        }
    }

    public static void ModifyLightingBrightness(ref float scale)
    {
        foreach (ModSystem system in With(nameof(ModSystem.ModifyLightingBrightness)))
        {
            try { system.ModifyLightingBrightness(ref scale); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyLightingBrightness", e); }
        }
    }

    public static void PostDrawTiles() => Run(nameof(ModSystem.PostDrawTiles), s => s.PostDrawTiles());

    public static void ModifyInterfaceLayers(List<Terraria.UI.GameInterfaceLayer> layers)
    {
        foreach (ModSystem system in With(nameof(ModSystem.ModifyInterfaceLayers)))
        {
            try { system.ModifyInterfaceLayers(layers); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyInterfaceLayers", e); }
        }
    }

    public static void PostDrawInterface(SpriteBatch spriteBatch)
    {
        foreach (ModSystem system in With(nameof(ModSystem.PostDrawInterface)))
        {
            try { system.PostDrawInterface(spriteBatch); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.PostDrawInterface", e); }
        }
    }

    public static void PreDrawMapIconOverlay(IReadOnlyList<IMapLayer> layers, MapOverlayDrawContext mapOverlayDrawContext)
    {
        foreach (ModSystem system in With(nameof(ModSystem.PreDrawMapIconOverlay)))
        {
            try { system.PreDrawMapIconOverlay(layers, mapOverlayDrawContext); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.PreDrawMapIconOverlay", e); }
        }
    }

    public static void PostDrawFullscreenMap(ref string mouseText)
    {
        foreach (ModSystem system in With(nameof(ModSystem.PostDrawFullscreenMap)))
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
        foreach (ModSystem system in With(nameof(ModSystem.HijackGetData)))
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
        foreach (ModSystem system in With(nameof(ModSystem.HijackSendData)))
        {
            try { result |= system.HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.HijackSendData", e); }
        }
        return result;
    }

    internal static ModSystem[] NetSystems() => With(nameof(ModSystem.NetSend));

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
