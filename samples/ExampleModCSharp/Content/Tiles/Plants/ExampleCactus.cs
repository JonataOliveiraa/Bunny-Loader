using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles
{
    public class ExampleCactus : ModCactus
    {
        private Asset<Texture2D> texture;
        private Asset<Texture2D> fruitTexture;

        public override void SetStaticDefaults() {
            GrowsOnTileId = [ModContent.TileType<ExampleSand>()];
            texture = ModContent.Request<Texture2D>("ExampleMod/Content/Tiles/Plants/ExampleCactus");
            fruitTexture = ModContent.Request<Texture2D>("ExampleMod/Content/Tiles/Plants/ExampleCactus_Fruit");
        }

        public override Asset<Texture2D> GetTexture() => texture;

        public override Asset<Texture2D> GetFruitTexture() => fruitTexture;
    }
}
