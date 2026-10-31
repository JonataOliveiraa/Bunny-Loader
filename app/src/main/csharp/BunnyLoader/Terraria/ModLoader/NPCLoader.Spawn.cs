using System;
using System.Collections.Generic;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.Utilities;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    public static void EditSpawnFlags(NPC.Spawner spawner)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Hooks & NPCMethod.EditSpawnPool) == 0) continue;
            try { global.EditSpawnFlags(spawner); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.EditSpawnFlags", e); }
        }
    }

    public static void EditSpawnInfo(NPC.Spawner spawner)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Hooks & NPCMethod.EditSpawnPool) == 0) continue;
            try { global.EditSpawnInfo(spawner); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.EditSpawnInfo", e); }
        }
    }

    public static void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Hooks & NPCMethod.EditSpawnRate) == 0) continue;
            try { global.EditSpawnRate(player, ref spawnRate, ref maxSpawns); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.EditSpawnRate", e); }
        }
    }

    public static void EditSpawnRange(Player player, ref int spawnRangeX, ref int spawnRangeY, ref int safeRangeX, ref int safeRangeY)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Hooks & NPCMethod.EditSpawnRange) == 0) continue;
            try { global.EditSpawnRange(player, ref spawnRangeX, ref spawnRangeY, ref safeRangeX, ref safeRangeY); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.EditSpawnRange", e); }
        }
    }

    public static int? ChooseSpawn(NPC.Spawner spawner)
    {
        if (spawner.SpawnTileX < 0 || spawner.SpawnTileY < 0) return null;
        NPCSpawnHelper.Reset();
        NPCSpawnHelper.DoChecks(spawner);
        IDictionary<int, float> pool = new Dictionary<int, float> { [0] = 1f };
        foreach (ModNPC? own in npcs)
        {
            if (!Has(own, NPCMethod.SpawnChance)) continue;
            try
            {
                float weight = own!.SpawnChance(spawner);
                if (float.IsFinite(weight) && weight > 0) pool[own.RegisteredType] = weight;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.SpawnChance", e); }
        }
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Hooks & NPCMethod.EditSpawnPool) == 0) continue;
            try { global.EditSpawnPool(pool, spawner); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.EditSpawnPool", e); }
        }
        double total = 0;
        foreach (var entry in pool)
            if (entry.Key >= -65 && entry.Key < NPCCount && float.IsFinite(entry.Value) && entry.Value > 0) total += entry.Value;
        if (total <= 0) return null;
        double choice = Main.rand.NextDouble() * total;
        foreach (var entry in pool)
        {
            if (entry.Key < -65 || entry.Key >= NPCCount || !float.IsFinite(entry.Value) || entry.Value <= 0) continue;
            if (choice < entry.Value) return entry.Key;
            choice -= entry.Value;
        }
        return null;
    }

    public static int SpawnNPC(int type, int tileX, int tileY)
    {
        int index;
        ModNPC? own = GetNPC(type);
        try { index = own?.SpawnNPC(tileX, tileY) ?? NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(), tileX * 16 + 8, tileY * 16, type); }
        catch (Exception e) { ItemHooks.Report($"NPC {type}.SpawnNPC", e); return Main.maxNPCs; }
        if ((uint)index < (uint)Main.maxNPCs && Main.npc[index].active) NotifySpawn(Main.npc[index], tileX, tileY);
        return index;
    }

    internal static void NotifySpawn(NPC npc, int x, int y)
    {
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.SpawnNPC)) continue;
            try { global!.SpawnNPC(npc.whoAmI, x, y); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.SpawnNPC", e); }
        }
    }
}
