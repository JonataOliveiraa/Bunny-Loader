using System.Collections.Generic;

namespace Terraria.DataStructures;

public abstract class ItemCreationContext
{
}

public class RecipeItemCreationContext : ItemCreationContext
{
    public readonly Recipe Recipe;

    public readonly List<Item> ConsumedItems;

    public readonly Item DestinationStack;

    public RecipeItemCreationContext(Recipe recipe, List<Item> consumedItems, Item destinationStack = default)
    {
        Recipe = recipe;
        ConsumedItems = consumedItems;
        DestinationStack = destinationStack;
    }
}

public class BuyItemCreationContext : ItemCreationContext
{
    public Item DestinationStack;

    public NPC VendorNPC;

    public BuyItemCreationContext(Item destinationStack, NPC vendorNPC)
    {
        DestinationStack = destinationStack;
        VendorNPC = vendorNPC;
    }
}
