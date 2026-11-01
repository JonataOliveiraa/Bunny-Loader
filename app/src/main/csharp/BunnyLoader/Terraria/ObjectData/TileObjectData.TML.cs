using System;
using BunnyLoader.Interop;

namespace Terraria.ObjectData;

public readonly partial struct TileObjectData
{
    public static int GetTileStyle(Tile getTile) => getTile.active() ? Terraria.ModLoader.TileLoader.StyleOf(getTile) : -1;

    public int[] CoordinateHeights
    {
        get => CoordinateHeights_.IsNull ? Array.Empty<int>() : CoordinateHeights_.AsSpan().ToArray();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var array = GameArray<int>.New(new GameClass("System", "Int32"), value.Length);
            value.AsSpan().CopyTo(array.AsSpan());
            CoordinateHeights_ = array;
        }
    }
}
