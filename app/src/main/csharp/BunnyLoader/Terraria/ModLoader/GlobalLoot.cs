using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.GameContent.ItemDropRules;

namespace Terraria.ModLoader;

public readonly struct GlobalLoot : ILoot
{
    readonly ItemDropDatabase database;
    public GlobalLoot(ItemDropDatabase database) => this.database = database;
    public List<IItemDropRule> Get(bool includeGlobalDrops = true) => DropRuleInterop.ReadRules(database._globalEntries);
    public IItemDropRule Add(IItemDropRule entry) { database.RegisterToGlobal(DropRuleInterop.ToNative(entry)); return entry; }
    public IItemDropRule Remove(IItemDropRule entry)
    {
        var list = new GameList(database._globalEntries);
        var pointers = new List<IntPtr>();
        IntPtr target = DropRuleInterop.ToNative(entry).Pointer;
        bool removed = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (!removed && list[i] == target) { removed = true; continue; }
            pointers.Add(list[i]);
        }
        if (!removed) return entry;
        DropRuleInterop.Clear(database._globalEntries);
        foreach (IntPtr pointer in pointers) list.Add(pointer);
        return entry;
    }
    public void RemoveWhere(Predicate<IItemDropRule> predicate, bool includeGlobalDrops = true)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        foreach (IItemDropRule entry in Get()) if (predicate(entry)) Remove(entry);
    }
}
