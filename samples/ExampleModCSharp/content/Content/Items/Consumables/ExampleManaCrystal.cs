using ExampleMod.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables;

internal class ExampleManaCrystal : ModItem
{
    public static readonly int MaxExampleManaCrystals = 10;
    public static readonly int ManaPerCrystal = 10;

    public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(ManaPerCrystal, MaxExampleManaCrystals);

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 10;
    }

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.ManaCrystal);
    }

    public override bool CanUseItem(Player player)
    {
        return player.ConsumedManaCrystals == Player.ManaCrystalMax;
    }

    public override bool? UseItem(Player player)
    {
        if (player.GetModPlayer<ExampleStatIncreasePlayer>().exampleManaCrystals >= MaxExampleManaCrystals)
        {
            return null;
        }

        player.UseManaMaxIncreasingItem(ManaPerCrystal);

        player.GetModPlayer<ExampleStatIncreasePlayer>().exampleManaCrystals++;

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
