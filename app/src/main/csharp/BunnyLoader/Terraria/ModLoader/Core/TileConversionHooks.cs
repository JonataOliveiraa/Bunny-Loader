using System;

namespace Terraria.ModLoader.Core;

internal static class TileConversionHooks
{
    static int depth;

    static bool installed;

    internal readonly struct Depth : IDisposable
    {
        public Depth() => depth++;

        public void Dispose() => depth--;
    }

    internal static void Install()
    {
        if (installed)
            return;
        installed = true;
        On_WorldGen.Convert_int_int_int_bool_bool += Convert;
    }

    static void Convert(On_WorldGen.orig_Convert_int_int_int_bool_bool orig, int i2, int j2, int conversionType, bool tiles, bool walls)
    {
        Tile tile = Main.tile[i2, j2];
        int type = tile.type, wall = tile.wall;
        if (!tile.active() && wall <= 0)
        {
            orig(i2, j2, conversionType, tiles, walls);
            return;
        }

        using var counter = new Depth();
        walls = walls && wall > 0 && WallLoader.Convert(i2, j2, conversionType);
        tiles = tiles && tile.active() && TileLoader.Convert(i2, j2, conversionType);
        if (tiles || walls)
            orig(i2, j2, conversionType, tiles, walls);
        if (depth == 1 && type != tile.type)
            TileLoader.OnTileConverted(i2, j2, type, tile.type, conversionType);
    }
}
