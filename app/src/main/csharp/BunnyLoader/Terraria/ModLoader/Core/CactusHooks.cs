using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class CactusHooks
{
    const int FruitFrame = 204, FruitFrameAlt = 202;

    static readonly List<(int X, int Y, int Type)> swapped = new();

    static bool installed;

    internal static void Setup()
    {
        foreach (var (plant, _) in PlantLoader.plantList)
        {
            if (plant is not ModCactus cactus)
                continue;
            cactus.SetStaticDefaults();
            foreach (int ground in cactus.GrowsOnTileId ?? Array.Empty<int>())
                PlantLoader.plantLookup[(TileID.Cactus, ground)] = cactus;
            Install();
        }
    }

    static void Install()
    {
        if (installed)
            return;
        installed = true;
        On_WorldGen.CheckCactus += CheckCactus;
        On_WorldGen.GrowCactus += GrowCactus;
        if (Host.Native()->InstallTileTextureHook(TileID.Cactus, TileID.DyePlants, &Texture) == 0)
            Log.Error("ModCactus: the cactus texture hook could not be installed");
    }

    internal static int GroundOf(int tileX, int tileY, int frameX, int frameY)
    {
        int x = tileX;
        if (frameX == 36)
            x--;
        if (frameX == 54)
            x++;
        if (frameX == 108)
            x += frameY == 18 ? -1 : 1;
        int y = tileY;
        bool found = false;
        if (!WorldGen.InWorld(x, y, 2))
            return 0;
        while (true)
        {
            Tile tile = Main.tile[x, y];
            if (tile.active() && tile.type == TileID.Cactus)
                found = true;
            else if (found && tile.active() && Main.tileSolid[tile.type])
                return tile.type;
            y++;
            if (y > tileY + 20 || !WorldGen.InWorld(x, y, 2))
                return 0;
        }
    }

    [UnmanagedCallersOnly]
    static IntPtr Texture(int x, int y, int type)
    {
        try
        {
            Tile tile = Main.tile[x, y];
            int frameX = tile.frameX;
            if (type == TileID.DyePlants)
            {
                if (frameX != FruitFrame && frameX != FruitFrameAlt)
                    return IntPtr.Zero;
                int below = y + 1;
                int ground = WorldGen.InWorld(x, below, 2) ? GroundOf(x, below, 0, 0) : 0;
                return PlantLoader.Get<ModCactus>(TileID.Cactus, ground) is ModCactus fruit ? fruit.GetFruitTexture().Value.Pointer : IntPtr.Zero;
            }
            int sand = GroundOf(x, y, frameX, tile.frameY);
            return sand > 0 && PlantLoader.Get<ModCactus>(TileID.Cactus, sand) is ModCactus cactus ? cactus.GetTexture().Value.Pointer : IntPtr.Zero;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModCactus texture", e);
            return IntPtr.Zero;
        }
    }

    static void Swap(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 1))
            return;
        Tile tile = Main.tile[x, y];
        if (!tile.active() || !TileLoader.CanGrowModCactus(tile.type))
            return;
        swapped.Add((x, y, tile.type));
        tile.type = TileID.Sand;
    }

    static void SwapBelow(int x, int y)
    {
        for (int k = y; k < y + 24 && WorldGen.InWorld(x, k, 1); k++)
        {
            Tile tile = Main.tile[x, k];
            if (tile.active() && tile.type == TileID.Cactus)
                continue;
            Swap(x, k);
            return;
        }
    }

    static void Restore(int mark)
    {
        for (int k = swapped.Count - 1; k >= mark; k--)
        {
            var entry = swapped[k];
            Tile tile = Main.tile[entry.X, entry.Y];
            tile.type = (ushort)entry.Type;
        }
        swapped.RemoveRange(mark, swapped.Count - mark);
    }

    static bool CheckCactus(On_WorldGen.orig_CheckCactus orig, int i, int j)
    {
        int mark = swapped.Count;
        SwapBelow(i, j);
        SwapBelow(i - 1, j);
        SwapBelow(i + 1, j);
        try
        {
            return orig(i, j);
        }
        finally
        {
            Restore(mark);
        }
    }

    static void GrowCactus(On_WorldGen.orig_GrowCactus orig, int i, int j)
    {
        int mark = swapped.Count;
        for (int x = i - 6; x <= i + 6; x++)
            for (int y = j - 3; y <= j + 1; y++)
                Swap(x, y);
        SwapBelow(i, j);
        try
        {
            orig(i, j);
        }
        finally
        {
            Restore(mark);
        }
    }
}
