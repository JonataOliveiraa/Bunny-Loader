using BunnyLoader;
using Terraria;
using Terraria.ModLoader;

namespace CsDamageClass.Content.Items;

public sealed class TestBlade : ModItem
{
    public override string? DisplayName => "Test Blade";

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
