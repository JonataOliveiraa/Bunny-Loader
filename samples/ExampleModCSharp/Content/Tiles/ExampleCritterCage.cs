using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles
{
    public class ExampleCritterCage : ModTile
    {
        public override void SetStaticDefaults() {
            TileID.Sets.CritterCageLidStyle[Type] = TileID.Sets.CritterCageLidStyle[TileID.FrogCage];
            Main.tileFrameImportant[Type] = Main.tileFrameImportant[TileID.FrogCage];
            Main.tileLavaDeath[Type] = Main.tileLavaDeath[TileID.FrogCage];
            Main.tileSolidTop[Type] = Main.tileSolidTop[TileID.FrogCage];
            Main.tileTable[Type] = Main.tileTable[TileID.FrogCage];
            AdjTiles = [TileID.FrogCage, TileID.GoldFrogCage];
            AnimationFrameHeight = 36;

            TileObjectData.newTile.CopyFrom(TileObjectData.GetTileData(TileID.FrogCage, 0));

            TileObjectData.addTile(Type);

            AddMapEntry(new Color(122, 217, 232), ModContent.GetInstance<ExampleCritterCageItem>().DisplayName);
        }

        public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY) {
            offsetY = 2;
            Main.critterCage = true;
        }

        public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset) {
            Tile tile = Main.tile[i, j];

            int tileCageFrameIndex = Main.instance.TilesRenderer.GetSmallAnimalCageFrame(i, j, tile.TileFrameX, tile.TileFrameY);
            frameYOffset = Main.frogCageFrame[tileCageFrameIndex] * AnimationFrameHeight;
        }
    }

    public class ExampleCritterCageItem : ModItem
    {
        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.FrogCage);
            Item.createTile = ModContent.TileType<ExampleCritterCage>();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Terrarium)
                .AddIngredient(ModContent.ItemType<NPCs.ExampleCritterItem>())
                .Register();
        }
    }
}
