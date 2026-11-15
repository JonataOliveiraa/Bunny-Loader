using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct NPC
{
    public readonly partial struct Spawner
    {
        public int SpawnTileX => NPCSpawnHooks.BaseOf(this).SpawnTileX;
        public int SpawnTileY => NPCSpawnHooks.BaseOf(this).SpawnTileY;
        public int GroundTileY => NPCSpawnHooks.BaseOf(this).GroundTileY;
        public int SpawnTileType => NPCSpawnHooks.BaseOf(this).SpawnTileType;
        public int SpawnWallType => NPCSpawnHooks.BaseOf(this).SpawnWallType;
        public Player Player => NPCSpawnHooks.BaseOf(this).Player;
        public bool SafeRangeX => NPCSpawnHooks.BaseOf(this).SafeRangeX;
    }
}
