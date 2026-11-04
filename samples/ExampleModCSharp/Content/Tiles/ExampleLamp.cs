using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

internal class ExampleLamp : ModTile
{
    private Asset<Texture2D> flameTexture;

    public override void SetStaticDefaults()
    {
        Main.tileLighted[Type] = true;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileWaterDeath[Type] = true;
        Main.tileLavaDeath[Type] = true;

        TileID.Sets.Wiring.IsAMechanism[Type] = true;
        TileID.Sets.Wiring.IgnoreWhenValidatingTraps[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1xX);
        TileObjectData.newTile.DrawFlipHorizontal = true;
        TileObjectData.newTile.StyleLineSkip = 2;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.WaterDeath = true;
        TileObjectData.newTile.WaterPlacement = LiquidPlacement.NotAllowed;
        TileObjectData.newTile.LavaPlacement = LiquidPlacement.NotAllowed;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(253, 221, 3), Language.GetText("MapObject.FloorLamp"));

        flameTexture = ModContent.Request<Texture2D>(Texture + "_Flame");
    }

    public override void HitWire(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int topY = j - tile.TileFrameY / 18 % 3;
        short frameAdjustment = (short)(tile.TileFrameX > 0 ? -18 : 18);

        Main.tile[i, topY].TileFrameX += frameAdjustment;
        Main.tile[i, topY + 1].TileFrameX += frameAdjustment;
        Main.tile[i, topY + 2].TileFrameX += frameAdjustment;

        Wiring.SkipWire(i, topY);
        Wiring.SkipWire(i, topY + 1);
        Wiring.SkipWire(i, topY + 2);

        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            NetMessage.SendTileSquare(-1, i, topY + 1, 3, TileChangeType.None);
        }
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 0)
        {
            spriteEffects = SpriteEffects.FlipHorizontally;
        }
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Tile tile = Main.tile[i, j];
        if (tile.TileFrameX == 0)
        {
            r = 1f;
            g = 0.75f;
            b = 1f;
        }
    }

    public override void EmitParticles(int i, int j, Tile tileCache, short tileFrameX, short tileFrameY, Color tileLight, bool visible)
    {
        if (!visible)
        {
            return;
        }

        Tile tile = Main.tile[i, j];

        short frameX = tile.TileFrameX;
        short frameY = tile.TileFrameY;

        if (frameX != 0 || !Main.rand.NextBool(40))
        {
            return;
        }

        int style = frameY / 54;

        if (frameY / 18 % 3 == 0)
        {
            int dustChoice = -1;

            if (style == 0)
            {
                dustChoice = 21;
            }

            if (dustChoice != -1)
            {
                var dust = Dust.NewDustDirect(new Vector2(i * 16 + 4, j * 16 + 2), 4, 4, dustChoice, 0f, 0f, 100, default, 1f);

                if (!Main.rand.NextBool(3))
                {
                    dust.noGravity = true;
                }

                dust.velocity *= 0.3f;
                dust.velocity.Y = dust.velocity.Y - 1.5f;
            }
        }
    }

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
    {
        if (drawData.tileFrameX == 0 && drawData.tileFrameY / 18 % 3 == 0)
        {
            Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
        }
    }

    public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (!TileDrawing.IsVisible(tile))
        {
            return;
        }

        SpriteEffects effects = SpriteEffects.None;

        if (i % 2 == 0)
        {
            effects = SpriteEffects.FlipHorizontally;
        }

        Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);

        int width = 16;
        int offsetY = 0;
        int height = 16;
        short frameX = tile.TileFrameX;
        short frameY = tile.TileFrameY;

        TileLoader.SetDrawPositions(i, j, ref width, ref offsetY, ref height, ref frameX, ref frameY);

        ulong randSeed = Main.TileFrameSeed ^ (ulong)((long)j << 32 | (long)(uint)i);

        for (int c = 0; c < 7; c++)
        {
            float shakeX = Utils.RandomInt(ref randSeed, -10, 11) * 0.15f;
            float shakeY = Utils.RandomInt(ref randSeed, -10, 1) * 0.35f;

            Rectangle sourceRectangle = new Rectangle(frameX, frameY, width, height);
            spriteBatch.Draw(flameTexture.Value, new Vector2(i * 16 - (int)Main.screenPosition.X - (width - 16f) / 2f + shakeX, j * 16 - (int)Main.screenPosition.Y + offsetY + shakeY) + zero, sourceRectangle, new Color(100, 100, 100, 0), 0f, default, 1f, effects, 0f);
        }
    }
}
