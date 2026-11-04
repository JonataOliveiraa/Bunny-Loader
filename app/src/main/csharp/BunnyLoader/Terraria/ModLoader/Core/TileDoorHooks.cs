using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe partial class TileHooks
{
    internal static void InstallDoors()
    {
        Once("Click", () => On_Player.TileInteractionsUse += Click);
        Once("OpenDoor", () => On_WorldGen.OpenDoor += OpenDoor);
        Once("CloseDoor", () => On_WorldGen.CloseDoor += CloseDoor);
        Once("DoorHandler", () => On_DoorOpeningHelper.TryGetHandler += DoorHandler);
    }

    static bool ToggleDoor(Player player, int i, int j, int type)
    {
        if (TileID.Sets.OpenDoorID[type] >= 0)
        {
            WorldGen.OpenDoor(i, j, player.direction);
            if (Main.tile[i, j].type != type)
            {
                NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, default, 0, i, j, player.direction);
                return true;
            }

            WorldGen.OpenDoor(i, j, -player.direction);
            if (Main.tile[i, j].type != type)
                NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, default, 0, i, j, -player.direction);
            return true;
        }

        if (TileID.Sets.CloseDoorID[type] < 0)
            return false;
        if (WorldGen.CloseDoor(i, j))
            NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, default, 1, i, j, player.direction);
        return true;
    }

    static bool DoorHandler(On_DoorOpeningHelper.orig_TryGetHandler orig, DoorOpeningHelper self, Point tileCoords, out DoorOpeningHelper.DoorAutoHandler infoProvider)
    {
        Tile tile = Main.tile[tileCoords.X, tileCoords.Y];
        int type = tile.type;
        if (TileLoader.GetTile(type) == null || TileID.Sets.OpenDoorID[type] < 0)
            return orig(self, tileCoords, out infoProvider);
        tile.type = TileID.ClosedDoor;
        try
        {
            return orig(self, tileCoords, out infoProvider);
        }
        finally
        {
            tile.type = (ushort)type;
        }
    }

    static bool OpenDoor(On_WorldGen.orig_OpenDoor orig, int i, int j, int direction)
    {
        Tile door = Main.tile[i, j];
        int closed = door.type;
        if (TileLoader.GetTile(closed) == null)
            return orig(i, j, direction);
        int open = TileID.Sets.OpenDoorID[closed];
        if (open < 0)
            return false;

        short frameX = 0;
        int frameY = door.frameY, style = 0;
        while (frameY >= 54)
        {
            frameY -= 54;
            style++;
        }

        if (door.frameX >= 54)
        {
            int column = door.frameX / 54;
            style += 36 * column;
            frameX += (short)(72 * column);
        }

        int top = j - frameY / 18;
        if (!WorldGen.ValidateTileSquareIsActiveAndOfType(i, top, 1, 3, closed))
            return false;
        TileColorCache[] colors = new TileColorCache[3];
        for (int k = 0; k < 3; k++)
            colors[k] = Main.tile[i, top + k].BlockColorAndCoating();

        int x, swing;
        if (direction == -1)
        {
            x = i - 1;
            frameX += 36;
            swing = i - 1;
        }
        else
        {
            x = i;
            swing = i + 1;
        }

        for (int y = top; y < top + 3; y++)
        {
            Tile blocker = Framing.GetTileSafely(swing, y);
            if (blocker.active() && !Main.tileCut[blocker.type] && blocker.type != TileID.Rope && !TileID.Sets.IsADripTile[blocker.type])
                return false;
        }

        for (int y = top; y < top + 3; y++)
        {
            Tile blocker = Main.tile[swing, y];
            if (blocker.active() && (Main.tileCut[blocker.type] || blocker.type == TileID.Rope || TileID.Sets.IsADripTile[blocker.type]))
                WorldGen.KillTile(swing, y);
        }

        if ((Main.netMode & NetmodeID.MultiplayerClient) == 0 && Wiring.running)
        {
            for (int k = 0; k < 3; k++)
            {
                Wiring.SkipWire(x, top + k);
                Wiring.SkipWire(x + 1, top + k);
            }
        }

        int row = style % 36 * 54;
        SoundEngine.PlaySound(SoundID.DoorOpen, i * 16, j * 16);
        for (int k = 0; k < 3; k++)
        {
            for (int side = 0; side < 2; side++)
            {
                Tile cell = Main.tile[x + side, top + k];
                cell.active(true);
                cell.type = (ushort)open;
                cell.frameY = (short)(row + 18 * k);
                cell.frameX = (short)(frameX + 18 * side);
                cell.UseBlockColors(colors[k]);
            }
        }

        for (int a = x - 1; a <= x + 2; a++)
        {
            for (int b = top - 1; b <= top + 2; b++)
                WorldGen.TileFrame(a, b);
        }

        return true;
    }

    static bool CloseDoor(On_WorldGen.orig_CloseDoor orig, int i, int j, bool forced)
    {
        Tile door = Main.tile[i, j];
        int open = door.type;
        if (TileLoader.GetTile(open) == null)
            return orig(i, j, forced);
        int closed = TileID.Sets.CloseDoorID[open];
        if (closed < 0)
            return false;

        int frameX = door.frameX, frameY = door.frameY, column = 0;
        while (frameY >= 54)
            frameY -= 54;
        if (frameX >= 72)
            column = 54 * (frameX / 72);
        int top = j - frameY / 18, hinge = i, side = 0;
        switch (frameX % 72)
        {
            case 0:
                side = 1;
                break;
            case 18:
                hinge = i - 1;
                side = 1;
                break;
            case 36:
                hinge = i + 1;
                side = -1;
                break;
            case 54:
                side = -1;
                break;
        }

        int left = side == -1 ? hinge - 1 : hinge;
        if (!forced)
        {
            for (int y = top; y < top + 3; y++)
            {
                if (!Collision.EmptyTile(hinge, y, true))
                    return false;
            }
        }

        if (!WorldGen.ValidateTileSquareIsActiveAndOfType(left, top, 2, 3, open))
            return false;
        for (int x = left; x < left + 2; x++)
        {
            for (int y = top; y < top + 3; y++)
            {
                Tile cell = Main.tile[x, y];
                if (!cell.active() || cell.type != open)
                    continue;
                if (x == hinge)
                {
                    cell.type = (ushort)closed;
                    cell.frameX = (short)(WorldGen.genRand.Next(3) * 18 + column);
                }
                else
                    cell.active(false);
            }
        }

        if ((Main.netMode & NetmodeID.MultiplayerClient) == 0 && Wiring.running)
        {
            for (int k = 0; k < 3; k++)
                Wiring.SkipWire(hinge, top + k);
        }

        for (int a = hinge - 1; a <= hinge + 1; a++)
        {
            for (int b = top - 1; b <= top + 2; b++)
                WorldGen.TileFrame(a, b);
        }

        SoundEngine.PlaySound(SoundID.DoorClosed, i * 16, j * 16);
        return true;
    }
}
