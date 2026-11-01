using System;
using BunnyLoader.Interop;

namespace Terraria.ObjectData;

public readonly partial struct TileObjectData
{
    public static int GetTileStyle(Tile getTile) => getTile.active() ? Terraria.ModLoader.TileLoader.StyleOf(getTile) : -1;

    static bool Part(Tile tile, out int partX, out int partY)
    {
        partX = partY = 0;
        if (!tile.active()) return false;
        TileObjectData data = GetTileData(tile.type, 0, 0);
        if (data.IsNull || data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0) return false;
        int padding = data.CoordinatePadding;
        partX = tile.frameX % data.CoordinateFullWidth / Math.Max(1, data.CoordinateWidth + padding);
        int[] heights = data.CoordinateHeights;
        int rest = tile.frameY % data.CoordinateFullHeight;
        while (partY + 1 < data.Height && partY < heights.Length && rest - heights[partY] - padding >= 0)
        {
            rest -= heights[partY] + padding;
            partY++;
        }
        return true;
    }

    public static Terraria.DataStructures.Point16 TopLeft(int i, int j) =>
        Part(Main.tile[i, j], out int partX, out int partY) ? new Terraria.DataStructures.Point16(i - partX, j - partY) : new Terraria.DataStructures.Point16(i, j);

    public static Terraria.DataStructures.Point16 TopLeft(Terraria.DataStructures.Point16 position) => TopLeft(position.X, position.Y);

    public static bool IsTopLeft(Tile tile) => !Part(tile, out int partX, out int partY) || (partX == 0 && partY == 0);

    public static bool IsTopLeft(int i, int j) => IsTopLeft(Main.tile[i, j]);

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
