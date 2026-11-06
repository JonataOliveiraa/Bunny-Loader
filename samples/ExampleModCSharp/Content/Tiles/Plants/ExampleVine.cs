using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles.Plants
{
    public class ExampleVine : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileCut[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLavaDeath[Type] = true;
            Main.tileNoFail[Type] = true;

            TileID.Sets.TileCutIgnore.Regrowth[Type] = true;
            TileID.Sets.IsVine[Type] = true;
            TileID.Sets.ReplaceTileBreakDown[Type] = true;
            TileID.Sets.VineThreads[Type] = true;

            AddMapEntry(new Color(160, 160, 160));

            DustType = DustID.Silver;
            HitSound = SoundID.Grass;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Main.instance.TilesRenderer.CrawlToTopOfVineAndAddSpecialPoint(j, i);

            return false;
        }

        public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY) {
            offsetY = -2;
        }

        public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects) {
            if (i % 2 == 0) {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j) {
            if (Main.rand.NextBool(2) && WorldGen.GetPlayerForTile(i, j).cordage) {
                yield return new Item(ItemID.VineRope);
            }
        }
    }

    public class ExampleVineGlobalTile : GlobalTile
    {
        private int ExampleVine;
        private int ExampleBlock;

        public override void SetStaticDefaults() {
            ExampleVine = ModContent.TileType<ExampleVine>();
            ExampleBlock = ModContent.TileType<ExampleBlock>();
        }

        public override void RandomUpdate(int i, int j, int type, bool underground) {
            if (underground) {
                return;
            }

            Tile tile = Main.tile[i, j];
            if (!tile.HasUnactuatedTile) {
                return;
            }

            if ((tile.TileType == ExampleVine || tile.TileType == ExampleBlock) && WorldGen.GrowMoreVines(i, j)) {
                int growChance = 70;
                if (tile.TileType == ExampleVine) {
                    growChance = 7;
                }

                int below = j + 1;
                Tile tileBelow = Main.tile[i, below];
                if (WorldGen.genRand.NextBool(growChance) && !tileBelow.HasTile && tileBelow.LiquidType != LiquidID.Lava) {
                    bool vineIsHangingOffValidTile = false;
                    for (int above = j; above > j - 10; above--) {
                        Tile tileAbove = Main.tile[i, above];
                        if (tileAbove.BottomSlope) {
                            return;
                        }

                        if (tileAbove.HasTile && tileAbove.TileType == ExampleBlock && !tileAbove.BottomSlope) {
                            vineIsHangingOffValidTile = true;
                            break;
                        }
                    }

                    if (vineIsHangingOffValidTile) {
                        tileBelow.TileType = (ushort)ExampleVine;
                        tileBelow.HasTile = true;
                        tileBelow.CopyPaintAndCoating(tile);
                        WorldGen.SquareTileFrame(i, below);
                        if (Main.netMode == NetmodeID.Server) {
                            NetMessage.SendTileSquare(-1, i, below);
                        }
                    }
                }
            }
        }

        public override bool TileFrame(int i, int j, int type, ref bool resetFrame, ref bool noBreak) {
            if (!TileID.Sets.IsVine[type]) {
                return true;
            }

            Tile tile = Main.tile[i, j];
            Tile tileAbove = Main.tile[i, j - 1];

            int aboveTileType = tileAbove.HasUnactuatedTile && !tileAbove.BottomSlope ? tileAbove.TileType : -1;

            if (type != aboveTileType) {
                if ((aboveTileType == ExampleBlock || aboveTileType == ExampleVine) && type != ExampleVine) {
                    tile.TileType = (ushort)ExampleVine;
                    WorldGen.SquareTileFrame(i, j);
                    return true;
                }

                if (type == ExampleVine && aboveTileType != ExampleBlock) {
                    if (aboveTileType == -1) {
                        WorldGen.KillTile(i, j);
                    }
                    else {
                        tile.TileType = TileID.Vines;
                    }
                }
            }

            return true;
        }
    }
}
