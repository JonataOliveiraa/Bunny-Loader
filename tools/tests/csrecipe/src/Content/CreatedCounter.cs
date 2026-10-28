using Terraria;
using Terraria.ModLoader;

namespace CsRecipe.Content;

public class CreatedCounter : GlobalItem
{
    public static int Created;

    public override void OnCreated(Item item, ItemCreationContext context)
    {
        if (context is RecipeItemCreationContext { Recipe.RecipeIndex: >= 0 }) Created++;
    }
}
