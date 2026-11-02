using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables;

public class ExampleBuffPotion : ModItem
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
        Item.rare = ItemRarityID.Orange;
        Item.value = Item.buyPrice(gold: 1);
        Item.buffType = ModContent.BuffType<Buffs.ExampleDefenseBuff>();
        Item.buffTime = 5400;
    }
}
