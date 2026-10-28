using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemPickupHooks
{
    static Hook? accept, grabItems, grabRange, pullCommon, pullPickup, pickup, spaceRef, spaceValue;

    static bool globalAccept, globalRange, globalPull, globalPickup, globalSpace;

    static IntPtr grabbing;

    static readonly Dictionary<IntPtr, int> WorldIndex = new();

    public static void Install(Func<string, bool> has, bool global)
    {
        bool canPickup = has(nameof(ModItem.CanPickup)), range = has(nameof(ModItem.GrabRange));
        if (canPickup || range)
        {
            if (global) globalAccept = true;
            ItemHooks.Once("CanAccept", () => accept = ItemHooks.Raw("bool CanAcceptItemIntoInventory(Item item)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>)&CanAccept));
        }
        if (range)
        {
            if (global) globalRange = true;
            ItemHooks.Once("GrabItems", () => grabItems = ItemHooks.Raw("void GrabItems(int i)",
                (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, void>)&GrabItems));
            ItemHooks.Once("GrabRange", () => grabRange = ItemHooks.Raw("int GetItemGrabRange(WorldItem item)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>)&GetItemGrabRange));
        }
        if (has(nameof(ModItem.GrabStyle)))
        {
            if (global) globalPull = true;
            ItemHooks.Once("PullCommon", () => pullCommon = ItemHooks.Raw("void PullItem_Common(WorldItem itemToPickUp, float xPullSpeed)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, float, IntPtr, void>)&PullCommon));
            ItemHooks.Once("PullPickup", () => pullPickup = ItemHooks.Raw("void PullItem_Pickup(WorldItem itemToPickUp, float speed, int acc)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, float, int, IntPtr, void>)&PullPickup));
        }
        if (has(nameof(ModItem.OnPickup)))
        {
            if (global) globalPickup = true;
            ItemHooks.Once("Pickup", () => pickup = ItemHooks.Raw("void PickupItem(WorldItem itemToPickUp)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&PickupItem));
        }
        if (has(nameof(ModItem.ItemSpace)))
        {
            if (global) globalSpace = true;
            ItemHooks.Once("SpaceRef", () => spaceRef = ItemHooks.Raw("void ItemSpace(Item newItem, ref Player.ItemSpaceStatus result)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&ItemSpaceRef));
            if (sizeof(Player.ItemSpaceStatus) <= sizeof(long))
                ItemHooks.Once("SpaceValue", () => spaceValue = ItemHooks.Raw("Player.ItemSpaceStatus ItemSpace(Item newItem)",
                    (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, long>)&ItemSpaceValue));
        }
    }

    static bool Wanted(bool global, Item item) => global || (!item.IsNull && ItemLoader.Get(item.type) != null);

    public static WorldItem WorldItemOf(Item item)
    {
        if (item.IsNull) return default;
        var all = Main.item;
        if (all.IsNull) return default;
        lock (WorldIndex)
        {
            if (WorldIndex.TryGetValue(item.Pointer, out int cached) && cached < all.Length)
            {
                WorldItem w = all[cached];
                if (!w.IsNull && w.inner == item) return w;
            }
            WorldIndex.Clear();
            WorldItem found = default;
            for (int i = 0; i < all.Length; i++)
            {
                WorldItem w = all[i];
                if (w.IsNull) continue;
                Item inner = w.inner;
                if (inner.IsNull) continue;
                WorldIndex[inner.Pointer] = i;
                if (inner == item) found = w;
            }
            return found;
        }
    }

    static bool Until(Item item, string name, bool stop, Func<GlobalItem, bool> global, Func<ModItem, bool> mod)
    {
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                if (global(g) == stop) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.{name}", e); }
        }
        if (ItemHooks.ModItemOf(item) is not ModItem m) return false;
        try { return mod(m) == stop; }
        catch (Exception e)
        {
            ItemHooks.Report($"{m.FullName}.{name}", e);
            return false;
        }
    }

    static bool CanPickup(Item item, Player player) =>
        !Until(item, nameof(ModItem.CanPickup), false, g => g.CanPickup(item, player), m => m.CanPickup(player));

    static bool OnPickup(Item item, Player player) =>
        !Until(item, nameof(ModItem.OnPickup), false, g => g.OnPickup(item, player), m => m.OnPickup(player));

    static bool GrabStyle(Item item, Player player) =>
        Until(item, nameof(ModItem.GrabStyle), true, g => g.GrabStyle(item, player), m => m.GrabStyle(player));

    static bool ItemSpace(Item item, Player player)
    {
        if (item.uniqueStack && player.HasItem(item.type)) return false;
        return Until(item, nameof(ModItem.ItemSpace), true, g => g.ItemSpace(item, player), m => m.ItemSpace(player));
    }

    static int GrabRange(Item item, Player player, int baseRange)
    {
        int range = baseRange;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.GrabRange(player, ref range); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.GrabRange", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.GrabRange(item, player, ref range); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.GrabRange", e); }
        }
        return range;
    }

    [UnmanagedCallersOnly]
    static byte CanAccept(IntPtr self, IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = accept;
        if (hook == null) return 1;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(itemPointer);
        call.Arg(method);
        if ((ItemHooks.Original(ref call, hook, "Player.CanAcceptItemIntoInventory") & 0xFF) == 0) return 0;
        try
        {
            var item = new Item(itemPointer);
            if (!Wanted(globalAccept, item)) return 1;
            WorldItem w = WorldItemOf(item);
            if (w.IsNull) return 1;
            var player = new Player(self);
            if (!CanPickup(item, player)) return 0;
            if (grabItems == null || grabbing != self) return 1;
            return Reach(player, w, item) ? (byte)1 : (byte)0;
        }
        catch (Exception e)
        {
            ItemHooks.Report("CanPickup/GrabRange", e);
            return 1;
        }
    }

    static bool Reach(Player player, WorldItem w, Item item)
    {
        int vanilla = player.GetItemGrabRange(item);
        int range = GrabRange(item, player, vanilla);
        if (range == vanilla) return true;
        var at = player.position;
        var to = w.position;
        int px = (int)at.X, py = (int)at.Y, pw = player.width, ph = player.height;
        int hx = (int)to.X, hy = (int)to.Y, hw = w.width, hh = w.height;
        bool Meets(int r) => px - r < hx + hw && hx < px + pw + r && py - r < hy + hh && hy < py + ph + r;
        if (Meets(0)) return true;
        if (range < vanilla) return Meets(range);
        if (!Meets(range) || Meets(vanilla)) return true;
        IntPtr outer = grabbing;
        grabbing = IntPtr.Zero;
        try { Pull(player, w, item); }
        finally { grabbing = outer; }
        return true;
    }

    static void Pull(Player player, WorldItem w, Item item)
    {
        Player.ItemSpaceStatus status = player.ItemSpace(w);
        if (!player.CanPullItem(w, status)) return;
        w.shimmered = false;
        w.beingGrabbed = true;
        bool fast = status.ItemIsGoingToVoidVault || ItemID.Sets.NebulaPickup[item.type] || (player.goldRing && item.IsACoin);
        if (fast) player.PullItem_Pickup(w, 12f, 5);
        else player.PullItem_Common(w, 0.75f);
    }

    [UnmanagedCallersOnly]
    static void GrabItems(IntPtr self, int i, IntPtr method)
    {
        Hook? hook = grabItems;
        if (hook == null) return;
        IntPtr outer = grabbing;
        grabbing = self;
        try
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(self);
            call.Arg(i);
            call.Arg(method);
            ItemHooks.Original(ref call, hook, "Player.GrabItems");
        }
        finally { grabbing = outer; }
    }

    [UnmanagedCallersOnly]
    static int GetItemGrabRange(IntPtr self, IntPtr worldItem, IntPtr method)
    {
        Hook? hook = grabRange;
        if (hook == null) return 0;
        int range = (int)ItemHooks.Call(hook, self, worldItem, method, "Player.GetItemGrabRange");
        try
        {
            Item item = new WorldItem(worldItem).inner;
            if (!Wanted(globalRange, item)) return range;
            return GrabRange(item, new Player(self), range);
        }
        catch (Exception e)
        {
            ItemHooks.Report("GrabRange", e);
            return range;
        }
    }

    [UnmanagedCallersOnly]
    static void PullCommon(IntPtr self, IntPtr worldItem, float xPullSpeed, IntPtr method)
    {
        Hook? hook = pullCommon;
        if (hook == null || Styled(self, worldItem)) return;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(worldItem);
        call.Arg(xPullSpeed);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "Player.PullItem_Common");
    }

    [UnmanagedCallersOnly]
    static void PullPickup(IntPtr self, IntPtr worldItem, float speed, int acc, IntPtr method)
    {
        Hook? hook = pullPickup;
        if (hook == null || Styled(self, worldItem)) return;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(worldItem);
        call.Arg(speed);
        call.Arg(acc);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "Player.PullItem_Pickup");
    }

    static bool Styled(IntPtr self, IntPtr worldItem)
    {
        try
        {
            Item item = new WorldItem(worldItem).inner;
            return Wanted(globalPull, item) && GrabStyle(item, new Player(self));
        }
        catch (Exception e)
        {
            ItemHooks.Report("GrabStyle", e);
            return false;
        }
    }

    [UnmanagedCallersOnly]
    static void PickupItem(IntPtr self, IntPtr worldItem, IntPtr method)
    {
        Hook? hook = pickup;
        if (hook == null) return;
        try
        {
            var w = new WorldItem(worldItem);
            Item item = w.inner;
            if (Wanted(globalPickup, item) && !OnPickup(item, new Player(self)))
            {
                w.TurnToAirAndSync();
                return;
            }
        }
        catch (Exception e) { ItemHooks.Report("OnPickup", e); }
        ItemHooks.Call(hook, self, worldItem, method, "Player.PickupItem");
    }

    [UnmanagedCallersOnly]
    static void ItemSpaceRef(IntPtr self, IntPtr itemPointer, IntPtr result, IntPtr method)
    {
        Hook? hook = spaceRef;
        if (hook == null) return;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(itemPointer);
        call.Arg(result);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "Player.ItemSpace");
        if (result == IntPtr.Zero) return;
        var status = (Player.ItemSpaceStatus*)result;
        try
        {
            if (!status->CanTakeItem && Allows(self, itemPointer)) status->CanTakeItem = true;
        }
        catch (Exception e) { ItemHooks.Report("ItemSpace", e); }
    }

    [UnmanagedCallersOnly]
    static long ItemSpaceValue(IntPtr self, IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = spaceValue;
        if (hook == null) return 0;
        long raw = ItemHooks.Call(hook, self, itemPointer, method, "Player.ItemSpace");
        var status = (Player.ItemSpaceStatus*)&raw;
        try
        {
            if (!status->CanTakeItem && Allows(self, itemPointer)) status->CanTakeItem = true;
        }
        catch (Exception e) { ItemHooks.Report("ItemSpace", e); }
        return raw;
    }

    static bool Allows(IntPtr self, IntPtr itemPointer)
    {
        var item = new Item(itemPointer);
        return Wanted(globalSpace, item) && ItemSpace(item, new Player(self));
    }
}
