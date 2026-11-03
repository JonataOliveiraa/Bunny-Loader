using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent;
using Terraria.IO;

namespace Terraria.ModLoader.Core;

internal static class BannerHooks
{
    const string SaveKey = "#csBanners";

    static readonly Dictionary<int, int> ItemOfBanner = new();
    static readonly Dictionary<int, int> BannerOfItems = new();
    static readonly List<ModNPC> Representatives = new();
    static JsonObject kept = new();
    static int size;
    static int sending;

    internal static bool BannerOfItem(int item, out int banner) => BannerOfItems.TryGetValue(item, out banner);

    internal static void ContentReady()
    {
        ItemOfBanner.Clear();
        BannerOfItems.Clear();
        Representatives.Clear();
        bool any = false;
        var npcs = new List<ModNPC>();
        foreach (Mod mod in Host.Mods)
            npcs.AddRange(mod.GetContent<ModNPC>());
        foreach (ModNPC npc in npcs)
        {
            if (npc.Banner <= 0 || npc.BannerItem <= 0)
            {
                any |= npc.Banner > 0;
                continue;
            }
            any = true;
            if (ItemOfBanner.TryGetValue(npc.Banner, out int previous) && previous != npc.BannerItem)
            {
                Log.Error($"{npc.FullName}: BannerItem {npc.BannerItem} conflicts with item {previous} of banner {npc.Banner}");
                continue;
            }
            ItemOfBanner[npc.Banner] = npc.BannerItem;
            BannerOfItems[npc.BannerItem] = npc.Banner;
        }
        foreach (ModNPC npc in npcs)
        {
            if ((npc.BannerItem > 0 && npc.Banner <= 0) || (npc.Banner >= NPCLoader.VanillaCount && !ItemOfBanner.ContainsKey(npc.Banner)))
                Log.Error($"{npc.FullName}: set Banner and BannerItem on the representative NPC");
            if (npc.Banner == npc.Type && ItemOfBanner.ContainsKey(npc.Type))
                Representatives.Add(npc);
        }
        if (!any)
            return;
        Resize();
        ItemHooks.Once("Banners", () =>
        {
            On_BannerSystem.NPCtoBanner += NPCtoBanner;
            On_BannerSystem.BannerToItem += BannerToItem;
            On_BannerSystem.BannerToNPC += BannerToNPC;
            On_BannerSystem.Clear += Clear;
            On_BannerSystem.Save += Save;
            On_BannerSystem_NetBannersModule.WriteFullState += WriteFullState;
            On_SceneMetrics.Reset += Reset;
            On_WorldFile.LoadWorld += LoadWorld;
            On_WorldFile.InternalSaveWorld += InternalSaveWorld;
        });
    }

    static int NPCtoBanner(On_BannerSystem.orig_NPCtoBanner orig, int i) =>
        i >= NPCLoader.VanillaCount && NPCLoader.GetNPC(i) is ModNPC npc ? npc.Banner : orig(i);

    static int BannerToItem(On_BannerSystem.orig_BannerToItem orig, int banner) =>
        ItemOfBanner.TryGetValue(banner, out int item) ? item : orig(banner);

    static int BannerToNPC(On_BannerSystem.orig_BannerToNPC orig, int i) =>
        i >= NPCLoader.VanillaCount && NPCLoader.GetNPC(i) != null ? i : orig(i);

    static void Resize()
    {
        size = Math.Max(size, NPCLoader.NPCCount);
        GameArray<int> kills = BannerSystem.killCount;
        if (!kills.IsNull && kills.Length < size)
            BannerSystem.killCount = kills.Resized(size);
        GameArray<ushort> claimable = BannerSystem.claimableBanners;
        if (!claimable.IsNull && claimable.Length < size)
            BannerSystem.claimableBanners = claimable.Resized(size);
        GameArray<bool> fresh = BannerSystem.newBanner;
        if (!fresh.IsNull && fresh.Length < size)
            BannerSystem.newBanner = fresh.Resized(size);
        Fit(Main.PlayerSceneMetrics);
        Fit(Main.SceneMetrics);
    }

    internal static void Fit(SceneMetrics metrics)
    {
        if (metrics.IsNull)
            return;
        GameArray<bool> buffs = metrics.NPCBannerBuff;
        if (!buffs.IsNull && buffs.Length < size)
            metrics.NPCBannerBuff = buffs.Resized(size);
    }

    static void Reset(On_SceneMetrics.orig_Reset orig, SceneMetrics self)
    {
        Fit(self);
        orig(self);
    }

    static void Clear(On_BannerSystem.orig_Clear orig)
    {
        orig();
        Resize();
        kept = new JsonObject();
    }

    static Terraria.Net.NetPacket WriteFullState(On_BannerSystem_NetBannersModule.orig_WriteFullState orig)
    {
        sending++;
        try { return orig(); }
        finally { sending--; }
    }

    static void Save(On_BannerSystem.orig_Save orig, GameObject writer)
    {
        int vanilla = BannerSystem.MaxBannerTypes;
        GameArray<int> kills = BannerSystem.killCount;
        GameArray<ushort> claimable = BannerSystem.claimableBanners;
        if (sending > 0 || kills.Length <= vanilla)
        {
            orig(writer);
            return;
        }
        BannerSystem.killCount = kills.Resized(vanilla);
        BannerSystem.claimableBanners = claimable.Resized(vanilla);
        try { orig(writer); }
        finally
        {
            BannerSystem.killCount = kills;
            BannerSystem.claimableBanners = claimable;
        }
    }

    static string KeyOf(ModNPC npc) => $"{npc.Mod.Uid}/{npc.Name}";

    static string? DataFile() => Main.worldPathName is { Length: > 0 } path ? path + ".bl.json" : null;

    static void LoadWorld(On_WorldFile.orig_LoadWorld orig, bool canCheckFileState)
    {
        orig(canCheckFileState);
        try { Restore(SystemHooks.ReadObject(DataFile())[SaveKey] as JsonObject); }
        catch (Exception e) { ItemHooks.Report("ModBannerTile (load)", e); }
    }

    static void Restore(JsonObject? saved)
    {
        Resize();
        kept = saved?.DeepClone() as JsonObject ?? new JsonObject();
        GameArray<int> kills = BannerSystem.killCount;
        GameArray<ushort> claimable = BannerSystem.claimableBanners;
        GameArray<bool> fresh = BannerSystem.newBanner;
        foreach (ModNPC npc in Representatives)
        {
            int type = npc.Type;
            if (type >= kills.Length)
                continue;
            JsonObject? entry = kept[KeyOf(npc)] as JsonObject;
            int count = Math.Max(0, entry?["c"]?.GetValue<int>() ?? 0);
            int pending = Math.Clamp(entry?["n"]?.GetValue<int>() ?? 0, 0, ushort.MaxValue);
            kills[type] = count;
            claimable[type] = (ushort)pending;
            fresh[type] = pending > 0 && (entry?["f"]?.GetValue<bool>() ?? false);
        }
        BannerSystem.RefreshHasBannerAvailable();
        BannerSystem.RefreshAnyNewClaimableBanners();
        BannerSystem.bannerState++;
    }

    static void InternalSaveWorld(On_WorldFile.orig_InternalSaveWorld orig, bool useCloudSaving, bool resetTime)
    {
        orig(useCloudSaving, resetTime);
        if (useCloudSaving || DataFile() is not string file)
            return;
        try
        {
            JsonObject entries = Snapshot();
            JsonObject all = SystemHooks.ReadObject(file);
            if (entries.Count > 0)
                all[SaveKey] = entries;
            else if (!all.Remove(SaveKey))
                return;
            SystemHooks.WriteObject(file, all);
        }
        catch (Exception e) { ItemHooks.Report("ModBannerTile (save)", e); }
    }

    static JsonObject Snapshot()
    {
        var result = (JsonObject)kept.DeepClone();
        GameArray<int> kills = BannerSystem.killCount;
        GameArray<ushort> claimable = BannerSystem.claimableBanners;
        GameArray<bool> fresh = BannerSystem.newBanner;
        foreach (ModNPC npc in Representatives)
        {
            int type = npc.Type;
            string key = KeyOf(npc);
            if (type >= kills.Length || (kills[type] == 0 && claimable[type] == 0))
            {
                result.Remove(key);
                continue;
            }
            result[key] = new JsonObject { ["c"] = kills[type], ["n"] = (int)claimable[type], ["f"] = fresh[type] };
        }
        return result;
    }
}
