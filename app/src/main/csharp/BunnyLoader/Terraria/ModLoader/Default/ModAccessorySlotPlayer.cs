using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace Terraria.ModLoader.Default;

public sealed class ModAccessorySlotPlayer
{
    static AccessorySlotLoader Loader => AccessorySlotLoader.Instance;

    internal GameArray<Item> exAccessorySlot;
    internal GameArray<Item> exDyesAccessory;
    internal bool[] exHideAccessory = Array.Empty<bool>();

    readonly Dictionary<string, int> slots = new();
    bool[] sharedLoadoutSlotTypes = Array.Empty<bool>();
    readonly ExEquipmentLoadout[] exLoadouts;
    readonly List<Item> extraItems = new();
    GameHandle? accessoryPin, dyePin;

    internal int scrollRow;

    public Player Player { get; }

    public int SlotCount => slots.Count;

    public int LoadedSlotCount => Loader.TotalCount;

    internal ModAccessorySlotPlayer(Player player)
    {
        Player = player;
        foreach (ModAccessorySlot slot in Loader.list)
            slots.Add(slot.FullName, slot.Type);
        ResetAndSizeAccessoryArrays();
        int loadouts = player.Loadouts.IsNull ? 0 : player.Loadouts.Length;
        exLoadouts = Enumerable.Range(0, loadouts).Select(i => new ExEquipmentLoadout(i, SlotCount)).ToArray();
    }

    internal void ResetAndSizeAccessoryArrays()
    {
        int size = SlotCount;
        accessoryPin?.Dispose();
        dyePin?.Dispose();
        exAccessorySlot = AccessorySlotHooks.NewItems(2 * size, out accessoryPin);
        exDyesAccessory = AccessorySlotHooks.NewItems(size, out dyePin);
        exHideAccessory = new bool[size];
        sharedLoadoutSlotTypes = new bool[size];
        foreach (ModAccessorySlot slot in Loader.list)
        {
            if (!slot.HasEquipmentLoadoutSupport)
                sharedLoadoutSlotTypes[slot.Type] = true;
        }
    }

    internal void SaveData(TagCompound tag)
    {
        tag["order"] = slots.Keys.ToList();
        tag["items"] = Enumerable.Range(0, exAccessorySlot.Length).Select(i => ItemIO.Save(exAccessorySlot[i])).ToList();
        tag["dyes"] = Enumerable.Range(0, exDyesAccessory.Length).Select(i => ItemIO.Save(exDyesAccessory[i])).ToList();
        tag["visible"] = exHideAccessory.ToList();
        foreach (ExEquipmentLoadout loadout in exLoadouts)
            loadout.SaveData(tag);
    }

    internal void LoadData(TagCompound tag)
    {
        var order = tag.GetList<string>("order")?.ToList() ?? new List<string>();
        foreach (string name in order)
        {
            if (!slots.ContainsKey(name))
                slots.Add(name, slots.Count);
        }

        ResetAndSizeAccessoryArrays();

        var items = (tag.GetList<TagCompound>("items") ?? new List<TagCompound>()).Select(ItemIO.Load).ToList();
        var dyes = (tag.GetList<TagCompound>("dyes") ?? new List<TagCompound>()).Select(ItemIO.Load).ToList();
        var visible = tag.GetList<bool>("visible")?.ToList() ?? new List<bool>();

        foreach (ExEquipmentLoadout loadout in exLoadouts)
            extraItems.AddRange(loadout.LoadData(tag, order, slots, sharedLoadoutSlotTypes));

        for (int i = 0; i < order.Count; i++)
        {
            int type = slots[order[i]];
            if (i < dyes.Count) exDyesAccessory[type] = dyes[i];
            if (i < visible.Count) exHideAccessory[type] = visible[i];
            if (i < items.Count) exAccessorySlot[type] = items[i];
            if (i + order.Count < items.Count) exAccessorySlot[type + SlotCount] = items[i + order.Count];
        }
    }

    internal void OnEnterWorld()
    {
        DetectConflictsWithSharedSlots();
        if (extraItems.Count == 0)
            return;
        foreach (Item item in extraItems)
            Player.QuickSpawnItem(Player.GetSource_FromThis(), item);
        Main.NewText(Language.GetTextValue("tModLoader.ModAccessorySlotNoLongerSharedItemsRemoved"));
        extraItems.Clear();
    }

    internal bool CanLoadoutShare(int context, int slot)
    {
        bool validSlot = context switch
        {
            ItemSlot.Context.ModdedAccessorySlot or ItemSlot.Context.ModdedVanityAccessorySlot => (uint)slot < (uint)exAccessorySlot.Length,
            ItemSlot.Context.ModdedDyeSlot => (uint)slot < (uint)exDyesAccessory.Length,
            _ => false
        };
        return validSlot && !IsSharedSlot(slot);
    }

    public Item GetSharedLoadoutItem(int context, int slot, out int? sharedFromLoadout)
    {
        sharedFromLoadout = null;
        GameArray<Item> items = context == ItemSlot.Context.ModdedDyeSlot ? exDyesAccessory : exAccessorySlot;
        if (!items[slot].IsAir || !CanLoadoutShare(context, slot))
            return items[slot];
        foreach (ExEquipmentLoadout loadout in exLoadouts)
        {
            Item item = context == ItemSlot.Context.ModdedDyeSlot ? loadout.ExDyesAccessory[slot] : loadout.ExAccessorySlot[slot];
            if (!item.IsAir && item.favorited)
            {
                sharedFromLoadout = loadout.LoadoutIndex;
                return item;
            }
        }
        return items[slot];
    }

    internal void ClearSlotShareInOtherLoadouts(int context, int slot)
    {
        if (!CanLoadoutShare(context, slot))
            return;
        foreach (ExEquipmentLoadout loadout in exLoadouts)
        {
            if (context == ItemSlot.Context.ModdedDyeSlot)
                loadout.ExDyesAccessory[slot].favorited = false;
            else
                loadout.ExAccessorySlot[slot].favorited = false;
        }
    }

    internal void UpdateVisibleAccessories()
    {
        for (int k = 0; k < SlotCount; k++)
        {
            if (Loader.ModdedIsSpecificItemSlotUnlockedAndUsable(k, Player, vanity: false))
                AccessorySlotHooks.UpdateVisibleAccessories(Player, GetSharedLoadoutItem(ItemSlot.Context.ModdedAccessorySlot, k, out _), exHideAccessory[k]);
        }
    }

    internal void UpdateVisibleVanityAccessories()
    {
        for (int k = 0; k < SlotCount; k++)
        {
            if (!Loader.ModdedIsSpecificItemSlotUnlockedAndUsable(k, Player, vanity: true))
                continue;
            Item vanityItem = GetSharedLoadoutItem(ItemSlot.Context.ModdedVanityAccessorySlot, k + SlotCount, out _);
            if (!vanityItem.IsAir && !Player.ItemIsVisuallyIncompatible(vanityItem))
                Player.UpdateVisibleAccessory(AccessorySlotHooks.VanityProxySlot, vanityItem);
        }
    }

    public void UpdateDyes(bool socialSlots)
    {
        int start = socialSlots ? SlotCount : 0;
        int end = socialSlots ? SlotCount * 2 : SlotCount;
        for (int i = start; i < end; i++)
        {
            int num = i % exDyesAccessory.Length;
            if (!Loader.ModdedIsSpecificItemSlotUnlockedAndUsable(num, Player, vanity: socialSlots))
                continue;
            Item armor = GetSharedLoadoutItem(socialSlots ? ItemSlot.Context.ModdedVanityAccessorySlot : ItemSlot.Context.ModdedAccessorySlot, i, out _);
            if (!armor.IsAir)
                Player.UpdateItemDye(i < exDyesAccessory.Length, exHideAccessory[num], armor, GetSharedLoadoutItem(ItemSlot.Context.ModdedDyeSlot, num, out _));
        }
    }

    internal void UpdateEquips()
    {
        for (int k = 0; k < SlotCount; k++)
        {
            if (Loader.ModdedIsSpecificItemSlotUnlockedAndUsable(k, Player, vanity: false))
                Loader.CustomUpdateEquips(k, Player);
        }
    }

    public void DropItems(IEntitySource itemSource)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            Player.TryDroppingSingleItem(itemSource, exAccessorySlot[i]);
            Player.TryDroppingSingleItem(itemSource, exAccessorySlot[i + SlotCount]);
            Player.TryDroppingSingleItem(itemSource, exDyesAccessory[i]);
            foreach (ExEquipmentLoadout loadout in exLoadouts)
            {
                Player.TryDroppingSingleItem(itemSource, loadout.ExAccessorySlot[i]);
                Player.TryDroppingSingleItem(itemSource, loadout.ExAccessorySlot[i + SlotCount]);
                Player.TryDroppingSingleItem(itemSource, loadout.ExDyesAccessory[i]);
            }
        }
    }

    internal void OnEquipmentLoadoutSwitched(int oldLoadoutIndex, int loadoutIndex)
    {
        if ((uint)oldLoadoutIndex < (uint)exLoadouts.Length)
            exLoadouts[oldLoadoutIndex].Swap(this);
        if ((uint)loadoutIndex < (uint)exLoadouts.Length)
            exLoadouts[loadoutIndex].Swap(this);
        if (Player.whoAmI == Main.myPlayer)
            DetectConflictsWithSharedSlots();
    }

    void DetectConflictsWithSharedSlots()
    {
        bool anyConflict = false;
        for (int i = 0; i < SlotCount; i++)
        {
            if (!IsSharedSlot(i))
                continue;
            anyConflict |= Loader.IsAccessoryInConflict(Player, exAccessorySlot[i], i, ItemSlot.Context.ModdedAccessorySlot);
            anyConflict |= Loader.IsAccessoryInConflict(Player, exAccessorySlot[i + SlotCount], i + SlotCount, ItemSlot.Context.ModdedVanityAccessorySlot);
        }
        if (anyConflict)
            Main.NewText(Language.GetTextValue("tModLoader.SharedAccessorySlotConflictMessage"));
    }

    internal bool IsSharedSlot(int slotType) => SlotCount > 0 && sharedLoadoutSlotTypes[slotType % SlotCount];

    internal bool SharedSlotHasLoadoutConflict(int slotType, bool vanitySlot)
    {
        if (!IsSharedSlot(slotType))
            return false;
        int functional = slotType % SlotCount;
        int vanity = functional + SlotCount;
        return vanitySlot
            ? Loader.IsAccessoryInConflict(Player, exAccessorySlot[vanity], vanity, ItemSlot.Context.ModdedVanityAccessorySlot)
            : Loader.IsAccessoryInConflict(Player, exAccessorySlot[functional], functional, ItemSlot.Context.ModdedAccessorySlot);
    }

    internal sealed class ExEquipmentLoadout
    {
        readonly string identifier;
        GameHandle? accessoryPin, dyePin;

        public ExEquipmentLoadout(int loadoutIndex, int slotCount)
        {
            LoadoutIndex = loadoutIndex;
            identifier = $"loadout_{loadoutIndex}";
            ResetAndSizeAccessoryArrays(slotCount);
        }

        public int LoadoutIndex { get; }

        public GameArray<Item> ExAccessorySlot { get; private set; }

        public GameArray<Item> ExDyesAccessory { get; private set; }

        public bool[] ExHideAccessory { get; private set; } = Array.Empty<bool>();

        public void SaveData(TagCompound tag)
        {
            tag[identifier] = new TagCompound
            {
                ["items"] = Enumerable.Range(0, ExAccessorySlot.Length).Select(i => ItemIO.Save(ExAccessorySlot[i])).ToList(),
                ["dyes"] = Enumerable.Range(0, ExDyesAccessory.Length).Select(i => ItemIO.Save(ExDyesAccessory[i])).ToList(),
                ["hidden"] = ExHideAccessory.ToList(),
            };
        }

        public IReadOnlyList<Item> LoadData(TagCompound tag, List<string> order, Dictionary<string, int> slots, bool[] sharedLoadoutSlotTypes)
        {
            List<Item> itemsToDrop = new();
            ResetAndSizeAccessoryArrays(slots.Count);
            if (!tag.ContainsKey(identifier))
                return itemsToDrop;

            tag = tag.GetCompound(identifier);
            var items = (tag.GetList<TagCompound>("items") ?? new List<TagCompound>()).Select(ItemIO.Load).ToList();
            var dyes = (tag.GetList<TagCompound>("dyes") ?? new List<TagCompound>()).Select(ItemIO.Load).ToList();
            var hidden = tag.GetList<bool>("hidden")?.ToList() ?? new List<bool>();

            for (int i = 0; i < order.Count; i++)
            {
                int type = slots[order[i]];
                Item dye = i < dyes.Count ? dyes[i] : AccessorySlotHooks.Air();
                Item accessory = i < items.Count ? items[i] : AccessorySlotHooks.Air();
                Item vanityItem = i + order.Count < items.Count ? items[i + order.Count] : AccessorySlotHooks.Air();
                bool isHidden = i < hidden.Count && hidden[i];

                if (sharedLoadoutSlotTypes[type])
                {
                    if (!accessory.IsAir) itemsToDrop.Add(accessory);
                    if (!vanityItem.IsAir) itemsToDrop.Add(vanityItem);
                    if (!dye.IsAir) itemsToDrop.Add(dye);
                    continue;
                }

                ExDyesAccessory[type] = dye;
                ExAccessorySlot[type + slots.Count] = vanityItem;
                ExAccessorySlot[type] = accessory;
                ExHideAccessory[type] = isHidden;
            }
            return itemsToDrop;
        }

        void ResetAndSizeAccessoryArrays(int size)
        {
            accessoryPin?.Dispose();
            dyePin?.Dispose();
            ExAccessorySlot = AccessorySlotHooks.NewItems(2 * size, out accessoryPin);
            ExDyesAccessory = AccessorySlotHooks.NewItems(size, out dyePin);
            ExHideAccessory = new bool[size];
        }

        internal void Swap(ModAccessorySlotPlayer player)
        {
            GameArray<Item> armor = player.exAccessorySlot;
            for (int i = 0; i < armor.Length; i++)
            {
                if (player.IsSharedSlot(i))
                    continue;
                (armor[i], ExAccessorySlot[i]) = (ExAccessorySlot[i], armor[i]);
            }
            GameArray<Item> dye = player.exDyesAccessory;
            for (int j = 0; j < dye.Length; j++)
            {
                if (player.IsSharedSlot(j))
                    continue;
                (dye[j], ExDyesAccessory[j]) = (ExDyesAccessory[j], dye[j]);
            }
            bool[] hide = player.exHideAccessory;
            for (int k = 0; k < hide.Length; k++)
            {
                if (player.IsSharedSlot(k))
                    continue;
                (hide[k], ExHideAccessory[k]) = (ExHideAccessory[k], hide[k]);
            }
        }
    }
}
