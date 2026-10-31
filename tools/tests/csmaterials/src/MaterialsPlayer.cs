using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsMaterials;

public class MaterialRecipes : ModSystem
{
    public static Recipe TorchFromRuby;

    public override void AddRecipes()
    {
        TorchFromRuby = Recipe.Create(ItemID.Torch, 3);
        TorchFromRuby.AddIngredient(ItemID.Ruby, 2);
        TorchFromRuby.Register();
    }
}

public class MaterialsPlayer : ModPlayer
{
    public static Item[] Bag;
    public static int Calls, Consumed, LastIndex = -1, LastStack = -1;

    public override IEnumerable<Item> AddMaterialsForCrafting(out ItemConsumedCallback itemConsumedCallback)
    {
        Calls++;
        Bag ??= new[] { new Item(ItemID.Gel, 1), new Item(ItemID.Ruby, 5) };
        itemConsumedCallback = (item, index) =>
        {
            Consumed++;
            LastIndex = index;
            LastStack = item.stack;
        };
        return Bag;
    }
}
