using System;
using System.Collections.Generic;
using System.Linq;

namespace Terraria.ModLoader;

public class NPCShop : AbstractNPCShop
{
    readonly List<Entry> entries = new();
    public IReadOnlyList<Entry> Entries => entries;
    protected override IEnumerable<AbstractNPCShop.Entry> AllEntries => entries;
    public bool FillLastSlot { get; private set; }
    internal int Index = -1;
    internal bool Vanilla, Modified;
    public NPCShop(int npcType, string name = "Shop") : base(npcType, name) { }
    public Entry GetEntry(int item) => entries.First(e => e.Item.type == item);
    public bool TryGetEntry(int item, out Entry entry) { entry = entries.FirstOrDefault(e => e.Item.type == item)!; return entry != null; }
    public NPCShop AllowFillingLastSlot() { FillLastSlot = true; return this; }
    public NPCShop Add(params Entry[] added) { entries.AddRange(added); return this; }
    public NPCShop Add(Item item, params Condition[] conditions) => Add(new Entry(item, conditions));
    public NPCShop Add(int item, params Condition[] conditions) => Add(new Entry(item, conditions));
    public NPCShop Add<T>(params Condition[] conditions) where T : ModItem => Add(ModContent.ItemType<T>(), conditions);
    public NPCShop InsertBefore(int target, int item, params Condition[] conditions) => InsertBefore(GetEntry(target), Entry.Create(item), conditions);
    public NPCShop InsertAfter(int target, int item, params Condition[] conditions) => InsertAfter(GetEntry(target), Entry.Create(item), conditions);
    public NPCShop InsertBefore(int target, Item item, params Condition[] conditions) => InsertBefore(GetEntry(target), item, conditions);
    public NPCShop InsertAfter(int target, Item item, params Condition[] conditions) => InsertAfter(GetEntry(target), item, conditions);
    public NPCShop InsertBefore(Entry target, Item item, params Condition[] conditions) => Add(new Entry(item, conditions).SortBefore(target));
    public NPCShop InsertAfter(Entry target, Item item, params Condition[] conditions) => Add(new Entry(item, conditions).SortAfter(target));

    public override void FinishSetup()
    {
        var ordered = new List<Entry>();
        var visiting = new HashSet<Entry>();
        void Visit(Entry entry)
        {
            if (ordered.Contains(entry)) return;
            if (!visiting.Add(entry)) throw new InvalidOperationException($"{FullName}: shop entry ordering loop");
            foreach (Entry before in entries.Where(e => e.Target == entry && !e.After)) Visit(before);
            ordered.Add(entry);
            foreach (Entry after in entries.Where(e => e.Target == entry && e.After)) Visit(after);
            visiting.Remove(entry);
        }
        foreach (Entry entry in entries.Where(e => e.Target == null && !e.OrdersLast)) Visit(entry);
        foreach (Entry entry in entries.Where(e => e.Target == null && e.OrdersLast)) Visit(entry);
        if (ordered.Count != entries.Count) throw new InvalidOperationException($"{FullName}: shop ordering target is not registered in this shop");
        entries.Clear();
        entries.AddRange(ordered);
    }

    public override void FillShop(ICollection<Item> items, NPC npc)
    {
        foreach (Entry entry in entries)
        {
            if (entry.Disabled) continue;
            bool available = entry.ConditionsMet();
            if (!available && !entry.SlotReserved) continue;
            Item item = available ? entry.Item.Clone() : Entry.Create(0);
            item.isAShopItem = true;
            if (available) entry.OnShopOpen(item, npc);
            items.Add(item);
        }
    }

    public override void FillShop(Item[] items, NPC npc, out bool overflow)
    {
        Array.Clear(items);
        var filled = new List<Item>();
        FillShop(filled, npc);
        int limit = Math.Max(0, items.Length - (FillLastSlot ? 0 : 1));
        int count = Math.Min(limit, filled.Count);
        for (int i = 0; i < count; i++) items[i] = filled[i];
        overflow = filled.Count > limit;
    }

    public void Open() => NPCShopDatabase.OpenShop(FullName);

    public new class Entry : AbstractNPCShop.Entry
    {
        internal Entry? Target;
        internal bool After, Native;
        Action<Item, NPC>? opened;
        public bool OrdersLast { get; private set; }
        public bool SlotReserved { get; private set; }
        public Entry(int item, params Condition[] conditions) : this(Create(item), conditions) { }
        public Entry(Item item, params Condition[] conditions) : base(item, conditions) { }
        internal static Item Create(int type) { Item item = Item.New(); item.SetDefaults(type, default); return item; }
        Entry Ordering(Entry target, bool after)
        {
            ArgumentNullException.ThrowIfNull(target);
            for (Entry? e = target; e != null; e = e.Target) if (e == this) throw new InvalidOperationException("Shop entry ordering loop");
            Target = target; After = after; return this;
        }
        public Entry SortBefore(Entry target) => Ordering(target, false);
        public Entry SortAfter(Entry target) => Ordering(target, true);
        public new Entry AddCondition(Condition condition) { base.AddCondition(condition); return this; }
        public Entry OrderLast() { OrdersLast = true; return this; }
        public Entry ReserveSlot() { SlotReserved = true; return this; }
        public Entry AddShopOpenedCallback(Action<Item, NPC> callback) { opened += callback; return this; }
        public void OnShopOpen(Item item, NPC npc) => opened?.Invoke(item, npc);
    }
}
