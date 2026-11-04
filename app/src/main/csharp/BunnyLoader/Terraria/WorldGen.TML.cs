using Terraria.ID;

namespace Terraria;

public readonly unsafe partial struct WorldGen
{
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
