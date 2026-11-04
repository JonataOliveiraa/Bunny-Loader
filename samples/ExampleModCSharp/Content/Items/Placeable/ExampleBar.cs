using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleBar : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 25;

        ItemID.Sets.SortingPriorityMaterials[Type] = 59;

        ItemTrader.ChlorophyteExtractinator.AddOption_OneWay(Type, 5, ItemID.ChlorophyteBar, 2);
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExampleBar>());
        Item.width = 20;
        Item.height = 20;
        Item.value = 750;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleOre>(4)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}
