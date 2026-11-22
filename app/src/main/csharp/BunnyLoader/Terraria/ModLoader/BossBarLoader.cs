using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class BossBarLoader
{
    static readonly object Gate = new();
    static readonly List<ModBossBar> bossBars = new();
    static readonly List<GlobalBossBar> globalBossBars = new();
    static readonly Dictionary<IntPtr, (uint Handle, int Type, ModBossBar Bar)> assigned = new();
    static readonly List<IntPtr> dead = new();
    static long swept = -1;

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
        catch (Exception e) { Log.Warn($"BossBarLoader: {file} is broken ({e.Message}); using the vanilla boss bar"); }
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
        get
        {
            lock (Gate)
            {
                if (assigned.Count == 0) return false;
                NativeApi* api = Host.Native();
                if (!Swept(api)) Sweep(api);
                foreach ((IntPtr pointer, var entry) in assigned)
                {
                    if (entry.Handle == 0) return true;
                    var npc = new NPC(pointer);
                    int slot = npc.whoAmI;
                    if ((uint)slot < (uint)Main.maxNPCs && Main.npc[slot] == npc && npc.active && npc.type == entry.Type) return true;
                }
                return false;
            }
        }
    }

    internal static ModBossBar? Get(NPC npc)
    {
        if (npc.IsNull) return null;
        lock (Gate)
        {
            if (assigned.Count == 0) return null;
            NativeApi* api = Host.Native();
            bool alive = Swept(api);
            if (!assigned.TryGetValue(npc.Pointer, out var entry) || entry.Type != npc.type) return null;
            if (alive || entry.Handle == 0 || api->GcHandleTarget(entry.Handle) == npc.Pointer) return entry.Bar;
            Remove(api, npc.Pointer);
            return null;
        }
    }

    static bool Swept(NativeApi* api)
    {
        long collections = GameGc.Collections();
        if (collections < 0) return false;
        if (collections == swept) return true;
        Sweep(api);
        swept = collections;
        return true;
    }

    static void Sweep(NativeApi* api)
    {
        foreach ((IntPtr pointer, var entry) in assigned)
            if (entry.Handle != 0 && api->GcHandleTarget(entry.Handle) != pointer) dead.Add(pointer);
        foreach (IntPtr pointer in dead) Remove(api, pointer);
        dead.Clear();
    }

    internal static void Set(NPC npc, ModBossBar? bar)
    {
        if (npc.IsNull) return;
        if (bar is NeverValidBossBar) BossBarHooks.Install(false);
        lock (Gate)
        {
            NativeApi* api = Host.Native();
            if (bar == null)
            {
                Remove(api, npc.Pointer);
                return;
            }
            if (assigned.TryGetValue(npc.Pointer, out var old) && (old.Handle == 0 || api->GcHandleTarget(old.Handle) == npc.Pointer))
            {
                assigned[npc.Pointer] = (old.Handle, npc.type, bar);
                return;
            }
            Remove(api, npc.Pointer);
            assigned[npc.Pointer] = (api->GcHandleNewWeak(npc.Pointer), npc.type, bar);
        }
    }

    internal static void Forget(NPC npc)
    {
        if (npc.IsNull) return;
        lock (Gate)
        {
            if (assigned.Count > 0) Remove(Host.Native(), npc.Pointer);
        }
    }

    static void Remove(NativeApi* api, IntPtr pointer)
    {
        if (!assigned.Remove(pointer, out var entry)) return;
        if (entry.Handle != 0) api->GcHandleFree(entry.Handle);
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
