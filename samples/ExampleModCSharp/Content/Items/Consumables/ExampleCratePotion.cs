using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables;

public class ExampleCratePotion : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 20;
    }

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 26;
        Item.useStyle = ItemUseStyleID.DrinkLiquid;
        Item.useAnimation = 15;
        Item.useTime = 15;
        Item.useTurn = true;
        Item.UseSound = SoundID.Item3;
        Item.maxStack = Item.CommonMaxStack;
        Item.consumable = true;
        Item.rare = ItemRarityID.Green;
        Item.value = Item.buyPrice(silver: 8);
        Item.buffType = ModContent.BuffType<Buffs.ExampleCrateBuff>();
        Item.buffTime = 3 * 60 * 60;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.CratePotion, 4)
            .AddTile(TileID.CrystalBall)
            .Register();
    }
}
