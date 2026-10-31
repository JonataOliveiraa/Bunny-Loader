using System;
using System.Collections.Generic;
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
