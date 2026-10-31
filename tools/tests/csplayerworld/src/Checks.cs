using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsPlayerWorld;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPlayerWorldMod>().Logger;

    static int Calls(string name) => WorldPlayer.Calls.GetValueOrDefault(name);

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        Item coins = p.inventory[50];
        p.inventory[50] = new Item(ItemID.PlatinumCoin, 5);
        foreach (Action<Player> step in new Action<Player>[] { Pickup, Teleport, Slots, Sell, Buy, Nurse, Catch })
        {
            try { step(p); }
            catch (Exception e) { Report(step.Method.Name, false, e.ToString()); }
        }
        p.inventory[50] = coins;
        p.talkNPC = -1;
        try { Starting(p); }
        catch (Exception e) { Report("Starting", false, e.ToString()); }
        Log.Info($"csplayerworld END pass={pass} fail={fail}");
    }

    static int CountOf(Player p, int type)
    {
        int count = 0;
        for (int i = 0; i < 58; i++)
        {
            Item item = p.inventory[i];
            if (item.type == type) count += item.stack;
        }
        return count;
    }

    static WorldItem Drop(Player p, int type, int stack)
    {
        int index = Item.NewItem(X: (int)p.Center.X, Y: (int)p.Center.Y, Width: 1, Height: 1, Type: type, Stack: stack, noBroadcast: false, pfix: 0, ownership: default);
        return Main.item[index];
    }

    static int Spawn(Player p, int type)
    {
        int index = NPC.NewNPC(default, (int)p.Center.X + 40, (int)p.Center.Y, type);
        return index;
    }

    static void Pickup(Player p)
    {
        WorldPlayer.VetoPickup = true;
        int gel = CountOf(p, ItemID.Gel);
        WorldItem vetoed = Drop(p, ItemID.Gel, 7);
        p.PickupItem(vetoed);
        Report("OnPickup false destroys the item", !vetoed.active && CountOf(p, ItemID.Gel) == gel,
            $"active={vetoed.active} gel={gel}->{CountOf(p, ItemID.Gel)}");
        int wood = CountOf(p, ItemID.Wood);
        WorldItem allowed = Drop(p, ItemID.Wood, 3);
        p.PickupItem(allowed);
        Report("OnPickup true lets the pickup happen", CountOf(p, ItemID.Wood) == wood + 3 && Calls(nameof(WorldPlayer.OnPickup)) >= 2,
            $"wood={wood}->{CountOf(p, ItemID.Wood)} calls={Calls(nameof(WorldPlayer.OnPickup))}");
        WorldPlayer.VetoPickup = false;
    }

    static void Teleport(Player p)
    {
        float x = p.position.X, y = p.position.Y;
        WorldPlayer.VetoTeleport = true;
        WorldPlayer.TeleportContexts.Clear();
        p.Teleport(new Vector2(x + 160, y), 1, 0);
        bool stayed = p.position.X == x;
        p.Teleport(new Vector2(x + 160, y), 2, 0);
        bool moved = p.position.X == x + 160;
        p.Teleport(new Vector2(x, y), 0, 0);
        WorldPlayer.VetoTeleport = false;
        Report("CanBeTeleportedTo vetoes the teleport rod only", stayed && moved && p.position.X == x,
            $"stayed={stayed} moved={moved} x={p.position.X}/{x}");
        Report("CanBeTeleportedTo gets the context", string.Join(",", WorldPlayer.TeleportContexts) == "TeleportRod,TeleportationPotion,Teleport",
            string.Join(",", WorldPlayer.TeleportContexts));
    }

    static void Slots(Player p)
    {
        ItemSlot.OverrideHover(p.inventory, ItemSlot.Context.InventoryItem, 3);
        Report("HoverSlot gets the inventory, context and slot", Calls(nameof(WorldPlayer.HoverSlot)) == 1 && WorldPlayer.LastSlot == 3 &&
            WorldPlayer.LastContext == ItemSlot.Context.InventoryItem && WorldPlayer.LastInventoryLength == p.inventory.Length,
            $"calls={Calls(nameof(WorldPlayer.HoverSlot))} slot={WorldPlayer.LastSlot} context={WorldPlayer.LastContext} length={WorldPlayer.LastInventoryLength}");

        ItemSlot.OverrideLeftClick(p.inventory, ItemSlot.Context.InventoryItem, 5);
        bool skipped = Calls(nameof(WorldPlayer.ShiftClickSlot)) == 0;
        Item a = p.inventory[5], b = p.inventory[6];
        WorldPlayer.SwapOnShift = true;
        On_ItemSlot.get_ShiftInUse += Shift;
        bool handled;
        try { handled = ItemSlot.OverrideLeftClick(p.inventory, ItemSlot.Context.InventoryItem, 5); }
        finally { On_ItemSlot.get_ShiftInUse -= Shift; }
        WorldPlayer.SwapOnShift = false;
        bool swapped = p.inventory[5] == b && p.inventory[6] == a;
        p.inventory[5] = a;
        p.inventory[6] = b;
        Report("ShiftClickSlot runs only with shift and writes the array back", skipped && handled && swapped && Calls(nameof(WorldPlayer.ShiftClickSlot)) == 1,
            $"skipped={skipped} handled={handled} swapped={swapped} calls={Calls(nameof(WorldPlayer.ShiftClickSlot))}");
    }

    static bool Shift(On_ItemSlot.orig_get_ShiftInUse orig) => true;

    static void Sell(Player p)
    {
        int merchant = Spawn(p, NPCID.Merchant);
        p.talkNPC = merchant;
        WorldPlayer.VetoSell = true;
        Item gel = new Item(ItemID.Gel, 5);
        bool vetoed = p.SellItem(gel, 5);
        WorldPlayer.VetoSell = false;
        bool sold = p.SellItem(gel, 5);
        Report("CanSellItem false blocks the sale", !vetoed && Calls(nameof(WorldPlayer.CanSellItem)) == 2, $"vetoed={vetoed} calls={Calls(nameof(WorldPlayer.CanSellItem))}");
        Report("PostSellItem gets the vendor and the item", sold && Calls(nameof(WorldPlayer.PostSellItem)) == 1 &&
            WorldPlayer.LastVendor.whoAmI == merchant && WorldPlayer.LastItem == gel,
            $"sold={sold} calls={Calls(nameof(WorldPlayer.PostSellItem))} vendor={WorldPlayer.LastVendor.whoAmI}/{merchant}");
    }

    static void Buy(Player p)
    {
        int merchant = p.talkNPC;
        Main.npcShop = 1;
        GameShop(out var shop);
        int type = shop[0].type;
        bool left = Main.mouseLeft;
        int split = Main.stackSplit, cursor = Main.cursorOverride;
        try
        {
            Main.mouseItem = new Item(0);
            Main.mouseLeft = true;
            Main.stackSplit = 0;
            Main.cursorOverride = -1;
            WorldPlayer.VetoBuy = true;
            ItemSlot.HandleShopSlot(shop, 0, false, true);
            bool blocked = Main.mouseItem.type == 0 && Calls(nameof(WorldPlayer.CanBuyItem)) == 1;
            WorldPlayer.VetoBuy = false;
            Main.stackSplit = 0;
            ItemSlot.HandleShopSlot(shop, 0, false, true);
            Report("CanBuyItem false blocks the purchase", blocked, $"mouse={Main.mouseItem.type} calls={Calls(nameof(WorldPlayer.CanBuyItem))}");
            Report("PostBuyItem gets the vendor and the bought item", Main.mouseItem.type == type && Calls(nameof(WorldPlayer.PostBuyItem)) == 1 &&
                WorldPlayer.LastVendor.whoAmI == merchant && WorldPlayer.LastItem.type == type,
                $"type={type} mouse={Main.mouseItem.type} calls={Calls(nameof(WorldPlayer.PostBuyItem))} item={WorldPlayer.LastItem.type}");
        }
        finally
        {
            Main.mouseItem = new Item(0);
            Main.mouseLeft = left;
            Main.stackSplit = split;
            Main.cursorOverride = cursor;
            Main.npc[merchant].active = false;
        }
    }

    static void GameShop(out BunnyLoader.Interop.GameArray<Item> shop) => shop = Main.instance.shop[Main.npcShop].item;

    static void Nurse(Player p)
    {
        int nurse = Spawn(p, NPCID.Nurse);
        p.talkNPC = nurse;
        p.statLife = p.statLifeMax2 - 40;
        WorldPlayer.NurseExtra = 0;
        int baseCost = Main.GetNurseHealCost();
        WorldPlayer.NurseExtra = 1000;
        int cost = Main.GetNurseHealCost();
        WorldPlayer.NurseExtra = 0;
        Report("ModifyNursePrice changes the heal cost", cost == baseCost + 1000 && baseCost > 0, $"base={baseCost} cost={cost}");

        WorldPlayer.VetoNurse = true;
        int life = p.statLife;
        Main.NPCChatText_DoNurseHeal(baseCost);
        WorldPlayer.VetoNurse = false;
        Report("ModifyNurseHeal false keeps the life and sets the chat", p.statLife == life && Main.npcChatText == "csplayerworld nurse says no",
            $"life={life}->{p.statLife} chat={Main.npcChatText}");

        WorldPlayer.NurseHealth = 10;
        int partial = Main.GetNurseHealCost();
        Main.NPCChatText_DoNurseHeal(partial);
        WorldPlayer.NurseHealth = -1;
        Report("ModifyNurseHeal limits the heal and PostNurseHeal runs", p.statLife == life + 10 && Calls(nameof(WorldPlayer.PostNurseHeal)) == 1 &&
            WorldPlayer.PostNurseHealth == 10 && WorldPlayer.PostNursePrice == partial && WorldPlayer.LastVendor.whoAmI == nurse && partial < baseCost,
            $"life={life}->{p.statLife} calls={Calls(nameof(WorldPlayer.PostNurseHeal))} health={WorldPlayer.PostNurseHealth} price={WorldPlayer.PostNursePrice}/{partial} base={baseCost}");
        p.statLife = p.statLifeMax2;
        Main.npc[nurse].active = false;
    }

    static void Catch(Player p)
    {
        int bunny = Spawn(p, NPCID.Bunny);
        WorldPlayer.VetoCatch = true;
        NPC.CatchNPC(bunny, Main.myPlayer);
        bool kept = Main.npc[bunny].active && Calls(nameof(WorldPlayer.OnCatchNPC)) == 0;
        WorldPlayer.VetoCatch = false;
        NPC.CatchNPC(bunny, Main.myPlayer);
        Report("CanCatchNPC false keeps the critter", kept, $"active={Main.npc[bunny].active}");
        Report("OnCatchNPC runs after the catch", !Main.npc[bunny].active && Calls(nameof(WorldPlayer.OnCatchNPC)) == 1 && !WorldPlayer.LastFailed,
            $"active={Main.npc[bunny].active} calls={Calls(nameof(WorldPlayer.OnCatchNPC))} failed={WorldPlayer.LastFailed}");
    }

    static void Starting(Player p)
    {
        byte difficulty = p.difficulty;
        p.difficulty = 1;
        try { p.DropItems(false); }
        finally { p.difficulty = difficulty; }
        string mod = ModContent.GetInstance<CsPlayerWorldMod>().Name;
        Report("AddStartingItems adds items after a mediumcore death", CountOf(p, ItemID.Torch) == 33 && Calls(nameof(WorldPlayer.AddStartingItems)) == 1,
            $"torches={CountOf(p, ItemID.Torch)} calls={Calls(nameof(WorldPlayer.AddStartingItems))}");
        Report("ModifyStartingInventory gets the items by mod", WorldPlayer.StartingKeys.Contains("Terraria") && WorldPlayer.StartingKeys.Contains(mod),
            string.Join(",", WorldPlayer.StartingKeys));
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csplayerworld {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
