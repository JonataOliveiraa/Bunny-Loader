using System;
using System.Collections.Generic;
using Terraria.GameContent.ItemDropRules;

namespace Terraria.ModLoader;

public readonly struct NPCLoot : ILoot
{
    readonly int npcNetId;
    readonly ItemDropDatabase database;

    public NPCLoot(int npcNetId, ItemDropDatabase database) { this.npcNetId = npcNetId; this.database = database; }
    public List<IItemDropRule> Get(bool includeGlobalDrops = true) => database.GetRulesForNPCID(npcNetId, includeGlobalDrops);
    public IItemDropRule Add(IItemDropRule entry) { database.RegisterToNPCNetId(npcNetId, DropRuleInterop.ToNative(entry)); return entry; }
    public IItemDropRule Remove(IItemDropRule entry) { database.RemoveFromNPCNetId(npcNetId, DropRuleInterop.ToNative(entry)); return entry; }
    public void RemoveWhere(Predicate<IItemDropRule> predicate, bool includeGlobalDrops = true)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        foreach (IItemDropRule entry in Get(includeGlobalDrops)) if (predicate(entry)) Remove(entry);
    }
}
