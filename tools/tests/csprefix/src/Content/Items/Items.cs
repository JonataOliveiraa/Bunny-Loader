using Terraria;
using Terraria.ModLoader;

namespace CsPrefix.Content.Items;

public class TestSword : ModItem
{
    public const int Forbidden = 81;

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
    }

    public override bool AllowPrefix(int pre) => pre != Forbidden;
}

public class Charm : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.accessory = true;
        Item.value = 10000;
    }
}
