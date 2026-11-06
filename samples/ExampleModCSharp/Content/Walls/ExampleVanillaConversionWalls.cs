using ExampleMod.Content.Items;
using ExampleMod.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Walls
{
    public class HallowedFossilWall : ModWall
    {
        public override void SetStaticDefaults() {
            WallID.Sets.SpreadsHallow[Type] = true;
            DustType = DustID.Pearlsand;
            AddMapEntry(new Color(157, 76, 152));

            WallLoader.RegisterSimpleConversion(WallID.DesertFossilEcho, BiomeConversionID.Hallow, Type);

            WallLoader.RegisterConversion(Type, BiomeConversionID.Sand, WallID.DesertFossilEcho);
        }
    }

    public class CorruptFossilWall : ModWall
    {
        public override void SetStaticDefaults() {
            WallID.Sets.SpreadsCorruption[Type] = true;
            DustType = DustID.Corruption;
            AddMapEntry(new Color(65, 48, 99));

            WallLoader.RegisterSimpleConversion(WallID.DesertFossilEcho, BiomeConversionID.Corruption, Type);
            WallLoader.RegisterConversion(Type, BiomeConversionID.Sand, WallID.DesertFossilEcho);
        }
    }

    public class CrimsonFossilWall : ModWall
    {
        public override void SetStaticDefaults() {
            WallID.Sets.SpreadsCrimson[Type] = true;
            DustType = DustID.Crimstone;
            AddMapEntry(new Color(112, 33, 32));

            WallLoader.RegisterSimpleConversion(WallID.DesertFossilEcho, BiomeConversionID.Crimson, Type);
            WallLoader.RegisterConversion(Type, BiomeConversionID.Sand, WallID.DesertFossilEcho);
        }
    }

    #region Items
    internal class HallowedFossilWallItem : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToPlaceableWall(ModContent.WallType<HallowedFossilWall>());
        }

        public override void AddRecipes() {
            CreateRecipe(4)
                .AddIngredient<HallowedFossilTileItem>()
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    internal class CorruptFossilWallItem : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToPlaceableWall(ModContent.WallType<CorruptFossilWall>());
        }

        public override void AddRecipes() {
            CreateRecipe(4)
                .AddIngredient<CorruptFossilTileItem>()
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    internal class CrimsonFossilWallItem : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToPlaceableWall(ModContent.WallType<CrimsonFossilWall>());
        }

        public override void AddRecipes() {
            CreateRecipe(4)
                .AddIngredient<CrimsonFossilTileItem>()
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
    #endregion
}
