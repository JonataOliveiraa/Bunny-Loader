using CsDamageClass.Content.DamageClasses;
using Terraria.ModLoader;

namespace CsDamageClass.Content.Items;

public sealed class TestBlade : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<TestClass>();
        Item.width = 20;
        Item.height = 20;
        Item.useStyle = 1;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.damage = 100;
        Item.knockBack = 4;
        Item.crit = 6;
    }
}
