using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.ObjectData;

namespace Terraria.DataStructures;

public readonly partial struct TileEntity
{
    public static bool TryGet<T>(int i, int j, out T tileEntity) where T : ModTileEntity
    {
        Point16 topLeft = TileObjectData.TopLeft(i, j);
        return TryGet(topLeft, out tileEntity);
    }

    public static bool TryGet<T>(Point16 position, out T tileEntity) where T : ModTileEntity
    {
        if (TileEntityHooks.At(position.X, position.Y) is T found)
        {
            tileEntity = found;
            return true;
        }
        tileEntity = null!;
        return false;
    }
}
