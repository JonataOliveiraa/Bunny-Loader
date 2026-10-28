using Terraria;
using Terraria.ModLoader;

namespace CsHeal.Content.Items;

public class CalmPotion : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
        Item.useStyle = 9;
        Item.consumable = true;
        Item.potion = true;
        Item.healLife = 10;
    }

    public override bool ApplyPotionDelay(Player player, int potionDelay) => false;
}
