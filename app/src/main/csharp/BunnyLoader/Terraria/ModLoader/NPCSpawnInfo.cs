namespace Terraria.ModLoader;

public struct NPCSpawnInfo
{
    public NPC.Spawner Spawner;
    public int SpawnTileX, SpawnTileY, SpawnTileType, GroundTileY, SpawnWallType;
    public Player Player;
    public int PlayerFloorX, PlayerFloorY;
    public bool Sky, Lihzahrd, PlayerSafe, Invasion, Water, Granite, Marble, SpiderCave, PlayerInTown, DesertCave, SafeRangeX;
}
