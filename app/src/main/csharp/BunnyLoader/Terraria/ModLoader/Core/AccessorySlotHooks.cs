using System;
using System.IO;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.IO;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static class AccessorySlotHooks
{
    const string SaveKey = "bunny:accessorySlots";

    internal const int FunctionalProxySlot = 3;
    internal const int VanityProxySlot = 13;

    static readonly EntityInstances<ModAccessorySlotPlayer> Players = new();
    static bool installed;

    [ThreadStatic] static int scopeSlot;
    [ThreadStatic] static bool scoped;

    static AccessorySlotLoader Loader => AccessorySlotLoader.Instance;

    internal static int? ScopeSlot => scoped ? scopeSlot : null;

    internal static void Install()
    {
        if (installed)
            return;
        installed = true;
        On_Player.UpdateEquips += UpdateEquips;
        On_Player.UpdateVisibleAccessories += UpdateVisibleAccessories;
        On_Player.UpdateDyes += UpdateDyes;
        On_Player.TrySwitchingLoadout += TrySwitchingLoadout;
        On_Player.DropItems += DropItems;
        On_Player.InternalSavePlayerFile += InternalSavePlayerFile;
        On_Player.LoadPlayer += LoadPlayer;
        On_Player_Hooks.EnterWorld += EnterWorld;
        On_ItemSlot.PickItemMovementAction += PickItemMovementAction;
        On_ItemSlot.ArmorSwap += ArmorSwap;
        AccessorySlotPanel.Install();
        AccessorySlotNet.Install();
    }

    internal static ModAccessorySlotPlayer Of(Player player)
    {
        ModAccessorySlotPlayer? slots = Players.Get(player.Pointer);
        if (slots != null && slots.SlotCount >= Loader.TotalCount)
            return slots;
        slots = new ModAccessorySlotPlayer(player);
        Players.Set(player.Pointer, slots);
        return slots;
    }

    internal static GameArray<Item> NewItems(int length, out GameHandle? pin)
    {
        GameArray<Item> array = GameArray<Item>.New(Item.Class, length);
        pin = new GameHandle(array.Pointer);
        for (int i = 0; i < length; i++)
            array[i] = Item.New();
        return array;
    }

    internal static Item Air()
    {
        Item item = Item.New();
        _ = new GameHandle(item.Pointer);
        return item;
    }

    internal static bool WithPlayer(Player player, Func<bool> call)
    {
        Player previous = Main.CurrentPlayerOverride;
        Main.CurrentPlayerOverride = player;
        try { return call(); }
        finally { Main.CurrentPlayerOverride = previous; }
    }

    internal static void WithPlayer(Player player, Action call)
    {
        Player previous = Main.CurrentPlayerOverride;
        Main.CurrentPlayerOverride = player;
        try { call(); }
        finally { Main.CurrentPlayerOverride = previous; }
    }

    internal static void Scope(int slot, Action call)
    {
        int previousSlot = scopeSlot;
        bool previous = scoped;
        scopeSlot = slot;
        scoped = true;
        try { call(); }
        finally
        {
            scopeSlot = previousSlot;
            scoped = previous;
        }
    }

    internal static void ApplyEquipFunctional(Player player, Item item, bool hidden)
    {
        var hide = player.hideVisibleAccessory;
        bool previous = hide[FunctionalProxySlot];
        hide[FunctionalProxySlot] = hidden;
        try { player.ApplyEquipFunctional(FunctionalProxySlot, item); }
        finally { hide[FunctionalProxySlot] = previous; }
    }

    internal static void ApplyEquipVanity(Player player, Item item) => player.ApplyEquipVanity(VanityProxySlot, item);

    internal static void UpdateVisibleAccessories(Player player, Item item, bool hidden)
    {
        if (item.IsAir)
            return;
        if (player.eocDash > 0 && player.shield == -1 && item.shieldSlot != -1)
        {
            player.shield = item.shieldSlot;
            if (player.cShieldFallback != -1)
                player.cShield = player.cShieldFallback;
        }
        if (player.shieldRaised && player.shield == -1 && item.shieldSlot != -1)
        {
            player.shield = item.shieldSlot;
            if (player.cShieldFallback != -1)
                player.cShield = player.cShieldFallback;
        }
        if (player.ItemIsVisuallyIncompatible(item))
            return;
        if (item.wingSlot > 0)
        {
            if (hidden && (player.velocity.Y == 0f || player.mount.Active))
                return;
            player.wings = item.wingSlot;
        }
        if (!hidden)
            player.UpdateVisibleAccessory(FunctionalProxySlot, item);
    }

    internal static bool HasConflict(Player player, Item item, int slot, int context)
    {
        if (item.IsAir || Math.Abs(context) == ItemSlot.Context.EquipDye)
            return false;
        ModAccessorySlotPlayer slots = Of(player);
        int count = slots.SlotCount;
        bool vanity = slot >= count;
        int own = slot % Math.Max(count, 1);
        var armor = player.armor;
        int sameStart = vanity ? 13 : 3, otherStart = vanity ? 3 : 13;
        for (int i = 0; i < 7; i++)
        {
            Item same = armor[sameStart + i];
            if (!same.IsAir && !ItemSlot.CanEquipBothAccessories(same, item, vanity))
                return true;
            Item other = armor[otherStart + i];
            if (!other.IsAir && SameItem(other, item))
                return true;
        }
        for (int j = 0; j < count; j++)
        {
            Item same = slots.exAccessorySlot[vanity ? j + count : j];
            if (j != own && !same.IsAir && !ItemSlot.CanEquipBothAccessories(same, item, vanity))
                return true;
            Item other = slots.exAccessorySlot[vanity ? j : j + count];
            if (!other.IsAir && SameItem(other, item))
                return true;
        }
        return false;
    }

    static bool ConflictsWithModSlots(GameArray<Item> inv, int context, Item item)
    {
        if (context is not (ItemSlot.Context.EquipAccessory or ItemSlot.Context.EquipAccessoryVanity))
            return false;
        Player player = Main.LocalPlayer;
        if (inv.Pointer != player.armor.Pointer)
            return false;
        ModAccessorySlotPlayer slots = Of(player);
        int count = slots.SlotCount;
        bool vanity = context == ItemSlot.Context.EquipAccessoryVanity;
        for (int j = 0; j < count; j++)
        {
            Item same = slots.exAccessorySlot[vanity ? j + count : j];
            if (!same.IsAir && !ItemSlot.CanEquipBothAccessories(same, item, vanity))
                return true;
            Item other = slots.exAccessorySlot[vanity ? j : j + count];
            if (!other.IsAir && SameItem(other, item))
                return true;
        }
        return false;
    }

    static bool SameItem(Item equipped, Item item)
    {
        if (equipped.type == item.type)
            return true;
        int a = ID.ItemID.Sets.AccessoryIncompatibilityType[item.type];
        return a != -1 && a == ID.ItemID.Sets.AccessoryIncompatibilityType[equipped.type];
    }

    static void UpdateEquips(On_Player.orig_UpdateEquips orig, Player self, int i)
    {
        orig(self, i);
        try
        {
            Of(self).UpdateEquips();
            AccessorySlotNet.AfterUpdateEquips(self);
        }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot.ApplyEquipEffects", e); }
    }

    static void UpdateVisibleAccessories(On_Player.orig_UpdateVisibleAccessories orig, Player self)
    {
        orig(self);
        try
        {
            ModAccessorySlotPlayer slots = Of(self);
            slots.UpdateVisibleAccessories();
            slots.UpdateVisibleVanityAccessories();
        }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot visible accessories", e); }
    }

    static void UpdateDyes(On_Player.orig_UpdateDyes orig, Player self)
    {
        orig(self);
        try
        {
            ModAccessorySlotPlayer slots = Of(self);
            slots.UpdateDyes(false);
            slots.UpdateDyes(true);
        }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot dyes", e); }
    }

    static void TrySwitchingLoadout(On_Player.orig_TrySwitchingLoadout orig, Player self, int loadoutIndex)
    {
        int previous = self.CurrentLoadoutIndex;
        orig(self, loadoutIndex);
        int current = self.CurrentLoadoutIndex;
        if (previous == current)
            return;
        try { Of(self).OnEquipmentLoadoutSwitched(previous, current); }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot loadouts", e); }
    }

    static void DropItems(On_Player.orig_DropItems orig, Player self, bool gemsOnly)
    {
        if (!gemsOnly)
        {
            try { Of(self).DropItems(self.GetSource_FromThis()); }
            catch (Exception e) { ItemHooks.Report("ModAccessorySlot drops", e); }
        }
        orig(self, gemsOnly);
    }

    static void EnterWorld(On_Player_Hooks.orig_EnterWorld orig, int playerIndex)
    {
        orig(playerIndex);
        Player player = Main.player[playerIndex];
        if (player.IsNull || playerIndex != Main.myPlayer)
            return;
        try { Of(player).OnEnterWorld(); }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot enter world", e); }
    }

    static void InternalSavePlayerFile(On_Player.orig_InternalSavePlayerFile orig, PlayerFileData playerFile)
    {
        orig(playerFile);
        try
        {
            Player player = playerFile.IsNull ? default : playerFile.Player;
            if (player.IsNull || PlayerHooks.DataFile(playerFile) is not string file)
                return;
            JsonObject all = PlayerHooks.Read(file);
            ModAccessorySlotPlayer slots = Of(player);
            if (slots.SlotCount > 0)
            {
                var tag = new TagCompound();
                slots.SaveData(tag);
                all[SaveKey] = JsonNode.Parse(TagJson.Write(tag));
            }
            else if (all.ContainsKey(SaveKey)) all.Remove(SaveKey);
            else return;
            if (all.Count > 0) File.WriteAllText(file, all.ToJsonString());
            else File.Delete(file);
        }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot save", e); }
    }

    static PlayerFileData LoadPlayer(On_Player.orig_LoadPlayer orig, string? playerPath, bool cloudSave)
    {
        PlayerFileData data = orig(playerPath, cloudSave);
        try
        {
            if (data.IsNull || PlayerHooks.DataFile(data) is not string file)
                return data;
            Player player = data.Player;
            if (player.IsNull)
                return data;
            Players.Set(player.Pointer, null);
            ModAccessorySlotPlayer slots = Of(player);
            if (PlayerHooks.Read(file)[SaveKey] is JsonObject saved)
                slots.LoadData(TagJson.Read(saved.ToJsonString()));
        }
        catch (Exception e) { ItemHooks.Report("ModAccessorySlot load", e); }
        return data;
    }

    static int PickItemMovementAction(On_ItemSlot.orig_PickItemMovementAction orig, GameArray<Item> inv, int context, int slot, Item checkItem, bool backwardCheck, int initialSlot)
    {
        int action = orig(inv, context, slot, checkItem, backwardCheck, initialSlot);
        if (action == -1 || checkItem.IsNull || checkItem.IsAir)
            return action;
        if (!scoped)
        {
            try { return ConflictsWithModSlots(inv, context, checkItem) ? -1 : action; }
            catch (Exception e)
            {
                ItemHooks.Report("ModAccessorySlot vanilla slot check", e);
                return action;
            }
        }
        if (context is not (ItemSlot.Context.EquipAccessory or ItemSlot.Context.EquipAccessoryVanity or ItemSlot.Context.EquipDye))
            return action;
        try
        {
            Player player = Main.LocalPlayer;
            ModAccessorySlotPlayer slots = Of(player);
            int index = context == ItemSlot.Context.EquipAccessoryVanity ? scopeSlot + slots.SlotCount : scopeSlot;
            if (!Loader.CanPlayerAcceptItem(player, scopeSlot, checkItem, -context))
                return -1;
            if (HasConflict(player, checkItem, index, -context))
                return -1;
            return action;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModAccessorySlot.CanAcceptItem", e);
            return -1;
        }
    }

    static Item ArmorSwap(On_ItemSlot.orig_ArmorSwap orig, Item item, out bool success, out int targetSlot)
    {
        if (TryModSwap(item, out Item result))
        {
            success = true;
            targetSlot = FunctionalProxySlot;
            return result;
        }
        return orig(item, out success, out targetSlot);
    }

    static bool TryModSwap(Item item, out Item result)
    {
        result = item;
        try
        {
            if (item.IsNull || item.IsAir || !item.accessory || item.vanity || item.stack != 1)
                return false;
            Player player = Main.LocalPlayer;
            ModAccessorySlotPlayer slots = Of(player);
            int count = slots.SlotCount;
            if (count == 0)
                return false;
            int target = -1;
            for (int j = 0; j < count; j++)
            {
                Item equipped = slots.exAccessorySlot[j];
                if (!equipped.IsAir && !ItemSlot.CanEquipBothAccessories(equipped, item, false))
                {
                    target = j;
                    break;
                }
            }
            if (target < 0)
            {
                var armor = player.armor;
                for (int i = 3; i < 10; i++)
                {
                    Item equipped = armor[i];
                    if (!equipped.IsAir && !ItemSlot.CanEquipBothAccessories(equipped, item, false))
                        return false;
                }
                int swapTo = -1;
                Loader.ModifyDefaultSwapSlot(item, ref swapTo);
                if (swapTo < 20)
                    return false;
                target = swapTo - 20;
            }
            if (!Loader.ModdedIsSpecificItemSlotUnlockedAndUsable(target, player, vanity: false) ||
                !Loader.CanPlayerAcceptItem(player, target, item, ItemSlot.Context.ModdedAccessorySlot) ||
                HasConflict(player, item, target, ItemSlot.Context.ModdedAccessorySlot) ||
                !ItemEquipHooks.CanEquipAccessory(item, target, true))
                return false;
            result = slots.exAccessorySlot[target];
            slots.exAccessorySlot[target] = item;
            return true;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModAccessorySlot.ModifyDefaultSwapSlot", e);
            result = item;
            return false;
        }
    }
}
