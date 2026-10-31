using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemHooks
{
    static readonly object Gate = new();
    static readonly HashSet<string> Installed = new();
    static readonly HashSet<string> Reported = new();
    static readonly Dictionary<int, IntPtr> UsedBy = new();

    static Hook? canUse, startUse, ownerOnly, animation, useStyle, holdStyle, updateEquips;

    public static void Install(Type type, Type baseType)
    {
        bool Has(string name) => Overrides(type, baseType, name);
        if (Has(nameof(ModItem.CanUseItem))) WantCanUse();
        if (Has(nameof(ModItem.UseItem)))
        {
            Once("OwnerOnly", () => ownerOnly = Raw("void ItemCheck_OwnerOnlyCode(ref Player.ItemCheckContext context, Item sItem, int weaponDamage, Rectangle heldItemFrame)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, int, Rectangle, IntPtr, void>)&OwnerOnlyCode));
            Once("StartUse", () => startUse = Raw("void ItemCheck_StartActualUse(Item sItem)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&StartActualUse));
        }
        if (Has(nameof(ModItem.UseAnimation)))
            Once("Animation", () => animation = Raw("void ApplyItemAnimation(Item sItem)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&ApplyItemAnimation));
        if (Has(nameof(ModItem.UseStyle)) || Has(nameof(ModItem.HoldItem)) || Has(nameof(ModItem.HoldoutOffset)))
            Once("UseStyle", () => useStyle = Raw("void ItemCheck_ApplyUseStyle(float mountOffset, Item sItem, Rectangle heldItemFrame)",
                (IntPtr)(delegate* unmanaged<IntPtr, float, IntPtr, Rectangle, IntPtr, void>)&ApplyUseStyle));
        if (Has(nameof(ModItem.HoldStyle)) || Has(nameof(ModItem.HoldItem)) || Has(nameof(ModItem.HoldoutOffset)))
            Once("HoldStyle", () => holdStyle = Raw("void ItemCheck_ApplyHoldStyle(float mountOffset, Item sItem, Rectangle heldItemFrame)",
                (IntPtr)(delegate* unmanaged<IntPtr, float, IntPtr, Rectangle, IntPtr, void>)&ApplyHoldStyle));
        if (Has(nameof(ModItem.UpdateInventory)))
            Once("Inventory", () => updateEquips = Raw("void UpdateEquips(int i)",
                (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, void>)&UpdateEquips));
        ItemHealHooks.Install(Has);
        ItemPickupHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemDrawHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemOpenHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemNetHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemTooltipHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemEquipHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemCombatHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemShootHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemHitHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemPvpHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemTimingHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemFrameHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemManaHooks.Install(Has, baseType == typeof(GlobalItem));
        FishingLineHooks.Install(Has);
        ItemLootHooks.Install(Has, baseType == typeof(GlobalItem));
        PrefixLoader.WantItemHooks(Has);
    }

    internal static void WantCanUse() =>
        Once("CanUse", () => canUse = Raw("bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte, IntPtr, byte>)&CheckCanUse));

    static readonly Dictionary<(Type, string), bool> OverrideCache = new();

    internal static bool Wants(GlobalItem global, string name)
    {
        var key = (global.GetType(), name);
        lock (OverrideCache)
        {
            if (OverrideCache.TryGetValue(key, out bool wants)) return wants;
            return OverrideCache[key] = Overrides(key.Item1, typeof(GlobalItem), name);
        }
    }

    internal static GlobalItem?[] GlobalsOf(Item item) => ItemLoader.Globals.Slots(item) ?? Array.Empty<GlobalItem?>();

    internal static bool Overrides(Type type, Type baseType, string name) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.Name == name && m.DeclaringType != baseType && m.GetBaseDefinition().DeclaringType == baseType);

    internal static void Once(string key, Action install)
    {
        lock (Gate)
        {
            if (Installed.Add(key)) install();
        }
    }

    internal static Hook? Raw(string signature, IntPtr replacement) => Raw("Terraria", "Player", signature, replacement);

    internal static Hook? Raw(string ns, string type, string signature, IntPtr replacement)
    {
        try
        {
            return Hook.Install(ns, type, signature, replacement);
        }
        catch (Exception e)
        {
            Log.Error($"item hooks: {type}.{signature} is not hooked: {e.Message}");
            return null;
        }
    }

    internal static void Report(string who, Exception e)
    {
        bool first;
        lock (Reported) first = Reported.Add(who + "|" + e.GetType().FullName);
        if (first) Log.Error($"{who} threw: {e}");
    }

    internal static long Original(ref GameCall call, Hook hook, string what)
    {
        try { return call.Invoke(hook.Original); }
        catch (GameException e)
        {
            Report(what, e);
            return 0;
        }
    }

    internal static ModItem? ModItemOf(Item item) => ItemLoader.Get(item.type) != null ? ItemLoader.InstanceOf(item) : null;

    [UnmanagedCallersOnly]
    static byte CheckCanUse(IntPtr self, IntPtr itemPointer, byte ignoreCursed, IntPtr method)
    {
        Hook? hook = canUse;
        if (hook == null) return 1;
        bool allowed = true;
        try { allowed = CanUseItem(new Item(itemPointer), new Player(self)); }
        catch (Exception e) { Report("CanUseItem", e); }
        if (!allowed) return 0;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(itemPointer);
        call.Arg(ignoreCursed);
        call.Arg(method);
        return (byte)(Original(ref call, hook, "Player.ItemCheck_CheckCanUse_Inner") & 0xFF);
    }

    static bool CanUseItem(Item item, Player player)
    {
        bool allowed = PlayerLoader.All(player, nameof(ModPlayer.CanUseItem), m => m.CanUseItem(item));
        if (ModItemOf(item) is ModItem m)
        {
            try { allowed &= m.CanUseItem(player); }
            catch (Exception e) { Report($"{m.FullName}.CanUseItem", e); }
        }
        foreach (GlobalItem? g in ItemLoader.Globals.Slots(item) ?? Array.Empty<GlobalItem?>())
        {
            if (g == null) continue;
            try { allowed &= g.CanUseItem(item, player); }
            catch (Exception e) { Report($"{g.FullName}.CanUseItem", e); }
        }
        return allowed;
    }

    [UnmanagedCallersOnly]
    static void StartActualUse(IntPtr self, IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = startUse;
        if (hook == null) return;
        Call(hook, self, itemPointer, method, "Player.ItemCheck_StartActualUse");
        try
        {
            var player = new Player(self);
            if (UseItem(new Item(itemPointer), player) != true) return;
            lock (UsedBy) UsedBy[player.whoAmI] = itemPointer;
        }
        catch (Exception e) { Report("UseItem", e); }
    }

    static bool? UseItem(Item item, Player player)
    {
        bool? result = null;
        foreach (GlobalItem? g in ItemLoader.Globals.Slots(item) ?? Array.Empty<GlobalItem?>())
        {
            if (g == null) continue;
            try
            {
                bool? used = g.UseItem(item, player);
                if (used.HasValue && result != false) result = used.Value;
            }
            catch (Exception e) { Report($"{g.FullName}.UseItem", e); }
        }
        if (ModItemOf(item) is ModItem m)
        {
            try
            {
                bool? used = m.UseItem(player);
                if (used.HasValue && result != false) result = used.Value;
            }
            catch (Exception e) { Report($"{m.FullName}.UseItem", e); }
        }
        return result;
    }

    [UnmanagedCallersOnly]
    static void OwnerOnlyCode(IntPtr self, IntPtr context, IntPtr itemPointer, int weaponDamage, Rectangle heldItemFrame, IntPtr method)
    {
        Hook? hook = ownerOnly;
        if (hook == null) return;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(context);
        call.Arg(itemPointer);
        call.Arg(weaponDamage);
        call.Arg(heldItemFrame);
        call.Arg(method);
        Original(ref call, hook, "Player.ItemCheck_OwnerOnlyCode");
        try
        {
            var player = new Player(self);
            IntPtr used;
            lock (UsedBy)
            {
                if (UsedBy.Count == 0 || !UsedBy.Remove(player.whoAmI, out used)) return;
            }
            if (player.ItemTimeIsZero && player.itemAnimation > 0) player.ApplyItemTime(new Item(used));
        }
        catch (Exception e)
        {
            Report("UseItem (item time)", e);
        }
    }

    [UnmanagedCallersOnly]
    static void ApplyItemAnimation(IntPtr self, IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = animation;
        if (hook == null) return;
        try
        {
            var item = new Item(itemPointer);
            var player = new Player(self);
            if (ModItemOf(item) is ModItem m)
            {
                try { m.UseAnimation(player); }
                catch (Exception e) { Report($"{m.FullName}.UseAnimation", e); }
            }
            foreach (GlobalItem? g in ItemLoader.Globals.Slots(item) ?? Array.Empty<GlobalItem?>())
            {
                if (g == null) continue;
                try { g.UseAnimation(item, player); }
                catch (Exception e) { Report($"{g.FullName}.UseAnimation", e); }
            }
        }
        catch (Exception e) { Report("UseAnimation", e); }
        Call(hook, self, itemPointer, method, "Player.ApplyItemAnimation");
    }

    [UnmanagedCallersOnly]
    static void ApplyUseStyle(IntPtr self, float mountOffset, IntPtr itemPointer, Rectangle heldItemFrame, IntPtr method)
    {
        Hook? hook = useStyle;
        if (hook == null) return;
        Styled(hook, self, mountOffset, itemPointer, heldItemFrame, method, "Player.ItemCheck_ApplyUseStyle");
        try { Style(new Player(self), new Item(itemPointer), heldItemFrame, use: true); }
        catch (Exception e) { Report("UseStyle", e); }
    }

    [UnmanagedCallersOnly]
    static void ApplyHoldStyle(IntPtr self, float mountOffset, IntPtr itemPointer, Rectangle heldItemFrame, IntPtr method)
    {
        Hook? hook = holdStyle;
        if (hook == null) return;
        Styled(hook, self, mountOffset, itemPointer, heldItemFrame, method, "Player.ItemCheck_ApplyHoldStyle");
        try { Style(new Player(self), new Item(itemPointer), heldItemFrame, use: false); }
        catch (Exception e) { Report("HoldStyle", e); }
    }

    static void Styled(Hook hook, IntPtr self, float mountOffset, IntPtr itemPointer, Rectangle frame, IntPtr method, string what)
    {
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(mountOffset);
        call.Arg(itemPointer);
        call.Arg(frame);
        call.Arg(method);
        Original(ref call, hook, what);
    }

    internal static long Call(Hook hook, IntPtr self, IntPtr argument, IntPtr method, string what)
    {
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(argument);
        call.Arg(method);
        return Original(ref call, hook, what);
    }

    internal static long Call(Hook hook, IntPtr self, IntPtr method, string what)
    {
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(method);
        return Original(ref call, hook, what);
    }

    static void Style(Player player, Item item, Rectangle frame, bool use)
    {
        if (item.IsNull || item.type <= 0) return;
        ModItem? m = ModItemOf(item);
        GlobalItem?[] globals = ItemLoader.Globals.Slots(item) ?? Array.Empty<GlobalItem?>();
        if (m != null)
        {
            try { ApplyHoldout(player, m.HoldoutOffset()); }
            catch (Exception e) { Report($"{m.FullName}.HoldoutOffset", e); }
        }
        foreach (GlobalItem? g in globals)
        {
            if (g == null) continue;
            try { ApplyHoldout(player, g.HoldoutOffset(item.type)); }
            catch (Exception e) { Report($"{g.FullName}.HoldoutOffset", e); }
        }
        if (m != null)
        {
            try
            {
                if (use) m.UseStyle(player, frame);
                else m.HoldStyle(player, frame);
                m.HoldItem(player);
            }
            catch (Exception e) { Report($"{m.FullName}.{(use ? "UseStyle" : "HoldStyle")}", e); }
        }
        foreach (GlobalItem? g in globals)
        {
            if (g == null) continue;
            try
            {
                if (use) g.UseStyle(item, player, frame);
                else g.HoldStyle(item, player, frame);
                g.HoldItem(item, player);
            }
            catch (Exception e) { Report($"{g.FullName}.{(use ? "UseStyle" : "HoldStyle")}", e); }
        }
    }

    static void ApplyHoldout(Player player, Vector2? offset)
    {
        if (offset is not Vector2 o || (o.X == 0 && o.Y == 0)) return;
        float x = o.X * player.direction, y = o.Y * player.gravDir;
        float cos = MathF.Cos(player.itemRotation), sin = MathF.Sin(player.itemRotation);
        Vector2 location = player.itemLocation;
        location.X += x * cos - y * sin;
        location.Y += x * sin + y * cos;
        player.itemLocation = location;
    }

    [UnmanagedCallersOnly]
    static void UpdateEquips(IntPtr self, int i, IntPtr method)
    {
        Hook? hook = updateEquips;
        if (hook == null) return;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(i);
        call.Arg(method);
        Original(ref call, hook, "Player.UpdateEquips");
        try
        {
            var player = new Player(self);
            var inventory = player.inventory;
            int count = Math.Min(inventory.Length, 58);
            for (int slot = 0; slot < count; slot++)
            {
                Item item = inventory[slot];
                if (item.IsNull || item.type <= 0 || item.stack <= 0) continue;
                UpdateInventory(item, player);
            }
        }
        catch (Exception e)
        {
            Report("UpdateInventory", e);
        }
    }

    static void UpdateInventory(Item item, Player player)
    {
        if (ModItemOf(item) is ModItem m)
        {
            try { m.UpdateInventory(player); }
            catch (Exception e) { Report($"{m.FullName}.UpdateInventory", e); }
        }
        foreach (GlobalItem? g in ItemLoader.Globals.Slots(item) ?? Array.Empty<GlobalItem?>())
        {
            if (g == null) continue;
            try { g.UpdateInventory(item, player); }
            catch (Exception e) { Report($"{g.FullName}.UpdateInventory", e); }
        }
    }
}
