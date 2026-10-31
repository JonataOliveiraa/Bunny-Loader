using System;
using Terraria.ID;
using Terraria.ObjectData;

namespace Terraria.ModLoader.Core;

internal static partial class TileHooks
{
    internal static bool CheckObject(int i, int j, int type)
    {
        if (TileID.Sets.Torches[type] || TileID.Sets.Platforms[type])
            return false;
        var data = TileObjectData.GetTileData(type, 0, 0);
        if (data.IsNull)
            return false;
        if (WorldGen.destroyObject)
            return true;
        Tile cell = Main.tile[i, j];
        int frameX = cell.frameX, frameY = cell.frameY, style = TileLoader.StyleAt(i, j);
        data = TileObjectData.GetTileData(cell);
        if (data.IsNull)
            return true;
        int partFrameX = frameX % data.CoordinateFullWidth, partFrameY = frameY % data.CoordinateFullHeight;
        int partX = partFrameX / (data.CoordinateWidth + data.CoordinatePadding), partY = 0, remaining = partFrameY;
        var heights = data.CoordinateHeights_;
        while (partY + 1 < data.Height && remaining - heights[partY] - data.CoordinatePadding >= 0)
        {
            remaining -= heights[partY] + data.CoordinatePadding;
            partY++;
        }

        int left = i - partX, top = j - partY;
        bool whole = true;
        for (int x = left; x < left + data.Width; x++)
            for (int y = top; y < top + data.Height; y++)
            {
                Tile t = Main.tile[x, y];
                if (!t.active() || t.type != type)
                    whole = false;
            }

        bool breakable = TileID.Sets.BreakableWhenPlacing[type], stay;
        TileID.Sets.BreakableWhenPlacing[type] = true;
        try
        {
            stay = whole && TileObject.CanPlace(left + data.Origin.X, top + data.Origin.Y, type, style, 0, out _, false, default);
        }
        finally
        {
            TileID.Sets.BreakableWhenPlacing[type] = breakable;
        }

        if (stay)
            return true;
        WorldGen.destroyObject = true;
        try
        {
            if (data.Width > 1 || data.Height > 1)
                WorldGen.KillTile_DropItems(i, j, cell, true);
            for (int x = left; x < left + data.Width; x++)
                for (int y = top; y < top + data.Height; y++)
                {
                    Tile t = Main.tile[x, y];
                    if (t.active() && t.type == type)
                        WorldGen.KillTile(x, y, false, false, false);
                }

            TileLoader.GetTile(type)?.KillMultiTile(left, top, frameX - partFrameX, frameY - partFrameY);
        }
        finally
        {
            WorldGen.destroyObject = false;
        }

        for (int x = left - 1; x <= left + data.Width; x++)
            for (int y = top - 1; y <= top + data.Height; y++)
                WorldGen.TileFrame(x, y, false, false);
        return true;
    }
}
