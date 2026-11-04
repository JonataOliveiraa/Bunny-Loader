using System;
using BunnyLoader;

namespace Terraria;

public readonly unsafe partial struct Chest
{
    public Item GetItem(int slot)
    {
        if (IsNull || slot < 0 || slot >= item.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        return new Item(Host.Native()->ExpandChestItem(index, slot));
    }

    public void SetItem(int slot, Item value)
    {
        if (IsNull || slot < 0 || slot >= item.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        if (value.IsNull) throw new ArgumentException("A chest item cannot be null.", nameof(value));
        item[slot].SetToItem(value);
        if (Host.Native()->StoreChestItem(index, slot, value.Pointer) == 0)
            throw new InvalidOperationException("The chest item data could not be stored.");
        InventoryStorage.SyncChestItemUpdateToStorage(index, slot);
    }
}
