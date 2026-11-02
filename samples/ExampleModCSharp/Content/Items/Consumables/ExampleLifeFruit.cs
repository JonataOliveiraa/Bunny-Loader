using ExampleMod.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables;

internal class ExampleLifeFruit : ModItem
{
    public static readonly int MaxExampleLifeFruits = 10;
    public static readonly int LifePerFruit = 10;

    public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LifePerFruit, MaxExampleLifeFruits);

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 10;
    }

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.LifeFruit);
    }

    public override bool CanUseItem(Player player)
    {
        return player.ConsumedLifeCrystals == Player.LifeCrystalMax && player.ConsumedLifeFruit == Player.LifeFruitMax;
    }

    public override bool? UseItem(Player player)
    {
        if (player.GetModPlayer<ExampleStatIncreasePlayer>().exampleLifeFruits >= MaxExampleLifeFruits)
        {
            return null;
        }

        player.UseHealthMaxIncreasingItem(LifePerFruit);

        player.GetModPlayer<ExampleStatIncreasePlayer>().exampleLifeFruits++;

        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
