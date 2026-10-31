using System;
using System.Collections.Generic;
using Terraria.GameContent.ItemDropRules;

namespace Terraria.ModLoader;

public readonly struct ItemLoot : ILoot
{
    readonly int itemType;
    readonly ItemDropDatabase database;
    public ItemLoot(int itemType, ItemDropDatabase database) { this.itemType = itemType; this.database = database; }
    public List<IItemDropRule> Get(bool includeGlobalDrops = true) => database.GetRulesForItemID(itemType);
    public IItemDropRule Add(IItemDropRule entry) => database.RegisterToItem(itemType, entry);
    public IItemDropRule Remove(IItemDropRule entry) => database.RemoveFromItem(itemType, entry);
    public void RemoveWhere(Predicate<IItemDropRule> predicate, bool includeGlobalDrops = true)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        foreach (IItemDropRule entry in Get()) if (predicate(entry)) Remove(entry);
    }
}
