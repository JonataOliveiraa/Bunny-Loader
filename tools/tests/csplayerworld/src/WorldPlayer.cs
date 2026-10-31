using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPlayerWorld;

public class WorldPlayer : ModPlayer
{
    public static readonly Dictionary<string, int> Calls = new();
    public static readonly List<string> TeleportContexts = new();
    public static readonly List<string> StartingKeys = new();
    public static bool VetoPickup, VetoTeleport, VetoSell, VetoBuy, VetoNurse, VetoCatch, SwapOnShift;
    public static int NurseExtra, NurseHealth = -1, LastContext = -1, LastSlot = -1, LastInventoryLength = -1;
    public static int PostNurseHealth = -1, PostNursePrice = -1;
    public static NPC LastVendor;
    public static Item LastItem;
    public static bool LastFailed = true;

    static void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public override bool OnPickup(WorldItem item)
    {
        Count(nameof(OnPickup));
        return !(VetoPickup && item.inner.type == ItemID.Gel);
    }

    public override bool CanBeTeleportedTo(Vector2 teleportPosition, string context)
    {
        Count(nameof(CanBeTeleportedTo));
        TeleportContexts.Add(context);
        return !(VetoTeleport && context == "TeleportRod");
    }

    public override bool HoverSlot(Item[] inventory, int context, int slot)
    {
        Count(nameof(HoverSlot));
        LastContext = context;
        LastSlot = slot;
        LastInventoryLength = inventory.Length;
        return true;
    }

    public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
    {
        Count(nameof(ShiftClickSlot));
        if (SwapOnShift) Utils.Swap(ref inventory[slot], ref inventory[slot + 1]);
        return true;
    }

    public override bool CanSellItem(NPC vendor, Item[] shopInventory, Item item)
    {
        Count(nameof(CanSellItem));
        return !VetoSell;
    }

    public override void PostSellItem(NPC vendor, Item[] shopInventory, Item item)
    {
        Count(nameof(PostSellItem));
        LastVendor = vendor;
        LastItem = item;
    }

    public override bool CanBuyItem(NPC vendor, Item[] shopInventory, Item item)
    {
        Count(nameof(CanBuyItem));
        return !VetoBuy;
    }

    public override void PostBuyItem(NPC vendor, Item[] shopInventory, Item item)
    {
        Count(nameof(PostBuyItem));
        LastVendor = vendor;
        LastItem = item;
    }

    public override bool ModifyNurseHeal(NPC nurse, ref int health, ref bool removeDebuffs, ref string chatText)
    {
        Count(nameof(ModifyNurseHeal));
        if (VetoNurse)
        {
            chatText = "csplayerworld nurse says no";
            return false;
        }
        if (NurseHealth >= 0) health = NurseHealth;
        return true;
    }

    public override void ModifyNursePrice(NPC nurse, int health, bool removeDebuffs, ref int price)
    {
        Count(nameof(ModifyNursePrice));
        price += NurseExtra;
    }

    public override void PostNurseHeal(NPC nurse, int health, bool removeDebuffs, int price)
    {
        Count(nameof(PostNurseHeal));
        LastVendor = nurse;
        PostNurseHealth = health;
        PostNursePrice = price;
    }

    public override bool? CanCatchNPC(NPC target, Item item)
    {
        Count(nameof(CanCatchNPC));
        return VetoCatch ? false : null;
    }

    public override void OnCatchNPC(NPC npc, Item item, bool failed)
    {
        Count(nameof(OnCatchNPC));
        LastFailed = failed;
    }

    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    {
        Count(nameof(AddStartingItems));
        if (mediumCoreDeath) yield return new Item(ItemID.Torch, 33);
    }

    public override void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath)
    {
        Count(nameof(ModifyStartingInventory));
        StartingKeys.Clear();
        StartingKeys.AddRange(itemsByMod.Keys);
    }
}
