using ExampleMod.Content.Items;
using ExampleMod.Content.Walls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles
{
    public class HallowedFossilTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            TileID.Sets.Hallow[Type] = true;
            TileID.Sets.HallowBiomeSight[Type] = true;
            TileID.Sets.HallowCountCollection.Add(Type);
            DustType = DustID.Pearlsand;
            AddMapEntry(new Color(157, 76, 152));
            TileID.Sets.ChecksForMerge[Type] = true;
            Main.tileMerge[TileID.HallowSandstone][Type] = true;

            TileLoader.RegisterSimpleConversion(TileID.DesertFossil, BiomeConversionID.Hallow, Type);
            TileID.Sets.Infectable[TileID.DesertFossil] = true;

            TileLoader.RegisterConversion(Type, BiomeConversionID.Sand, TileID.DesertFossil);
        }

        public override void RandomUpdate(int i, int j, bool underground) {
            WorldGen.SpreadInfectionToNearbyTile(i, j, BiomeConversionID.Hallow);
        }
    }

    public class CorruptFossilTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            TileID.Sets.Corrupt[Type] = true;
            TileID.Sets.CorruptBiomeSight[Type] = true;
            TileID.Sets.CorruptCountCollection.Add(Type);
            DustType = DustID.Corruption;
            AddMapEntry(new Color(65, 48, 99));
            TileID.Sets.ChecksForMerge[Type] = true;
            Main.tileMerge[TileID.CorruptSandstone][Type] = true;

            TileLoader.RegisterSimpleConversion(TileID.DesertFossil, BiomeConversionID.Corruption, Type);

            TileLoader.RegisterConversion(Type, BiomeConversionID.Sand, TileID.DesertFossil);
        }

        public override void RandomUpdate(int i, int j, bool underground) {
            WorldGen.SpreadInfectionToNearbyTile(i, j, BiomeConversionID.Corruption);
        }
    }

    public class CrimsonFossilTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            TileID.Sets.Crimson[Type] = true;
            TileID.Sets.CrimsonBiomeSight[Type] = true;
            TileID.Sets.CrimsonCountCollection.Add(Type);
            DustType = DustID.Crimstone;
            AddMapEntry(new Color(112, 33, 32));
            TileID.Sets.ChecksForMerge[Type] = true;
            Main.tileMerge[TileID.CrimsonSandstone][Type] = true;

            TileLoader.RegisterSimpleConversion(TileID.DesertFossil, BiomeConversionID.Crimson, Type);

            TileLoader.RegisterConversion(Type, BiomeConversionID.Sand, TileID.DesertFossil);
        }

        public override void Convert(int i, int j, int conversionType) {
            switch (conversionType) {
                case BiomeConversionID.Sand:
                    WorldGen.ConvertTile(i, j, TileID.DesertFossil);
                    return;
            }
        }

        public override void RandomUpdate(int i, int j, bool underground) {
            WorldGen.SpreadInfectionToNearbyTile(i, j, BiomeConversionID.Crimson);
        }
    }

    #region Items
    internal class HallowedFossilTileItem : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<HallowedFossilTile>());
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<HallowedFossilWallItem>(4)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    internal class CorruptFossilTileItem : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<CorruptFossilTile>());
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CorruptFossilWallItem>(4)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    internal class CrimsonFossilTileItem : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<CrimsonFossilTile>());
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CrimsonFossilWallItem>(4)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
    #endregion
}
