using CsDamage.Content.DamageClasses;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDamage.Content.Items;

public class TestClassSword : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<TestDamageClass>();
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.damage = 70;
        Item.knockBack = 4;
        Item.crit = 6;
    }
}
