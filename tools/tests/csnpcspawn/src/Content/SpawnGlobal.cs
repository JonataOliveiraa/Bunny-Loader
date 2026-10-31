using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CsNpcSpawn.Content;

public sealed class SpawnGlobal : GlobalNPC
{
    public static int Mode, Observed, InfoCalls, FlagsCalls;
    public static bool ContextValid = true;
    public override bool AppliesToEntity(NPC npc, bool lateInstantiation) => npc.type == ModContent.NPCType<SpawnNpc>() || npc.type == ModContent.NPCType<LegacyNpc>();
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) { spawnRate = 7; maxSpawns = Checks.Running ? 11 : 0; }
    public override void EditSpawnRange(Player player, ref int x, ref int y, ref int sx, ref int sy) { x = 30; y = 20; sx = 8; sy = 6; }
    public override void EditSpawnFlags(NPC.Spawner spawner) { FlagsCalls++; ContextValid &= spawner.Player.whoAmI == Main.myPlayer; }
    public override void EditSpawnInfo(NPC.Spawner spawner) { InfoCalls++; spawner.nearGranite = true; }
    public override void EditSpawnPool(IDictionary<int, float> pool, NPC.Spawner spawner)
    {
        ContextValid &= spawner.Player.whoAmI == Main.myPlayer && spawner.nearGranite;
        pool.Remove(0);
        if (Mode == 1) { pool.Clear(); pool[ModContent.NPCType<LegacyNpc>()] = 1; }
        if (Mode == 2) pool.Clear();
        if (Mode == 3) { pool.Clear(); pool[-4] = 1; }
        if (Mode == 4) { pool.Clear(); pool[0] = 1; }
        if (Mode == 5) { pool.Clear(); pool[ModContent.NPCType<SpawnNpc>()] = float.NaN; pool[ModContent.NPCType<LegacyNpc>()] = -1; }
    }
    public override void SpawnNPC(int index, int x, int y)
    {
        Observed++;
        ContextValid &= Main.npc[index].ai[0] == 7 && x == Checks.X && y == Checks.Y;
    }
}
