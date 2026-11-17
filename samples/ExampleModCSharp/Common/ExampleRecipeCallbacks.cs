using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace ExampleMod.Common;

public static class ExampleRecipeCallbacks
{
    // ConsumeItemCallbacks - These are used to adjust the number of ingredients consumed by recipes, similar to Alchemy Table - See https://github.com/tModLoader/tModLoader/wiki/Intermediate-Recipes#custom-item-consumption
    public static void DontConsumeChain(Recipe recipe, int type, ref int amount, bool isDecrafting)
    {
        if (type == ItemID.Chain) amount = 0; // since there is no check for isDecrafting this will not return chains on shimmer either
    }

    // Other ConsumeItemCallback methods...

    // OnCraftCallbacks - These are used to run code after a recipe is crafted - See https://github.com/tModLoader/tModLoader/wiki/Intermediate-Recipes#custom-recipe-craft-behavior
    public static void RandomlySpawnConfetti(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack)
    {
        if (Main.rand.Next(3) != 0) return;
        Player player = Main.LocalPlayer;
        int x = (int)(player.position.X + player.width / 2f), y = (int)player.position.Y;
        Item.NewItem(X: x, Y: y, Width: 1, Height: 1, Type: ItemID.Confetti, Stack: 5, noBroadcast: false, pfix: 0, ownership: default);
    }
}
