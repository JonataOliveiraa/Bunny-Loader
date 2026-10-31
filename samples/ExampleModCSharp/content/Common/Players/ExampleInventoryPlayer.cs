using System.Collections.Generic;
using ExampleMod.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleInventoryPlayer : ModPlayer
{
    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    {
        if (mediumCoreDeath)
        {
            return [
                new Item(ItemID.HealingPotion)
            ];
        }

        return [
            new Item(ModContent.ItemType<ExampleItem>()),
            new Item(ItemID.GoldOre, 256)
        ];
    }

    public override void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath)
    {
        itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.IronAxe);
    }
}
