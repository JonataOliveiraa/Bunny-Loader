using Terraria.ID;

namespace Terraria;

public readonly unsafe partial struct WorldGen
{
    public static void SpreadInfectionToNearbyTile(int x, int y, int conversionType, int range = 3)
    {
        if (!AllowedToSpreadInfections)
            return;
        if (!Main.hardMode || (NPC.downedPlantBoss && genRand.NextBool(2)))
            return;
        bool keepSpreading = true;
        while (keepSpreading)
        {
            keepSpreading = false;
            int testX = x + genRand.Next(-range, range + 1);
            int testY = y + genRand.Next(-range, range + 1);
            if (!InWorld(testX, testY, 10))
                return;
            int before = Main.tile[testX, testY].type;
            if (!TileID.Sets.Infectable[before])
                return;
            if (nearbyChlorophyte(testX, testY) && conversionType != BiomeConversionID.Hallow)
            {
                ChlorophyteDefense(testX, testY);
                return;
            }
            if (CountNearBlocksTypes(testX, testY, 2, 1, TileID.Sunflower) > 0)
                return;
            Convert(testX, testY, conversionType, 0, true, false);
            if (before != Main.tile[testX, testY].type)
                keepSpreading = genRand.NextBool(2);
        }
    }

    public static void ConvertTile(int i, int j, int newType)
    {
        Tile tile = Main.tile[i, j];
        if (tile.type == newType)
            return;
        TryConvertingOrKillingTreesAboveIfTheyWouldBecomeInvalid(i, j, newType);
        tile.type = (ushort)newType;
        SquareTileFrame(i, j);
        if (Main.netMode != NetmodeID.SinglePlayer)
            NetMessage.SendTileSquare(-1, i, j);
    }

    public static void ConvertWall(int i, int j, int newType)
    {
        Tile tile = Main.tile[i, j];
        if (tile.wall == newType)
            return;
        tile.wall = (ushort)newType;
        SquareWallFrame(i, j);
        if (Main.netMode != NetmodeID.SinglePlayer)
            NetMessage.SendTileSquare(-1, i, j);
    }
}
