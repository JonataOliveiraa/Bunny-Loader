using System;
using Terraria.Graphics.Light;

namespace Terraria.ModLoader.Core;

internal static class TileLightHooks
{
    const uint TileFlag = 1u << 10, WallFlag = 4u;

    static int[] tileCells = new int[12288], wallCells = new int[12288];

    static bool tiles, walls, installed;

    internal static void WantTiles()
    {
        tiles = true;
        Install();
    }

    internal static void WantWalls()
    {
        walls = true;
        Install();
    }

    static void Install()
    {
        if (installed)
            return;
        installed = true;
        On_Lighting.LightTiles += Light;
    }

    static void Light(On_Lighting.orig_LightTiles orig, int firstX, int lastX, int firstY, int lastY)
    {
        orig(firstX, lastX, firstY, lastY);
        if (Main.gameMenu)
            return;
        int padding = 28, extra = 1;
        if (Lighting.UsingNewLighting)
        {
            LightingEngine engine = Lighting.NewEngine;
            if (!engine.IsNull && engine._state != LightingEngine.EngineState.ExportMetrics)
                return;
            padding = LightingEngine.AREA_PADDING;
            extra = 0;
        }

        int x = firstX - padding, y = firstY - padding, width = lastX - firstX + padding * 2 + extra, height = lastY - firstY + padding * 2 + extra;
        if (tiles)
            Tiles(x, y, width, height);
        if (walls)
            Walls(x, y, width, height);
    }

    static void Tiles(int x, int y, int width, int height)
    {
        int count = TileHooks.Scan(ref tileCells, x, y, width, height, TileFlag);
        if (count == 0)
            return;
        GlobalTile[] globals = TileLoader.Hooks.ModifyLight;
        var lighted = Main.tileLighted;
        int known = lighted.IsNull ? 0 : lighted.Length;
        for (int k = 0; k < count; k++)
        {
            int i = tileCells[k * 3], j = tileCells[k * 3 + 1], type = tileCells[k * 3 + 2];
            if (type >= known || !lighted[type])
                continue;
            float r = 0, g = 0, b = 0;
            try
            {
                TileLoader.GetTile(type)?.ModifyLight(i, j, ref r, ref g, ref b);
                foreach (GlobalTile global in globals)
                    global.ModifyLight(i, j, type, ref r, ref g, ref b);
            }
            catch (Exception e)
            {
                ItemHooks.Report("TileLoader.ModifyLight", e);
            }

            if (r != 0 || g != 0 || b != 0)
                Lighting.AddLight(i, j, r, g, b);
        }
    }

    static void Walls(int x, int y, int width, int height)
    {
        int count = WallHooks.Scan(ref wallCells, x, y, width, height, WallFlag);
        if (count == 0)
            return;
        GlobalWall[] globals = WallHooks.LightGlobals;
        for (int k = 0; k < count; k++)
        {
            int i = wallCells[k * 3], j = wallCells[k * 3 + 1], type = wallCells[k * 3 + 2];
            float r = 0, g = 0, b = 0;
            if (WallLoader.GetWall(type) is ModWall wall)
            {
                try { wall.ModifyLight(i, j, ref r, ref g, ref b); }
                catch (Exception e) { ItemHooks.Report($"{wall.FullName}.ModifyLight", e); }
            }
            foreach (GlobalWall global in globals)
            {
                try { global.ModifyLight(i, j, type, ref r, ref g, ref b); }
                catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyLight", e); }
            }
            if (r != 0 || g != 0 || b != 0)
                Lighting.AddLight(i, j, r, g, b);
        }
    }
}
