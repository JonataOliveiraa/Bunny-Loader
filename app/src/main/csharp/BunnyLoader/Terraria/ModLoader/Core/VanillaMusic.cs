using System;
using System.Collections.Generic;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class VanillaMusic
{
    static readonly Dictionary<int, (int Normal, int Otherworld)> Groups = Build();

    static Dictionary<int, (int, int)> Build()
    {
        var map = new Dictionary<int, (int, int)>();
        void Add(int[] types, int normal, int otherworld)
        {
            foreach (int type in types) map[type] = (normal, otherworld);
        }
        Add(new[] { 13, 14, 15 }, 23, 1);
        Add(new[] { 26, 27, 28, 29, 111, 471, 472 }, 11, 11);
        Add(new[] { 35, 36 }, 24, 0);
        Add(new[] { 50 }, 19, 0);
        Add(new[] { 113, 114 }, 2, 3);
        Add(new[] { 125, 126 }, 21, 2);
        Add(new[] { 127 }, 22, 2);
        Add(new[] { 128, 129, 130, 131 }, 22, 0);
        Add(new[] { 134 }, 18, 2);
        Add(new[] { 135, 136 }, 18, 0);
        Add(new[] { 143, 144, 145 }, 3, 11);
        Add(new[] { 266 }, 3, 1);
        Add(new[] { 212, 213, 214, 215, 216, 252, 491, 662 }, 8, 8);
        Add(new[] { 222 }, 5, 1);
        Add(new[] { 245 }, 4, 2);
        Add(new[] { 262, 263, 264 }, 6, 6);
        Add(new[] { 370 }, 15, 15);
        Add(new[] { 381, 382, 383, 384, 385, 386, 387, 388, 389, 390, 391, 392, 395, 520 }, 9, 9);
        Add(new[] { 398 }, 7, 7);
        Add(new[] { 422, 493, 507, 517 }, 10, 10);
        Add(new[] { 439 }, 20, 2);
        Add(new[] { 636 }, 14, 14);
        Add(new[] { 657 }, 13, 13);
        Add(new[] { 668 }, 16, 16);
        return map;
    }

    static int TierOfGroup(int group)
    {
        if (group == 7) return 8;
        if (group is 17 or 9 or 10 or 6) return 7;
        if (group is 8 or 11 or 12) return 5;
        return group > 0 ? 6 : 0;
    }

    static int NpcGroup(bool otherworld)
    {
        var screen = Main.screenPosition;
        int sx = (int)screen.X, sy = (int)screen.Y, sw = Main.screenWidth, sh = Main.screenHeight;
        var oldOnes = NPCID.Sets.BelongsToInvasionOldOnesArmy;
        bool special = Main.remixWorld && Main.getGoodWorld;
        var npcs = Main.npc;
        for (int i = 0; i < npcs.Length; i++)
        {
            NPC npc = npcs[i];
            if (!npc.active) continue;
            int type = npc.type;
            int range = 5000, group = 0;
            if (type is 379 or 438)
            {
                if (type == 379 ? npc.ai[3] >= 0 : npc.ai[1] == 1)
                {
                    range = 1600;
                    group = otherworld ? 2 : 20;
                }
            }
            else if (Groups.TryGetValue(type, out var g))
            {
                group = otherworld ? g.Otherworld : g.Normal;
            }
            if (type < oldOnes.Length && oldOnes[type]) group = 12;
            if (group == 0 && npc.boss) group = 1;
            if (special && type is 127 or 134 or 125 or 126) group = 17;
            if (group == 0) continue;
            var position = npc.position;
            int cx = (int)(position.X + npc.width / 2) - range;
            int cy = (int)(position.Y + npc.height / 2) - range;
            if (sx < cx + range * 2 && cx < sx + sw && sy < cy + range * 2 && cy < sy + sh) return group;
        }
        return 0;
    }

    internal static SceneEffectPriority Tier(bool otherworld)
    {
        if (CreditsRollEvent.IsEventOngoing) return (SceneEffectPriority)9;
        SceneMetrics metrics = Main.SceneMetrics;
        if (metrics.InTorchGodMinigame) return SceneEffectPriority.BossHigh;
        if (!Main.showSplash)
        {
            int group = NpcGroup(otherworld);
            int tier = TierOfGroup(group);
            if (tier > 0 && !(otherworld && group >= 18)) return (SceneEffectPriority)tier;
        }
        var screen = Main.screenPosition;
        int screenHeight = Main.screenHeight;
        double surfaceY = Main.worldSurface * 16 + screenHeight / 2;
        bool high = screen.Y / 16 < Main.worldSurface + 10 || Main.remixWorld;
        if (high && (Main.pumpkinMoon || Main.snowMoon)) return SceneEffectPriority.Event;
        if (!otherworld && Main.ShouldPlayRainbowBoulderMusic) return SceneEffectPriority.Event;

        var center = metrics.Center;
        float cy = center.Y;
        double underworld = Main.UnderworldLayer * 16;
        double rock = Main.rockLayer * 16;
        bool town = metrics.TownNPCCount >= 3 && !metrics.ZoneShadowCandle && !metrics.ZoneGraveyard;
        double scale = Math.Pow(Main.maxTilesX / 4200.0, 2);
        double space = ((screen.Y + screenHeight / 2) / 16 - (65 + 10 * scale)) / (Main.worldSurface / 5);
        bool environment =
            (Main.eclipse && (Main.remixWorld ? cy > rock : cy < surfaceY)) ||
            (!otherworld && Main.slimeRain && !metrics.ZoneGraveyard && (!Main.bloodMoon || Main.dayTime) && cy < surfaceY) ||
            (Main.remixWorld && Main.bloodMoon && !metrics.ZoneCrimson && !metrics.ZoneCorrupt && cy > rock && cy <= underworld) ||
            (Main.remixWorld && Main.bloodMoon && cy > underworld && center.X / 16 > Main.maxTilesX * 0.37 + 50 && center.X / 16 < Main.maxTilesX * 0.63) ||
            metrics.ZoneShimmer ||
            (!otherworld && town && Main.dayTime && ((Main.cloudAlpha == 0 && !Main._shouldUseWindyDayMusic) || cy >= surfaceY)) ||
            (!otherworld && town && !Main.dayTime && ((!Main.bloodMoon && Main.cloudAlpha == 0) || cy >= surfaceY)) ||
            metrics.ZoneSandstorm || cy > underworld || space < 1;
        if (environment) return SceneEffectPriority.Environment;

        int tx = (int)(center.X / 16), ty = (int)(cy / 16);
        bool inWorld = tx >= 0 && ty >= 0 && tx < Main.maxTilesX && ty < Main.maxTilesY;
        bool temple = inWorld && Main.tile[tx, ty].wall == 87;
        bool mushroom = (Main.bgStyle == 9 && cy < surfaceY) || Main.undergroundBackground == 2;
        if (temple || metrics.ZoneDungeon || mushroom || metrics.ZoneCorrupt || metrics.ZoneCrimson) return SceneEffectPriority.BiomeHigh;
        if (metrics.ZoneMeteor || metrics.ZoneGraveyard || metrics.ZoneJungle || metrics.ZoneSnow ||
            (!otherworld && (metrics.ZoneUndergroundDesert || metrics.ZoneDesert))) return SceneEffectPriority.BiomeMedium;
        return SceneEffectPriority.BiomeLow;
    }
}
