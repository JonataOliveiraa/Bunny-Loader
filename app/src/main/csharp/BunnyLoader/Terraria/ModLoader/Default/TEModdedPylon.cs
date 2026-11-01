using System.Linq;
using Terraria.ID;
using Terraria.ModLoader.Core;
using Terraria.ObjectData;

namespace Terraria.ModLoader.Default;

public abstract class TEModdedPylon : ModTileEntity
{
    public override void NetPlaceEntityAttempt(int x, int y)
    {
        if (!GetModPylonFromCoords(x, y, out ModPylon? pylon) || !pylon.CanPlacePylon() || !IsTileValidForEntity(x, y))
        {
            RejectPlacementFromNet(x, y);
            return;
        }

        TileEntityHooks.NetPlace(this, x, y);
    }

    public bool TryGetModPylon(out ModPylon modPylon) => GetModPylonFromCoords(Position.X, Position.Y, out modPylon!);

    static void RejectPlacementFromNet(int x, int y)
    {
        WorldGen.KillTile(x, y);
        if ((Main.netMode & 2) != 0)
            NetMessage.SendData(MessageID.TileManipulation, number2: x, number3: y);
    }

    public new int Place(int i, int j)
    {
        int id = base.Place(i, j);
        Main.PylonSystem.RequestImmediateUpdate();
        return id;
    }

    public new void Kill(int x, int y)
    {
        base.Kill(x, y);
        Main.PylonSystem.RequestImmediateUpdate();
    }

    public override string ToString() => Position.X + "x  " + Position.Y + "y";

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (!tile.active() || !TileID.Sets.CountsAsPylon.Contains(tile.type))
            return false;
        TileObjectData tileData = TileObjectData.GetTileData(tile);
        return !tileData.IsNull && tile.frameY == 0 && tile.frameX % tileData.CoordinateFullWidth == 0;
    }

    public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        TileObjectData tileData = TileObjectData.GetTileData(type, style, alternate);
        int topLeftX = i - tileData.Origin.X;
        int topLeftY = j - tileData.Origin.Y;
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendTileSquare(Main.myPlayer, topLeftX, topLeftY, tileData.Width, tileData.Height);
            TileEntityHooks.RequestPlace(this, topLeftX, topLeftY);
            return -1;
        }

        return Place(topLeftX, topLeftY);
    }

    public int PlacementPreviewHook_CheckIfCanPlace(int x, int y, int type, int style = 0, int direction = 1, int alternate = 0)
    {
        ModPylon? pylon = TileLoader.GetTile(type) as ModPylon;
        return pylon == null || pylon.CanPlacePylon() ? 0 : 1;
    }

    public static bool GetModPylonFromCoords(int x, int y, out ModPylon? modPylon)
    {
        Tile tile = Main.tile[x, y];
        if (tile.active() && TileLoader.GetTile(tile.type) is ModPylon p)
        {
            modPylon = p;
            return true;
        }

        modPylon = null;
        return false;
    }
}
