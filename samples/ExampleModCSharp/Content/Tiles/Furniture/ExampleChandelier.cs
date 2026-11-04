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

namespace ExampleMod.Content.Tiles.Furniture;

public class ExampleChandelier : ModTile
{
    private Asset<Texture2D> flameTexture;
    private Asset<Texture2D> glowTexture;
    private int turningOnAnimationType;

    public enum StyleID
    {
        Copper,
        Silver,
        Frozen,
        PalmWood,
        BorealWood,
        Flesh
    }

    public override void Load()
    {
        flameTexture = ModContent.Request<Texture2D>(Texture + "_Flame");
        glowTexture = ModContent.Request<Texture2D>(Texture + "_Glow");
    }

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileLavaDeath[Type] = true;
        Main.tileLighted[Type] = true;

        TileID.Sets.MultiTileSway[Type] = true;
        TileID.Sets.Wiring.IsAMechanism[Type] = true;
        TileID.Sets.Wiring.IgnoreWhenValidatingTraps[Type] = true;

        TileID.Sets.RoomNeeds.CountsAsTorch[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.Origin = new Point16(1, 0);
        TileObjectData.newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, 1, 1);
        TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
        TileObjectData.newTile.LavaDeath = true;

        TileObjectData.newTile.RandomStyleRange = 6;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleLineSkip = 2;
        TileObjectData.newTile.DrawYOffset = -2;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(235, 166, 135), Language.GetText("MapObject.Chandelier"));

        RegisterItemDrop(ModContent.ItemType<Items.Placeable.Furniture.ExampleChandelier>());

        turningOnAnimationType = Animation.RegisterTemporaryAnimation(frameRate: 12, frames: [0, 2, 2, 3, 2, 2, 1, 3]);
    }

    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
    {
        offsetY += 2;
        return;
    }

    public override void HitWire(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int topX = i - tile.TileFrameX % 54 / 18;
        int topY = j - tile.TileFrameY % 54 / 18;

        short frameAdjustment = (short)(tile.TileFrameY >= 54 ? -54 : 54);

        for (int x = topX; x < topX + 3; x++)
        {
            for (int y = topY; y < topY + 3; y++)
            {
                Main.tile[x, y].TileFrameY += frameAdjustment;
                Wiring.SkipWire(x, y);
            }
        }

        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            NetMessage.SendTileSquare(-1, topX, topY, 3, 3);
        }

        if (frameAdjustment == -54 && (StyleID)TileObjectData.GetTileStyle(tile) == StyleID.Frozen)
        {
            Animation.NewTemporaryAnimation(turningOnAnimationType, Type, topX, topY);
        }
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        if (Main.tile[i, j].TileFrameY / 54 != 0)
        {
            return;
        }

        StyleID style = (StyleID)TileObjectData.GetTileStyle(Main.tile[i, j]);
        switch (style)
        {
            case StyleID.Flesh:
                r = 1f;
                g = 0.6f;
                b = 0.6f;
                break;
            case StyleID.Frozen:
                r = 0.75f;
                g = 0.85f;
                b = 1f;
                break;
            case StyleID.PalmWood:
                r = 1f;
                g = 0.95f;
                b = 0.65f;
                break;
            case StyleID.BorealWood:
                r = 0f;
                g = 0.9f;
                b = 1f;
                break;
            default:
                r = 1f;
                g = 0.95f;
                b = 0.8f;
                break;
        }
    }

    public override void EmitParticles(int i, int j, Tile tileCache, short tileFrameX, short tileFrameY, Color tileLight, bool visible)
    {
        if (Main.rand.NextBool(40) && tileFrameY < 54)
        {
            StyleID style = (StyleID)(tileFrameX / 54);

            int tileColumn = tileFrameX / 18 % 3;
            if (tileFrameY / 18 % 3 == 1 && tileColumn != 1)
            {
                int dustChoice;
                switch (style)
                {
                    case StyleID.Copper:
                    case StyleID.Silver:
                        dustChoice = DustID.Torch;
                        break;
                    case StyleID.BorealWood:
                        dustChoice = DustID.BlueTorch;
                        break;
                    default:
                        dustChoice = -1;
                        break;
                }

                if (dustChoice != -1)
                {
                    Dust dust = Dust.NewDustDirect(new Vector2(i * 16, j * 16 + 2), 14, 6, dustChoice, 0f, 0f, 100);
                    if (Main.rand.NextBool(3))
                    {
                        dust.noGravity = true;
                    }

                    dust.velocity *= 0.3f;
                    dust.velocity.Y -= 1.5f;
                }
            }
        }
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (TileObjectData.IsTopLeft(tile))
        {
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.MultiTileVine);
        }

        return false;
    }

    public override void AdjustMultiTileVineParameters(int i, int j, ref float? overrideWindCycle, ref float windPushPowerX, ref float windPushPowerY, ref bool dontRotateTopTiles, ref float totalWindMultiplier, ref Texture2D glowTexture, ref Color glowColor)
    {
        StyleID style = (StyleID)TileObjectData.GetTileStyle(Main.tile[i, j]);
        if (style == StyleID.Copper)
        {
            return;
        }

        overrideWindCycle = 1f;
        windPushPowerY = 0;

        switch (style)
        {
            case StyleID.Silver:

                break;
            case StyleID.Frozen:

                totalWindMultiplier *= 0.5f;
                break;
            case StyleID.PalmWood:

                overrideWindCycle = 0f;
                break;
            case StyleID.BorealWood:

                overrideWindCycle = null;
                windPushPowerY = -1f;
                dontRotateTopTiles = true;

                glowTexture = this.glowTexture.Value;
                glowColor = Color.White;
                break;
            case StyleID.Flesh:
                overrideWindCycle = null;
                windPushPowerY = -1f;
                dontRotateTopTiles = true;
                totalWindMultiplier *= 0.3f;
                break;
        }
    }

    public override void GetTileFlameData(int i, int j, ref TileDrawing.TileFlameData tileFlameData)
    {
        ulong flameSeed = Main.TileFrameSeed ^ (ulong)(((long)i << 32) | (uint)j);

        tileFlameData.flameTexture = flameTexture.Value;
        tileFlameData.flameSeed = flameSeed;

        StyleID style = (StyleID)TileObjectData.GetTileStyle(Main.tile[i, j]);

        switch (style)
        {
            case StyleID.Flesh:
                tileFlameData.flameCount = 3;
                tileFlameData.flameColor = new Color(50, 50, 50, 0);
                tileFlameData.flameRangeXMin = -10;
                tileFlameData.flameRangeXMax = 11;
                tileFlameData.flameRangeYMin = -10;
                tileFlameData.flameRangeYMax = 11;
                tileFlameData.flameRangeMultX = 0.05f;
                tileFlameData.flameRangeMultY = 0.15f;
                break;
            case StyleID.Frozen:
                tileFlameData.flameCount = 7;
                tileFlameData.flameColor = new Color(50, 50, 50, 0);
                tileFlameData.flameRangeXMin = -10;
                tileFlameData.flameRangeXMax = 11;
                tileFlameData.flameRangeYMin = -10;
                tileFlameData.flameRangeYMax = 11;
                tileFlameData.flameRangeMultX = 0.3f;
                tileFlameData.flameRangeMultY = 0.3f;
                break;
            default:
                tileFlameData.flameCount = 7;
                tileFlameData.flameColor = new Color(100, 100, 100, 0);
                tileFlameData.flameRangeXMin = -10;
                tileFlameData.flameRangeXMax = 11;
                tileFlameData.flameRangeYMin = -10;
                tileFlameData.flameRangeYMax = 1;
                tileFlameData.flameRangeMultX = 0.15f;
                tileFlameData.flameRangeMultY = 0.35f;
                break;
        }
    }

    public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset)
    {
        Tile tile = Main.tile[i, j];
        int topX = i - tile.TileFrameX % 54 / 18;
        int topY = j - tile.TileFrameY % 54 / 18;
        if (tile.TileFrameY / 54 == 0 && Animation.GetTemporaryFrame(topX, topY, out int frameData))
        {
            frameYOffset = 54 * frameData;
        }
    }
}
