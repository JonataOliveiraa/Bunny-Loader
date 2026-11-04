using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles;

public class ExampleTrap : ModTile
{
    public override void SetStaticDefaults()
    {
        TileID.Sets.DrawsWalls[Type] = true;
        TileID.Sets.DontDrawTileSliced[Type] = true;
        TileID.Sets.IgnoresNearbyHalfbricksWhenDrawn[Type] = true;
        TileID.Sets.Wiring.IsAMechanism[Type] = true;

        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileFrameImportant[Type] = true;

        AddMapEntry(new Color(21, 179, 192), Language.GetText("MapObject.Trap"));
        AddMapEntry(new Color(0, 141, 63), Language.GetText("MapObject.Trap"));
    }

    public override ushort GetMapOption(int i, int j) => (ushort)(Main.tile[i, j].TileFrameY / 18);

    public override bool IsTileDangerous(int i, int j, Player player) => true;

    public override IEnumerable<Item> GetItemDrops(int i, int j)
    {
        Tile t = Main.tile[i, j];
        int style = t.TileFrameY / 18;

        yield return new Item(Mod.Find<ModItem>(Items.Placeable.ExampleTrap.GetInternalNameFromStyle(style)).Type);
    }

    public override bool CreateDust(int i, int j, ref int type)
    {
        int style = Main.tile[i, j].TileFrameY / 18;
        if (style == 0)
        {
            type = DustID.Glass;
        }
        if (style == 1)
        {
            type = DustID.JungleGrass;
        }
        return true;
    }

    public override void PlaceInWorld(int i, int j, Item item)
    {
        int style = Main.LocalPlayer.HeldItem.placeStyle;
        Tile tile = Main.tile[i, j];
        tile.TileFrameY = (short)(style * 18);
        if (Main.LocalPlayer.direction == 1)
        {
            tile.TileFrameX += 18;
        }
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendTileSquare(-1, Player.tileTargetX, Player.tileTargetY, 1, TileChangeType.None);
        }
    }

    private static int[] frameXCycle = [2, 3, 4, 5, 1, 0];

    public override bool Slope(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int nextFrameX = frameXCycle[tile.TileFrameX / 18];
        tile.TileFrameX = (short)(nextFrameX * 18);
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendTileSquare(-1, Player.tileTargetX, Player.tileTargetY, 1, TileChangeType.None);
        }
        return false;
    }

    public override void HitWire(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int style = tile.TileFrameY / 18;
        Vector2 spawnPosition;

        int horizontalDirection = (tile.TileFrameX == 0) ? -1 : ((tile.TileFrameX == 18) ? 1 : 0);
        int verticalDirection = (tile.TileFrameX < 36) ? 0 : ((tile.TileFrameX < 72) ? -1 : 1);

        if (style == 0)
        {
            if (Wiring.CheckMech(i, j, 60))
            {
                spawnPosition = new Vector2(i * 16 + 8 + 0 * horizontalDirection, j * 16 + 9 + 0 * verticalDirection);

                Projectile.NewProjectile(Wiring.GetProjectileSource(i, j), spawnPosition, new Vector2(horizontalDirection, verticalDirection) * 6f, ProjectileID.IchorBullet, 20, 2f, Main.myPlayer);
            }
        }
        else if (style == 1)
        {
            if (Wiring.CheckMech(i, j, 200))
            {
                spawnPosition = new Vector2(i * 16 + 8 + 0 * horizontalDirection, j * 16 + 9 + 0 * verticalDirection);
                Projectile.NewProjectile(Wiring.GetProjectileSource(i, j), spawnPosition, new Vector2(horizontalDirection, verticalDirection) * 8f, ProjectileID.ChlorophyteBullet, 40, 2f, Main.myPlayer);
            }
        }
    }
}
