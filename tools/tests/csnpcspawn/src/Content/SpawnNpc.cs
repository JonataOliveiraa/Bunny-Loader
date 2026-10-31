using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;

namespace CsNpcSpawn.Content;

public sealed class SpawnNpc : SpawnNpcBase
{
    public static int Chances;
    public override float SpawnChance(NPC.Spawner spawner)
    {
        Chances++;
        ContextValid &= spawner.Player.whoAmI == Main.myPlayer && spawner.SpawnTileX == Checks.X && spawner.SpawnTileY == Checks.Y
            && spawner.GroundTileY >= Checks.Y && spawner.SpawnTileType == 2 && spawner.SpawnWallType == 0 && !spawner.SafeRangeX;
        return SpawnCondition.OverworldDaySlime.Chance > 0 ? 3f : 0f;
    }
}
