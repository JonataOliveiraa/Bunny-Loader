using System;
using System.Collections.Generic;
using Terraria.ModLoader.Core;

namespace Terraria.GameContent.ItemDropRules;

public readonly partial struct ItemDropDatabase
{
    static readonly Dictionary<(IntPtr, int), List<IItemDropRule>> ItemEntries = new();

    public List<IItemDropRule> GetRulesForNPCID(int npcNetId, bool includeGlobalDrops = true) => DropRuleInterop.ReadRules(GetRulesForNPCID_(npcNetId, includeGlobalDrops));
    public IItemDropRule RegisterToGlobal(IItemDropRule entry) { RegisterToGlobal(DropRuleInterop.ToNative(entry)); return entry; }
    public IItemDropRule RegisterToNPC(int type, IItemDropRule entry) { RegisterToNPC(type, DropRuleInterop.ToNative(entry)); return entry; }
    public IItemDropRule RemoveFromNPC(int type, IItemDropRule entry) { RemoveFromNPC(type, DropRuleInterop.ToNative(entry)); return entry; }

    public List<IItemDropRule> GetRulesForItemID(int itemID) => ItemEntries.TryGetValue((Pointer, itemID), out var rules) ? new(rules) : new();

    public IItemDropRule RegisterToItem(int type, IItemDropRule entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ItemLootHooks.Enable();
        DropRuleInterop.ToNative(entry);
        if (!ItemEntries.TryGetValue((Pointer, type), out var rules)) ItemEntries.Add((Pointer, type), rules = new());
        rules.Add(entry);
        return entry;
    }

    public IItemDropRule RemoveFromItem(int type, IItemDropRule entry)
    {
        if (ItemEntries.TryGetValue((Pointer, type), out var rules)) rules.Remove(entry);
        return entry;
    }
}
