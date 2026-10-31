using Terraria.ModLoader;

namespace CsNpcSpawn.Content;

public sealed class LegacyNpc : SpawnNpcBase
{
    public static int LegacyChances;
    public override float SpawnChance(NPCSpawnInfo info)
    {
        LegacyChances++;
        ContextValid &= info.Player.whoAmI == Terraria.Main.myPlayer && info.SpawnTileX == Checks.X && info.SpawnTileY == Checks.Y;
        return 1f;
    }

}
