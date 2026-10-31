using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static class PlayerWorldHooks
{
    sealed class ShopScope
    {
        public Player Player;
        public Item[] Inventory = Array.Empty<Item>();
        public Item Item;
        public NPC Vendor;
        public bool Bought;
    }

    sealed class NursePlan
    {
        public Player Player;
        public NPC Nurse;
        public int Health;
        public bool RemoveDebuffs = true;
        public string Chat = "";
        public bool Allowed = true;
        public int Price;
        public bool Paid;
    }

    sealed class CatchScope
    {
        public Player Player;
        public Item Item;
    }

    static ShopScope? shop;
    static NursePlan? nurse;
    static CatchScope? catching;

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModPlayer.OnPickup))) ItemHooks.Once("PlayerPickup", () => On_Player.PickupItem += PickupItem);
        if (has(nameof(ModPlayer.HoverSlot))) ItemHooks.Once("PlayerHoverSlot", () => On_ItemSlot.OverrideHover_ItemArray_int_int += OverrideHover);
        if (has(nameof(ModPlayer.ShiftClickSlot))) ItemHooks.Once("PlayerShiftClickSlot", () => On_ItemSlot.OverrideLeftClick += OverrideLeftClick);
        if (has(nameof(ModPlayer.CanBeTeleportedTo)))
            ItemHooks.Once("PlayerTeleport", () =>
            {
                On_Player.Teleport += Teleport;
                On_Player.UnityTeleport += UnityTeleport;
            });
        if (has(nameof(ModPlayer.CanBuyItem)) || has(nameof(ModPlayer.PostBuyItem)) || has(nameof(ModPlayer.CanSellItem)) || has(nameof(ModPlayer.PostSellItem)))
            ItemHooks.Once("PlayerShop", () =>
            {
                On_ItemSlot.HandleShopSlot += HandleShopSlot;
                On_Player.SellItem += SellItem;
                WantBuyItem();
            });
        if (has(nameof(ModPlayer.ModifyNursePrice)) || has(nameof(ModPlayer.ModifyNurseHeal)) || has(nameof(ModPlayer.PostNurseHeal)))
            ItemHooks.Once("PlayerNurse", () =>
            {
                On_Main.GetNurseHealCost += GetNurseHealCost;
                On_Main.NPCChatText_DoNurseHeal += DoNurseHeal;
                WantBuyItem();
            });
        if (has(nameof(ModPlayer.CanCatchNPC)) || has(nameof(ModPlayer.OnCatchNPC)))
            ItemHooks.Once("PlayerCatching", () =>
            {
                On_Player.ItemCheck_CatchCritters += CatchCritters;
                On_NPC.CatchNPC += CatchNPC;
                PlayerHurtHooks.Want();
            });
        if (has(nameof(ModPlayer.AddStartingItems)) || has(nameof(ModPlayer.ModifyStartingInventory)))
            ItemHooks.Once("PlayerStartingItems", () =>
            {
                On_GUIPlayerCreateMenu.SetupStartingItems += SetupStartingItems;
                On_Player.DropItems += DropItems;
            });
    }

    static void WantBuyItem() => ItemHooks.Once("PlayerBuyItem", () => On_Player.BuyItem += BuyItem);

    static Player Local => Main.LocalPlayer;

    static NPC VendorOf(Player player)
    {
        int talk = player.talkNPC;
        return (uint)talk < 200u ? Main.npc[talk] : default;
    }

    static Item[] Copy(GameArray<Item> items)
    {
        var copy = new Item[items.IsNull ? 0 : items.Length];
        for (int i = 0; i < copy.Length; i++) copy[i] = items[i];
        return copy;
    }

    static void Back(GameArray<Item> items, Item[] copy)
    {
        if (items.IsNull) return;
        int count = Math.Min(items.Length, copy.Length);
        for (int i = 0; i < count; i++)
        {
            if (copy[i] != items[i]) items[i] = copy[i];
        }
    }

    static bool Any(Player player, string method, Func<ModPlayer, bool> call)
    {
        bool hit = false;
        PlayerLoader.Each(player, method, m => hit |= call(m));
        return hit;
    }

    static void PickupItem(On_Player.orig_PickupItem orig, Player self, WorldItem itemToPickUp)
    {
        if (!PlayerLoader.All(self, nameof(ModPlayer.OnPickup), itemToPickUp, static (m, w) => m.OnPickup(w)))
        {
            itemToPickUp.TurnToAirAndSync();
            return;
        }
        orig(self, itemToPickUp);
    }

    static void OverrideHover(On_ItemSlot.orig_OverrideHover_ItemArray_int_int orig, GameArray<Item> inv, int context, int slot)
    {
        Item[] copy = Copy(inv);
        bool handled = Any(Local, nameof(ModPlayer.HoverSlot), m => m.HoverSlot(copy, context, slot));
        Back(inv, copy);
        if (!handled) orig(inv, context, slot);
    }

    static bool OverrideLeftClick(On_ItemSlot.orig_OverrideLeftClick orig, GameArray<Item> inv, int context, int slot)
    {
        if (ItemSlot.ShiftInUse)
        {
            Item[] copy = Copy(inv);
            bool handled = Any(Local, nameof(ModPlayer.ShiftClickSlot), m => m.ShiftClickSlot(copy, context, slot));
            Back(inv, copy);
            if (handled) return true;
        }
        return orig(inv, context, slot);
    }

    static void Teleport(On_Player.orig_Teleport orig, Player self, Vector2 newPos, int Style, int extraInfo)
    {
        string context = Style == 1 ? "TeleportRod" : Style == 2 ? "TeleportationPotion" : "Teleport";
        if (PlayerLoader.All(self, nameof(ModPlayer.CanBeTeleportedTo), (newPos, context), static (m, s) => m.CanBeTeleportedTo(s.newPos, s.context)))
            orig(self, newPos, Style, extraInfo);
    }

    static void UnityTeleport(On_Player.orig_UnityTeleport orig, Player self, Vector2 telePos)
    {
        if (PlayerLoader.All(self, nameof(ModPlayer.CanBeTeleportedTo), telePos, static (m, p) => m.CanBeTeleportedTo(p, "Wormhole")))
            orig(self, telePos);
    }

    static void HandleShopSlot(On_ItemSlot.orig_HandleShopSlot orig, GameArray<Item> inv, int slot, bool rightClickIsValid, bool leftClickIsValid)
    {
        Player player = Local;
        ShopScope? outer = shop;
        var scope = new ShopScope { Player = player, Inventory = Copy(inv), Item = (uint)slot < (uint)inv.Length ? inv[slot] : default, Vendor = VendorOf(player) };
        shop = scope;
        try { orig(inv, slot, rightClickIsValid, leftClickIsValid); }
        finally { shop = outer; }
        if (scope.Bought)
            PlayerLoader.Each(player, nameof(ModPlayer.PostBuyItem), (scope.Vendor, scope.Inventory, Main.mouseItem),
                static (m, s) => m.PostBuyItem(s.Vendor, s.Inventory, s.mouseItem));
    }

    static bool BuyItem(On_Player.orig_BuyItem orig, Player self, long price, int customCurrency)
    {
        ShopScope? scope = shop;
        bool shopping = scope != null && scope.Player == self;
        if (shopping && !PlayerLoader.All(self, nameof(ModPlayer.CanBuyItem), scope!, static (m, s) => m.CanBuyItem(s.Vendor, s.Inventory, s.Item))) return false;
        NursePlan? plan = nurse;
        bool nursing = plan != null && plan.Player == self;
        bool result = orig(self, nursing ? plan!.Price : price, customCurrency);
        if (result && shopping) scope!.Bought = true;
        if (result && nursing) plan!.Paid = true;
        return result;
    }

    static bool SellItem(On_Player.orig_SellItem orig, Player self, Item item, int stack)
    {
        NPC vendor = VendorOf(self);
        if (vendor.IsNull) return orig(self, item, stack);
        Item[] inventory;
        try { inventory = Copy(Main.instance.shop[Main.npcShop].item); }
        catch (Exception) { inventory = Array.Empty<Item>(); }
        if (!PlayerLoader.All(self, nameof(ModPlayer.CanSellItem), (vendor, inventory, item), static (m, s) => m.CanSellItem(s.vendor, s.inventory, s.item))) return false;
        bool result = orig(self, item, stack);
        if (result) PlayerLoader.Each(self, nameof(ModPlayer.PostSellItem), (vendor, inventory, item), static (m, s) => m.PostSellItem(s.vendor, s.inventory, s.item));
        return result;
    }

    static NursePlan Plan(Player player)
    {
        var plan = new NursePlan
        {
            Player = player,
            Nurse = VendorOf(player),
            Health = Math.Max(0, player.statLifeMax2 - player.statLife),
            Chat = Main.npcChatText ?? "",
        };
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyNurseHeal), plan, static (m, p) =>
        {
            int health = p.Health;
            bool debuffs = p.RemoveDebuffs;
            string chat = p.Chat;
            if (!m.ModifyNurseHeal(p.Nurse, ref health, ref debuffs, ref chat)) p.Allowed = false;
            p.Health = Math.Max(0, health);
            p.RemoveDebuffs = debuffs;
            p.Chat = chat ?? "";
        });
        return plan;
    }

    static T WithNurse<T>(NursePlan plan, Func<T> run)
    {
        Player player = plan.Player;
        int max = player.statLifeMax2;
        var hidden = new Dictionary<int, bool>();
        var debuff = Main.debuff;
        if (!plan.RemoveDebuffs)
        {
            var types = player.buffType;
            for (int i = 0; i < types.Length; i++)
            {
                int type = types[i];
                if (type <= 0 || (uint)type >= (uint)debuff.Length || hidden.ContainsKey(type)) continue;
                hidden[type] = debuff[type];
                debuff[type] = false;
            }
        }
        player.statLifeMax2 = player.statLife + Math.Min(plan.Health, Math.Max(0, max - player.statLife));
        try { return run(); }
        finally
        {
            player.statLifeMax2 = max;
            foreach ((int type, bool value) in hidden) debuff[type] = value;
        }
    }

    static int GetNurseHealCost(On_Main.orig_GetNurseHealCost orig)
    {
        NursePlan plan = Plan(Local);
        int price = WithNurse(plan, () => orig());
        PlayerLoader.Each(plan.Player, nameof(ModPlayer.ModifyNursePrice), plan, ref price,
            static (m, p, ref v) => m.ModifyNursePrice(p.Nurse, p.Health, p.RemoveDebuffs, ref v));
        return Math.Max(0, price);
    }

    static void DoNurseHeal(On_Main.orig_NPCChatText_DoNurseHeal orig, int healCost)
    {
        Player player = Local;
        NursePlan plan = Plan(player);
        if (!plan.Allowed)
        {
            Main.npcChatText = plan.Chat;
            return;
        }
        plan.Price = healCost;
        NursePlan? outer = nurse;
        nurse = plan;
        try { WithNurse(plan, () => { orig(Math.Max(1, healCost)); return 0; }); }
        finally { nurse = outer; }
        if (plan.Paid)
            PlayerLoader.Each(player, nameof(ModPlayer.PostNurseHeal), plan, static (m, p) => m.PostNurseHeal(p.Nurse, p.Health, p.RemoveDebuffs, p.Price));
    }

    static void CatchCritters(On_Player.orig_ItemCheck_CatchCritters orig, Player self, Item sItem, Rectangle itemRectangle)
    {
        CatchScope? outer = catching;
        catching = new CatchScope { Player = self, Item = sItem };
        var blocked = new List<(NPC Npc, int Type)>();
        try
        {
            var npcs = Main.npc;
            for (int i = 0; i < npcs.Length; i++)
            {
                NPC npc = npcs[i];
                if (npc.IsNull || !npc.active || npc.catchItem <= 0) continue;
                Rectangle box = npc.Hitbox;
                if (box.X >= itemRectangle.X + itemRectangle.Width || itemRectangle.X >= box.X + box.Width ||
                    box.Y >= itemRectangle.Y + itemRectangle.Height || itemRectangle.Y >= box.Y + box.Height)
                    continue;
                bool? decision = PlayerLoader.Nullable(self, nameof(ModPlayer.CanCatchNPC), (npc, sItem), static (m, s) => m.CanCatchNPC(s.npc, s.sItem));
                if (decision == false)
                {
                    blocked.Add((npc, npc.catchItem));
                    npc.catchItem = 0;
                }
                else if (decision == true) NPC.CatchNPC(i, self.whoAmI);
            }
            orig(self, sItem, itemRectangle);
        }
        finally
        {
            foreach ((NPC npc, int type) in blocked) npc.catchItem = (short)type;
            catching = outer;
        }
    }

    static void CatchNPC(On_NPC.orig_CatchNPC orig, int i, int who)
    {
        CatchScope? scope = catching;
        Player player = scope?.Player ?? Main.player[who >= 0 && who < 255 ? who : Main.myPlayer];
        NPC npc = Main.npc[i];
        Item item = scope?.Item ?? player.inventory[player.selectedItem];
        if (scope == null && PlayerLoader.Nullable(player, nameof(ModPlayer.CanCatchNPC), (npc, item), static (m, s) => m.CanCatchNPC(s.npc, s.item)) == false) return;
        orig(i, who);
        PlayerLoader.Each(player, nameof(ModPlayer.OnCatchNPC), (npc, item), static (m, s) => m.OnCatchNPC(s.npc, s.item, false));
    }

    internal static void HurtWhileCatching(Player player, PlayerDeathReason source)
    {
        CatchScope? scope = catching;
        if (scope == null || scope.Player != player || source.IsNull) return;
        int index = source._sourceNPCIndex;
        if ((uint)index >= 200u) return;
        PlayerLoader.Each(player, nameof(ModPlayer.OnCatchNPC), (npc: Main.npc[index], scope.Item), static (m, s) => m.OnCatchNPC(s.npc, s.Item, true));
    }

    static void SetupStartingItems(On_GUIPlayerCreateMenu.orig_SetupStartingItems orig, GUIPlayerCreateMenu self)
    {
        orig(self);
        Player player = Main.PendingPlayer;
        if (!player.IsNull) Starting(player, false);
    }

    static void DropItems(On_Player.orig_DropItems orig, Player self, bool gemsOnly)
    {
        orig(self, gemsOnly);
        if (!gemsOnly && self.difficulty == 1) Starting(self, true);
    }

    static void Starting(Player player, bool death)
    {
        try
        {
            var inventory = player.inventory;
            var vanilla = new List<Item>();
            for (int i = 0; i < 50; i++)
            {
                Item item = inventory[i];
                if (!item.IsNull && item.type > 0 && item.stack > 0) vanilla.Add(item);
            }
            var byMod = new Dictionary<string, List<Item>> { ["Terraria"] = vanilla };
            PlayerLoader.Each(player, nameof(ModPlayer.AddStartingItems), (byMod, death), static (m, s) =>
            {
                if (!s.byMod.TryGetValue(m.Mod.Name, out List<Item>? list)) s.byMod[m.Mod.Name] = list = new List<Item>();
                list.AddRange(m.AddStartingItems(s.death) ?? Array.Empty<Item>());
            });
            PlayerLoader.Each(player, nameof(ModPlayer.ModifyStartingInventory), (byMod, death), static (m, s) => m.ModifyStartingInventory(s.byMod, s.death));
            List<Item> items = byMod.Values.SelectMany(l => l).Where(item => !item.IsNull && item.type > 0 && item.stack > 0).ToList();
            for (int i = 0; i < 50; i++)
            {
                if (i < items.Count) inventory[i] = items[i];
                else
                {
                    Item empty = Item.New();
                    ItemLoader.KeepAlive(empty);
                    empty.SetDefaults(0, default);
                    inventory[i] = empty;
                }
            }
            for (int i = 50; i < items.Count; i++) player.GetOrDropItem(items[i], default);
        }
        catch (Exception e) { ItemHooks.Report("AddStartingItems", e); }
    }
}
