using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class BossBarLoader
{
    static readonly object Gate = new();
    static readonly List<ModBossBar> bossBars = new();
    static readonly List<GlobalBossBar> globalBossBars = new();
    static readonly Dictionary<IntPtr, (int Type, ModBossBar Bar)> assigned = new();

    sealed class DefaultBossBarStyle : ModBossBarStyle
    {
        public override string DisplayName => "Vanilla";
    }

    static readonly ModBossBarStyle vanillaStyle = new DefaultBossBarStyle();
    static readonly List<ModBossBarStyle> bossBarStyles = new() { vanillaStyle };
    static ModBossBarStyle currentStyle = vanillaStyle;
    static bool styleRestored;

    public static ModBossBarStyle CurrentStyle
    {
        get
        {
            if (!styleRestored) RestoreStyle();
            return currentStyle;
        }
    }

    public static void SwitchBossBarStyle(ModBossBarStyle bossBarStyle)
    {
        styleRestored = true;
        if (bossBarStyle == currentStyle) return;
        Select(bossBarStyle);
        SaveStyle();
    }

    internal static void NextBossBarStyle()
    {
        int at = bossBarStyles.IndexOf(CurrentStyle);
        SwitchBossBarStyle(bossBarStyles[(at + 1) % bossBarStyles.Count]);
    }

    internal static void AddBossBarStyle(ModBossBarStyle bossBarStyle)
    {
        lock (Gate) bossBarStyles.Add(bossBarStyle);
        ModTypeLookup<ModBossBarStyle>.Register(bossBarStyle);
        BossBarHooks.Install(false);
        BossBarStyleSetting.Install();
    }

    static void Select(ModBossBarStyle bossBarStyle)
    {
        try { currentStyle.OnDeselected(); }
        catch (Exception e) { ItemHooks.Report("ModBossBarStyle.OnDeselected", e); }
        currentStyle = bossBarStyle;
        try { currentStyle.OnSelected(); }
        catch (Exception e) { ItemHooks.Report("ModBossBarStyle.OnSelected", e); }
    }

    static string StyleFile => Path.Combine(Main.SavePath, "BunnyLoader.bossbar.json");

    static void RestoreStyle()
    {
        string file;
        try { file = StyleFile; }
        catch (Exception) { return; }
        styleRestored = true;
        if (!File.Exists(file)) return;
        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(file));
            if (!json.RootElement.TryGetProperty("Style", out JsonElement style)) return;
            string? name = style.GetString();
            foreach (ModBossBarStyle candidate in bossBarStyles)
                if (candidate != vanillaStyle && candidate.FullName == name) { Select(candidate); return; }
        }
        catch (Exception e) { Log.Error($"BossBarLoader: {file} is broken ({e.Message}); using the vanilla boss bar"); }
    }

    static void SaveStyle()
    {
        try { File.WriteAllText(StyleFile, JsonSerializer.Serialize(new { Style = currentStyle == vanillaStyle ? "" : currentStyle.FullName })); }
        catch (Exception e) { ItemHooks.Report("BossBarLoader (save)", e); }
    }

    internal static IReadOnlyList<GlobalBossBar> Globals => globalBossBars;

    public static int BossBarCount => bossBars.Count;

    internal static void AddBossBar(ModBossBar bossBar)
    {
        lock (Gate)
        {
            bossBar.index = bossBars.Count;
            bossBars.Add(bossBar);
        }
        BossBarHooks.Install(false);
    }

    internal static void AddGlobalBossBar(GlobalBossBar globalBossBar)
    {
        lock (Gate) globalBossBars.Add(globalBossBar);
        BossBarHooks.Install(true);
    }

    public static ModBossBar? GetBossBar(int type) => (uint)type < (uint)bossBars.Count ? bossBars[type] : null;

    public static bool NpcToBossBar(NPC npc, out ModBossBar? value)
    {
        value = Get(npc);
        return value != null;
    }

    internal static bool AnyAssigned
    {
        get { lock (Gate) return assigned.Count > 0; }
    }

    internal static ModBossBar? Get(NPC npc)
    {
        if (npc.IsNull) return null;
        lock (Gate)
            return assigned.TryGetValue(npc.Pointer, out var entry) && entry.Type == npc.type ? entry.Bar : null;
    }

    internal static void Set(NPC npc, ModBossBar? bar)
    {
        if (npc.IsNull) return;
        lock (Gate)
        {
            if (bar == null) assigned.Remove(npc.Pointer);
            else assigned[npc.Pointer] = (npc.type, bar);
        }
    }

    public static bool PreDraw(SpriteBatch spriteBatch, NPC npc, ModBossBar? bossBar, ref BossBarDrawParams drawParams)
    {
        bool modify = true;
        foreach (GlobalBossBar global in globalBossBars)
        {
            try { modify &= global.PreDraw(spriteBatch, npc, ref drawParams); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.PreDraw", e); }
        }
        if (modify && bossBar != null)
        {
            try { modify &= bossBar.PreDraw(spriteBatch, npc, ref drawParams); }
            catch (Exception e) { ItemHooks.Report($"{bossBar.FullName}.PreDraw", e); }
        }
        return modify;
    }

    public static void PostDraw(SpriteBatch spriteBatch, NPC npc, ModBossBar? bossBar, BossBarDrawParams drawParams)
    {
        if (bossBar != null)
        {
            try { bossBar.PostDraw(spriteBatch, npc, drawParams); }
            catch (Exception e) { ItemHooks.Report($"{bossBar.FullName}.PostDraw", e); }
        }
        foreach (GlobalBossBar global in globalBossBars)
        {
            try { global.PostDraw(spriteBatch, npc, drawParams); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.PostDraw", e); }
        }
    }
}
