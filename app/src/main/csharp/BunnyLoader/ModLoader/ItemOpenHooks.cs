using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.ID;

namespace Terraria.ModLoader;

internal static unsafe class ItemOpenHooks
{
    static Hook? grant, canBeOpened, category;

    static bool wanted, globalWanted;

    static HashSet<int> openable = new();

    static HashSet<int> addedByMods = new();

    public static void Install(Func<string, bool> has, bool global)
    {
        if (!has(nameof(ModItem.CanRightClick)) && !has(nameof(ModItem.RightClick)) && !has(nameof(ModItem.ConsumeItem))) return;
        wanted = true;
        if (global) globalWanted = true;
        ItemHooks.Once("Open", () =>
        {
            grant = ItemHooks.Raw("Terraria.UI", "ItemSlot", "bool TryOpenContainer_GrantItems(Item item, Player player)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>)&TryOpenContainer);
            canBeOpened = ItemHooks.Raw("", "GUIPageOptions", "bool CanBeOpened(Item SelectedItem)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte>)&CanBeOpened);
            category = ItemHooks.Raw("", "VirtualControllerInputState", "VirtualControllerInputState.Category GetItemCategory(int item)",
                (IntPtr)(delegate* unmanaged<int, IntPtr, int>)&GetItemCategory);
        });
    }

    public static void ContentReady()
    {
        if (!wanted) return;
        var open = new HashSet<int>();
        var added = new HashSet<int>();
        var bags = ItemID.Sets.OpenableBag;
        int last = bags.Length;
        Item sample = Item.New();
        using var handle = new GameHandle(sample.Pointer);
        for (int type = 1; type < last; type++)
        {
            bool vanilla = type < ItemID.Count;
            if (vanilla && !globalWanted) continue;
            if (!vanilla && ItemLoader.Get(type) == null) continue;
            try
            {
                sample.SetDefaults(type, default);
                if (!CanRightClick(sample)) continue;
            }
            catch (Exception e)
            {
                ItemHooks.Report("CanRightClick", e);
                continue;
            }
            open.Add(type);
            if (bags[type]) continue;
            bags[type] = true;
            added.Add(type);
        }
        openable = open;
        addedByMods = added;
    }

    static bool CanRightClick(Item item)
    {
        if (item.IsNull || item.type <= 0 || item.stack <= 0) return false;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try
            {
                if (m.CanRightClick()) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.CanRightClick", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                if (g.CanRightClick(item)) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanRightClick", e); }
        }
        return false;
    }

    static void RightClick(Item item, Player player)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.RightClick(player); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.RightClick", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.RightClick(item, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.RightClick", e); }
        }
    }

    static bool ConsumeItem(Item item, Player player)
    {
        bool consume = true;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { consume &= m.ConsumeItem(player); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ConsumeItem", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { consume &= g.ConsumeItem(item, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ConsumeItem", e); }
        }
        return consume;
    }

    static void DevArmor(int type, Player player)
    {
        var sets = ItemID.Sets.BossBag;
        if (!sets[type] || ItemID.Sets.PreHardmodeLikeBossBag[type]) return;
        player.TryGettingDevArmor(player.GetItemSource_OpenItem(type));
    }

    [UnmanagedCallersOnly]
    static byte TryOpenContainer(IntPtr itemPointer, IntPtr playerPointer, IntPtr method)
    {
        Hook? hook = grant;
        if (hook == null) return 0;
        var item = new Item(itemPointer);
        bool ours = false;
        try
        {
            int type = item.IsNull ? 0 : item.type;
            bool modded = ItemLoader.Get(type) != null;
            ours = modded || addedByMods.Contains(type);
            if (ours || globalWanted)
            {
                var player = new Player(playerPointer);
                if (ours && !CanRightClick(item)) return 0;
                RightClick(item, player);
                if (modded)
                {
                    try { DevArmor(type, player); }
                    catch (Exception e) { ItemHooks.Report("RightClick (developer armor)", e); }
                }
                bool consume = ConsumeItem(item, player);
                if (ours) return consume ? (byte)1 : (byte)0;
                byte opened = Original(hook, itemPointer, playerPointer, method);
                return opened != 0 && consume ? (byte)1 : (byte)0;
            }
        }
        catch (Exception e)
        {
            ItemHooks.Report("RightClick", e);
            if (ours) return 0;
        }
        return Original(hook, itemPointer, playerPointer, method);
    }

    static byte Original(Hook hook, IntPtr item, IntPtr player, IntPtr method)
    {
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(item);
        call.Arg(player);
        call.Arg(method);
        return (byte)(ItemHooks.Original(ref call, hook, "ItemSlot.TryOpenContainer_GrantItems") & 0xFF);
    }

    [UnmanagedCallersOnly]
    static byte CanBeOpened(IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = canBeOpened;
        if (hook == null) return 0;
        try
        {
            var item = new Item(itemPointer);
            if (!item.IsNull && openable.Contains(item.type)) return 1;
        }
        catch (Exception e) { ItemHooks.Report("CanRightClick (open button)", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(itemPointer);
        call.Arg(method);
        return (byte)(ItemHooks.Original(ref call, hook, "GUIPageOptions.CanBeOpened") & 0xFF);
    }

    [UnmanagedCallersOnly]
    static int GetItemCategory(int type, IntPtr method)
    {
        Hook? hook = category;
        if (hook == null) return 0;
        if (openable.Contains(type)) return (int)global::VirtualControllerInputState.Category.NonFireItems;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(type);
        call.Arg(method);
        return (int)ItemHooks.Original(ref call, hook, "VirtualControllerInputState.GetItemCategory");
    }
}
