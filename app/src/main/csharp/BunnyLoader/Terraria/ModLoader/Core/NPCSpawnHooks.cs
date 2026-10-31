using System;
using BunnyLoader;
using Microsoft.Xna.Framework;

namespace Terraria.ModLoader.Core;

internal static class NPCSpawnHooks
{
    sealed class SpawnState { internal NPCSpawnInfo Info; }
    static readonly EntityInstances<SpawnState> States = new();
    [ThreadStatic] static NPCSpawnInfo current;

    internal static NPCSpawnInfo InfoOf(NPC.Spawner spawner)
    {
        if (spawner.IsNull) throw new ArgumentException("Spawn context cannot be null", nameof(spawner));
        NPCSpawnInfo info = current.Spawner == spawner ? current : States.Get(spawner.Pointer)?.Info ?? new NPCSpawnInfo
        {
            Spawner = spawner, SpawnTileX = -1, SpawnTileY = -1, GroundTileY = -1, SpawnTileType = -1, SpawnWallType = -1,
            Player = Main.player[(uint)spawner.defaultTarget < 255u ? spawner.defaultTarget : Main.myPlayer]
        };
        info.Sky = spawner.skyMob;
        info.Lihzahrd = spawner.ZoneLihzhardTemple;
        info.PlayerSafe = spawner.spawnFriendly || spawner.noWorms;
        info.Invasion = spawner.invaders;
        info.Water = spawner.waterTile;
        info.Granite = spawner.nearGranite;
        info.Marble = spawner.nearMarble;
        info.SpiderCave = spawner.spawnSpider;
        info.PlayerInTown = spawner.spawnFriendly;
        info.DesertCave = spawner.spawnUndergroundDesert;
        if (!info.Player.IsNull)
        {
            info.PlayerFloorX = (int)info.Player.Center.X / 16;
            info.PlayerFloorY = (int)info.Player.Bottom.Y / 16;
        }
        return info;
    }

    static void Store(NPC.Spawner spawner)
    {
        SpawnState? state = States.Get(spawner.Pointer);
        if (state == null) States.Set(spawner.Pointer, state = new());
        state.Info = current;
    }

    internal static void Install(Type type, Type baseType, int npcType, ref NPCMethod flags)
    {
        TownNPCHooks.Install(type, baseType, npcType, ref flags);
        bool Has(string name) => ItemHooks.Overrides(type, baseType, name);
        if (Has(nameof(GlobalNPC.EditSpawnFlags)) || Has(nameof(GlobalNPC.EditSpawnInfo))) flags |= NPCMethod.EditSpawnPool;
        if ((flags & (NPCMethod.SpawnChance | NPCMethod.EditSpawnPool)) != 0)
        {
            ItemHooks.Once("NPCSpawnPool", () => On_NPC_Spawner.SpawnAnNPC += SpawnAnNPC);
            ItemHooks.Once("NPCSpawnFlags", () =>
            {
                On_NPC_Spawner.SetSpawnFlags += SetFlags;
                On_NPC_Spawner.SetSpawnFlagsForChosenTile += ChosenTile;
            });
        }
        if ((flags & NPCMethod.EditSpawnRate) != 0) ItemHooks.Once("NPCSpawnRate", () => On_NPC_Spawner.GetSpawnRate += Rate);
        if ((flags & NPCMethod.EditSpawnRange) != 0) ItemHooks.Once("NPCSpawnRange", () => On_NPC_Spawner.GetSpawnArea += Area);
        if ((flags & NPCMethod.SpawnNPC) != 0)
            ItemHooks.Once("NPCNaturalSpawn", () => On_NPC_Spawner.SpawnNPC_int_int_int_int_float_float_float_float_int += Spawned);
    }

    static void SetFlags(On_NPC_Spawner.orig_SetSpawnFlags orig, NPC.Spawner self, Player player)
    {
        orig(self, player);
        NPCSpawnInfo outer = current;
        current = new NPCSpawnInfo { Spawner = self, Player = player, SpawnTileX = -1, SpawnTileY = -1, GroundTileY = -1 };
        Store(self);
        try { NPCLoader.EditSpawnFlags(self); }
        finally { current = outer; }
    }

    static void ChosenTile(On_NPC_Spawner.orig_SetSpawnFlagsForChosenTile orig, NPC.Spawner self, int x, int y, int groundY, int tileType, int wallType)
    {
        orig(self, x, y, groundY, tileType, wallType);
        NPCSpawnInfo outer = current;
        current = new NPCSpawnInfo { Spawner = self, Player = Main.player[(uint)self.defaultTarget < 255u ? self.defaultTarget : Main.myPlayer],
            SpawnTileX = x, SpawnTileY = y, GroundTileY = groundY, SpawnTileType = tileType, SpawnWallType = wallType };
        Store(self);
        try { NPCLoader.EditSpawnInfo(self); }
        finally { current = outer; }
    }

    static void SpawnAnNPC(On_NPC_Spawner.orig_SpawnAnNPC orig, NPC.Spawner self, int x, int y, int tileType, int wallType, bool xRange, int target)
    {
        if (Main.netMode == 1) { orig(self, x, y, tileType, wallType, xRange, target); return; }
        NPCSpawnInfo outer = current;
        NPC.Spawner.FindGroundTile(x, y, out int groundY);
        current = new NPCSpawnInfo { Spawner = self, Player = Main.player[(uint)target < 255u ? target : Main.myPlayer],
            SpawnTileX = x, SpawnTileY = y, GroundTileY = groundY, SpawnTileType = tileType, SpawnWallType = wallType, SafeRangeX = xRange };
        Store(self);
        try
        {
            int? chosen = NPCLoader.ChooseSpawn(self);
            if (chosen == 0) orig(self, x, y, tileType, wallType, xRange, target);
            else if (chosen.HasValue) NPCLoader.SpawnNPC(chosen.Value, x, y);
        }
        finally { current = outer; }
    }

    static NPC Spawned(On_NPC_Spawner.orig_SpawnNPC_int_int_int_int_float_float_float_float_int orig, NPC.Spawner self,
        int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)
    {
        NPC npc = orig(self, X, Y, Type, Start, ai0, ai1, ai2, ai3, Target);
        if (!npc.IsNull && npc.active)
        {
            NPCSpawnInfo info = InfoOf(self);
            NPCLoader.NotifySpawn(npc, info.SpawnTileX >= 0 ? info.SpawnTileX : X / 16, info.SpawnTileY >= 0 ? info.SpawnTileY : Y / 16);
        }
        return npc;
    }

    static void Rate(On_NPC_Spawner.orig_GetSpawnRate orig, NPC.Spawner self, Player player, out int spawnRate, out int maxSpawns)
    {
        orig(self, player, out spawnRate, out maxSpawns);
        if (Main.netMode == 1) return;
        NPCLoader.EditSpawnRate(player, ref spawnRate, ref maxSpawns);
        spawnRate = Math.Max(1, spawnRate);
    }

    static void Area(On_NPC_Spawner.orig_GetSpawnArea orig, Player player, out Rectangle spawnArea, out Rectangle safeArea)
    {
        orig(player, out spawnArea, out safeArea);
        if (Main.netMode == 1) return;
        int x = (int)(NPC.sWidth / 16 * 0.7), y = (int)(NPC.sHeight / 16 * 0.7);
        int held = player.inventory[player.selectedItem].type;
        if (held == 1254 || held == 1299 || player.scope)
        {
            double divisor = held == 1254 && player.scope ? 1.25 : held == 1254 || held == 1299 ? 1.5 : 2;
            x += (int)(NPC.sWidth / 16 * 0.5 / divisor);
            y += (int)(NPC.sHeight / 16 * 0.5 / divisor);
        }
        int sx = NPC.safeRangeX, sy = NPC.safeRangeY;
        NPCLoader.EditSpawnRange(player, ref x, ref y, ref sx, ref sy);
        x = Math.Clamp(x, 1, Main.maxTilesX);
        y = Math.Clamp(y, 1, Main.maxTilesY);
        NPC.safeRangeX = sx = Math.Clamp(sx, 0, Main.maxTilesX);
        NPC.safeRangeY = sy = Math.Clamp(sy, 0, Main.maxTilesY);
        int cx = (int)Math.Floor(player.position.X / 16f), cy = (int)Math.Floor(player.position.Y / 16f);
        int left = Math.Clamp(cx - x, 0, Main.maxTilesX), right = Math.Clamp(cx + x, 0, Main.maxTilesX);
        int top = Math.Clamp(cy - y, 0, Main.maxTilesY), bottom = Math.Clamp(cy + y, 0, Main.maxTilesY);
        spawnArea = new Rectangle(left, top, right - left, bottom - top);
        bool half = Main.dualDungeonsSeed && !player.ZoneOverworldHeight && !player.ZoneSkyHeight;
        safeArea = new Rectangle(cx - (half ? sx / 2 : sx), cy - (half ? sy / 2 : sy), sx * (half ? 1 : 2), sy * (half ? 1 : 2));
    }
}
