using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles;

public class ExampleSlopeTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
    }

    public override bool Slope(int i, int j)
    {
        Tile tile = Framing.GetTileSafely(i, j);
        tile.TileFrameX = (short)((tile.TileFrameX + 18) % 72);
        SoundEngine.PlaySound(SoundID.MenuTick);
        return false;
    }

    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        return false;
    }

    public override bool SwitchTiles(int i, int j, Entity entity, Vector2 position, int width, int height, Vector2 oldPosition, int objType)
    {
        Player player = Main.player[entity.whoAmI];
        if (player.Pointer != entity.Pointer)
        {
            return false;
        }

        if (player.invis)
        {
            return false;
        }

        var tileCoordinates = new Rectangle(i * 16, j * 16, 16, 16);
        var oldPlayerHitbox = new Rectangle((int)oldPosition.X, (int)oldPosition.Y, width, height);
        var playerHitbox = new Rectangle((int)position.X, (int)position.Y, width, height);

        if (!tileCoordinates.Intersects(oldPlayerHitbox))
        {
            return false;
        }

        bool success = (Main.tile[i, j].TileFrameX / 18) switch
        {
            0 => playerHitbox.Left >= tileCoordinates.Right,
            1 => playerHitbox.Top >= tileCoordinates.Bottom,
            2 => playerHitbox.Right <= tileCoordinates.Left,
            3 => playerHitbox.Bottom <= tileCoordinates.Top,
            _ => false,
        };

        if (success)
        {
            Wiring.HitSwitchAndSync(i, j);
            return true;
        }

        return false;
    }

    public override void HitSwitch(int i, int j)
    {
        Wiring.TripWire(i, j, 1, 1);
        Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, DustID.GreenFairy);
        SoundEngine.PlaySound(SoundID.Unlock, new Vector2(i * 16, j * 16));
    }
}
