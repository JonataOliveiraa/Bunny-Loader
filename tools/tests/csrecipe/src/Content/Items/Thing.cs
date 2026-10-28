using Terraria;
using Terraria.ModLoader;

namespace CsRecipe.Content.Items;

public class Thing : ModItem
{
    public static int Created;

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 99;
        Item.value = 100;
    }

    public override void OnCreated(ItemCreationContext context)
    {
        if (context is RecipeItemCreationContext) Created++;
    }
}
