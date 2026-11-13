using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Exceptions;

namespace RecipeLimitsTest;

public class LimitRecipes : ModSystem
{
    static readonly int[] Thirteen =
    {
        ItemID.Wood, ItemID.StoneBlock, ItemID.DirtBlock, ItemID.Gel, ItemID.Torch, ItemID.Acorn, ItemID.IronBar,
        ItemID.CopperBar, ItemID.Glass, ItemID.Rope, ItemID.Chain, ItemID.Mushroom, ItemID.Daybloom,
    };

    public static Recipe Largest, Full, Holed, Sword, Bow;

    public static bool ThrewAt15, SharedSlot, RefusedFull, RefusedNull;

    public static string ThrowMessage = "";

    public static int FirstCount, SharedAfterAdd = -1, BowAdded, CountBeforeRefusals, CountAfterRefusals, LengthAfterRefusals;

    public override void AddRecipes()
    {
        CountBeforeRefusals = Recipe.numRecipes;
        Recipe crowded = Recipe.Create(ItemID.Shuriken);
        foreach (int type in Thirteen) crowded.AddIngredient(type);
        crowded.AddIngredient(ItemID.Cactus);
        crowded.requiredItem[14] = NewItem(ItemID.Sunflower);
        try { crowded.Register(); }
        catch (RecipeException) { RefusedFull = true; }

        Recipe hole = Recipe.Create(ItemID.Shuriken);
        for (int k = 0; k < 5; k++) hole.AddIngredient(Thirteen[k]);
        hole.requiredItem[3] = default;
        try { hole.Register(); }
        catch (RecipeException) { RefusedNull = true; }
        CountAfterRefusals = Recipe.numRecipes;
        LengthAfterRefusals = Main.recipe.Length;

        Full = Recipe.Create(ItemID.Shuriken);
        foreach (int type in Thirteen) Full.AddIngredient(type);
        Full.AddIngredient(ItemID.Cactus);
        Full.Register();
        Full.requiredItem[14] = NewItem(ItemID.Sunflower);

        Holed = Recipe.Create(ItemID.Shuriken);
        for (int k = 0; k < 5; k++) Holed.AddIngredient(Thirteen[k]);
        Holed.Register();
        Holed.requiredItem[3] = default;

        Largest = Recipe.Create(ItemID.Shuriken);
        foreach (int type in Thirteen) Largest.AddIngredient(type);
        Largest.AddIngredient(ModContent.ItemType<Pebble>());
        try { Largest.AddIngredient(ItemID.Cactus); }
        catch (RecipeException e)
        {
            ThrewAt15 = true;
            ThrowMessage = e.Message;
        }
        Largest.Register();
    }

    public override void PostAddRecipes()
    {
        Recipe first = Main.recipe[0];
        FirstCount = Count(first);
        SharedSlot = first.requiredItem[FirstCount].Pointer == Recipe.defaultRecipeItem.Pointer;

        Sword = Find(ItemID.WoodenSword);
        Sword.AddIngredient(ItemID.Gel);
        SharedAfterAdd = Recipe.defaultRecipeItem.type;

        Bow = Find(ItemID.WoodenBow);
        while (true)
        {
            try { Bow.AddIngredient(ItemID.Cactus + BowAdded); }
            catch (RecipeException) { break; }
            BowAdded++;
        }
        Bow.requiredItem[14] = NewItem(ItemID.Sunflower);

        Recipe.defaultRecipeItem.SetDefaults(ItemID.Gel, default);
    }

    public static int Count(Recipe recipe)
    {
        var items = recipe.requiredItem;
        int n = 0;
        while (n < items.Length && !items[n].IsNull && items[n].type > 0) n++;
        return n;
    }

    static Recipe Find(int result)
    {
        for (int i = 0; i < Recipe.numRecipes; i++)
        {
            if (Main.recipe[i].createItem.type == result) return Main.recipe[i];
        }
        return default;
    }

    static Item NewItem(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        item.stack = 1;
        return item;
    }
}
