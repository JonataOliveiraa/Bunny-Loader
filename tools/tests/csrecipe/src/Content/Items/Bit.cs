using Terraria;
using Terraria.ModLoader;

namespace CsRecipe.Content.Items;

public class Bit : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 999;
        Item.value = 10;
    }
}
