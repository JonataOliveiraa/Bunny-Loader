using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class PressurePlateHooks
{
    static readonly List<(int X, int Y)> oldCells = new(), newCells = new();

    internal static void Want() => ItemHooks.Once("PressurePlates", () => On_PressurePlateHelper.UpdatePlayerPosition += UpdatePlayerPosition);

    static bool ModPlate(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 1))
            return false;
        Tile tile = Main.tile[x, y];
        return tile.active() && tile.type >= TileID.Count && TileID.Sets.PressurePlate[tile.type] == -3;
    }

    static void Cells(List<(int X, int Y)> cells, Vector2 topLeft, int width, int height)
    {
        cells.Clear();
        int left = (int)(topLeft.X + 1) / 16, right = (int)(topLeft.X + width - 1) / 16;
        int top = (int)(topLeft.Y + 1) / 16, bottom = (int)(topLeft.Y + height - 1) / 16;
        for (int x = left; x <= right; x++)
            for (int y = top; y <= bottom; y++)
                if (ModPlate(x, y))
                    cells.Add((x, y));
    }

    static Rectangle Bounds(int x, int y) => new(x * 16, y * 16 + 16 - PressurePlateHelper.pressurePlateBounds.Height, 16, PressurePlateHelper.pressurePlateBounds.Height);

    static void UpdatePlayerPosition(On_PressurePlateHelper.orig_UpdatePlayerPosition orig, Player player)
    {
        Vector2 last = PressurePlateHelper.PlayerLastPosition[player.whoAmI];
        orig(player);
        Cells(oldCells, last, player.width, player.height);
        Cells(newCells, player.position, player.width, player.height);
        if (oldCells.Count == 0 && newCells.Count == 0)
            return;
        Rectangle now = player.Hitbox;
        now.Inflate(-1, -1);
        Rectangle before = player.Hitbox;
        before.X = (int)last.X;
        before.Y = (int)last.Y;
        before.Inflate(-1, -1);
        foreach (var (x, y) in oldCells)
            if (!now.Intersects(Bounds(x, y)) && !newCells.Contains((x, y)))
                PressurePlateHelper.MoveAwayFrom(new Point(x, y), player.whoAmI);
        foreach (var (x, y) in newCells)
            if (now.Intersects(Bounds(x, y)) && (!oldCells.Contains((x, y)) || !before.Intersects(Bounds(x, y))))
                PressurePlateHelper.MoveInto(new Point(x, y), player.whoAmI);
    }
}
