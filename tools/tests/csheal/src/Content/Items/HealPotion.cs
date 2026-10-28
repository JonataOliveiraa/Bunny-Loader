using Terraria;
using Terraria.ModLoader;

namespace CsHeal.Content.Items;

public class HealPotion : ModItem
{
    public static bool? SawQuick;

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
        Item.useStyle = 9;
        Item.useTime = 17;
        Item.useAnimation = 17;
        Item.consumable = true;
        Item.maxStack = 30;
        Item.potion = true;
        Item.healLife = 50;
    }

    public override void GetHealLife(Player player, bool quickHeal, ref int healValue)
    {
        SawQuick = quickHeal;
        healValue += 25;
    }

    public override void ModifyPotionDelay(Player player, ref int baseDelay) => baseDelay = 600;
}
