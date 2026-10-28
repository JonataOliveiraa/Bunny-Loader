using Terraria;
using Terraria.ModLoader;

namespace CsItems.Content.Items;

// O ExampleSword do ExampleMod, com nome e tooltip proprios.
public class CsSword : ModItem
{
    public override string? DisplayName => "Espada C#";

    public override string? Tooltip => "Feita pelo loader C#\nSem QuickJS";

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = 1;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.autoReuse = true;
        Item.melee = true;
        Item.damage = 50;
        Item.knockBack = 6;
        Item.crit = 6;
        Item.value = Item.buyPrice(gold: 1);
        Item.rare = 2;
    }
}
