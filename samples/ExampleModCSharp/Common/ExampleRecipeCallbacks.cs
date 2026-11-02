using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace ExampleMod.Common;

public static class ExampleRecipeCallbacks
{
    public static void DontConsumeChain(Recipe recipe, int type, ref int amount, bool isDecrafting)
    {
        if (type == ItemID.Chain) amount = 0;
    }

    public static void RandomlySpawnConfetti(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack)
    {
        if (Main.rand.Next(3) != 0) return;
        Player player = Main.LocalPlayer;
        int x = (int)(player.position.X + player.width / 2f), y = (int)player.position.Y;
        Item.NewItem(X: x, Y: y, Width: 1, Height: 1, Type: ItemID.Confetti, Stack: 5, noBroadcast: false, pfix: 0, ownership: default);
    }
}
