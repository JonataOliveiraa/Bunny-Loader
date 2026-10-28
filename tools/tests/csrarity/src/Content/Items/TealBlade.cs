using CsRarity.Content.Rarities;
using Terraria;
using Terraria.ModLoader;

namespace CsRarity.Content.Items;

public class TealBlade : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.damage = 20;
        Item.melee = true;
        Item.useStyle = 1;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.knockBack = 5;
        Item.value = 10000;
        Item.rare = ModContent.RarityType<Teal>();
    }
}
