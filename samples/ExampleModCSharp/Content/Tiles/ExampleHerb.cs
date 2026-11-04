using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public enum PlantStage : byte
{
    Planted,
    Growing,
    Grown
}

public class ExampleHerb : ModTile
{
    private const int FrameWidth = 18;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileObsidianKill[Type] = true;
        Main.tileCut[Type] = true;
        Main.tileNoFail[Type] = true;
        TileID.Sets.ReplaceTileBreakUp[Type] = true;
        TileID.Sets.IgnoredInHouseScore[Type] = true;

        LocalizedText name = CreateMapEntryName();
        AddMapEntry(new Color(128, 128, 128), name);

        TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch);
        TileObjectData.newTile.AnchorValidTiles = [
            TileID.Grass,
            TileID.HallowedGrass,
            ModContent.TileType<ExampleBlock>()
        ];
        TileObjectData.newTile.AnchorAlternateTiles = [
            TileID.ClayPot,
            TileID.PlanterBox
        ];
        TileObjectData.addTile(Type);

        HitSound = SoundID.Grass;
        DustType = DustID.Ambient_DarkBrown;
    }

    public override bool CanPlace(int i, int j)
    {
        Tile tile = Framing.GetTileSafely(i, j);

        if (tile.HasTile)
        {
            int tileType = tile.TileType;
            if (tileType == Type)
            {
                PlantStage stage = GetStage(i, j);

                return stage == PlantStage.Grown;
            }
            else
            {
                if (Main.tileCut[tileType] || TileID.Sets.BreakableWhenPlacing[tileType] || tileType == TileID.WaterDrip || tileType == TileID.LavaDrip || tileType == TileID.HoneyDrip || tileType == TileID.SandDrip)
                {
                    bool foliageGrass = tileType == TileID.Plants || tileType == TileID.Plants2;
                    bool moddedFoliage = tileType >= TileID.Count && (Main.tileCut[tileType] || TileID.Sets.BreakableWhenPlacing[tileType]);
                    bool harvestableVanillaHerb = Main.tileAlch[tileType] && WorldGen.IsHarvestableHerbWithSeed(tileType, tile.TileFrameX / 18, j);

                    if (foliageGrass || moddedFoliage || harvestableVanillaHerb)
                    {
                        WorldGen.KillTile(i, j);
                        if (!tile.HasTile && Main.netMode == NetmodeID.MultiplayerClient)
                        {
                            NetMessage.SendData(MessageID.TileManipulation, number: 0, number2: i, number3: j);
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        return true;
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 0)
        {
            spriteEffects = SpriteEffects.FlipHorizontally;
        }
    }

    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
    {
        offsetY = -2;
    }

    public override bool CanDrop(int i, int j)
    {
        PlantStage stage = GetStage(i, j);

        if (stage == PlantStage.Planted)
        {
            return false;
        }
        return true;
    }

    public override IEnumerable<Item> GetItemDrops(int i, int j)
    {
        PlantStage stage = GetStage(i, j);

        Vector2 worldPosition = new Vector2(i, j).ToWorldCoordinates();
        Player nearestPlayer = Main.player[Player.FindClosest(worldPosition, 16, 16)];

        int herbItemType = ModContent.ItemType<ExampleItem>();
        int herbItemStack = 1;

        int seedItemType = ModContent.ItemType<ExampleHerbSeeds>();
        int seedItemStack = 1;

        if (nearestPlayer.active && (nearestPlayer.HeldItem.type == ItemID.StaffofRegrowth || nearestPlayer.HeldItem.type == ItemID.AcornAxe))
        {
            herbItemStack = Main.rand.Next(1, 3);
            seedItemStack = Main.rand.Next(1, 6);
        }
        else if (stage == PlantStage.Grown)
        {
            herbItemStack = 1;
            seedItemStack = Main.rand.Next(1, 4);
        }

        if (herbItemType > 0 && herbItemStack > 0)
        {
            yield return new Item(herbItemType, herbItemStack);
        }

        if (seedItemType > 0 && seedItemStack > 0)
        {
            yield return new Item(seedItemType, seedItemStack);
        }
    }

    public override bool IsTileSpelunkable(int i, int j)
    {
        PlantStage stage = GetStage(i, j);

        return stage == PlantStage.Grown;
    }

    public override void RandomUpdate(int i, int j, bool underground)
    {
        Tile tile = Framing.GetTileSafely(i, j);
        PlantStage stage = GetStage(i, j);

        if (stage != PlantStage.Grown)
        {
            tile.TileFrameX += FrameWidth;

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
        }
    }

    private static PlantStage GetStage(int i, int j)
    {
        Tile tile = Framing.GetTileSafely(i, j);
        return (PlantStage)(tile.TileFrameX / FrameWidth);
    }
}
