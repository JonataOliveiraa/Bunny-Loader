using System.Collections.Generic;

namespace Terraria.ModLoader;

public record class RecipeItemCreationContext(Recipe Recipe, List<Item> ConsumedItems, Item DestinationStack) : ItemCreationContext;
