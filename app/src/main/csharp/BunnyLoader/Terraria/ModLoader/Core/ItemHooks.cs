using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using BunnyLoader.Hooks;
using BunnyLoader.Interop;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemHooks
{
    static readonly object Gate = new();
    static readonly HashSet<string> Installed = new();
    static readonly HashSet<string> Reported = new();
    static readonly Dictionary<int, IntPtr> UsedBy = new();

    static readonly int* AirScopes = (int*)NativeMemory.AllocZeroed(sizeof(int));

    static Hook? canUse, startUse, ownerOnly, animation, useStyle, holdStyle, updateEquips;

    [ThreadStatic] static IntPtr equipping;

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
        if (Has(nameof(ModItem.UpdateInventory)) || Has(nameof(ModItem.UpdateInfoAccessory)))
            Once("Inventory", () => updateEquips = Raw("void UpdateEquips(int i)",
                (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, void>)&UpdateEquips));
        if (Has(nameof(ModItem.UpdateInfoAccessory)))
            Once("InventoryInfo", () => On_Player.RefreshInfoAccsFromTeamPlayers += RefreshInfoAccsFromTeamPlayers);
        ItemExampleHooks.Install(Has);
        ItemVisibilityHooks.Install(Has);
        ItemStackHooks.Install(Has);
        ItemConsumeHooks.Install(Has);
        ItemHealHooks.Install(Has);
        ItemPickupHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemDrawHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemHeldDrawHooks.Install(Has);
        if (Has(nameof(ModItem.OnCraft))) RecipeLoader.WantCraft();
        ItemReforgeHooks.Install(Has);
        if (Has(nameof(ModItem.CaughtFishStack))) PlayerFishingHooks.WantCaught();
        if (Has(nameof(ModItem.ApplyPrefix))) PrefixLoader.WantApplyHook();
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
        ItemResearchHooks.Install(Has);
        ItemWorldHooks.Install(Has, baseType == typeof(GlobalItem));
        if (Has(nameof(ModItem.ModifyEquipTextureDraw))) EquipDrawHooks.Install();
        AnglerQuestHooks.Install(Has, baseType == typeof(GlobalItem));
        ItemLootHooks.Install(Has, baseType == typeof(GlobalItem));
        PrefixLoader.WantItemHooks(Has);
    }

    internal static void WantCanUse() =>
        Once("CanUse", () =>
        {
            canUse = Raw("bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte, IntPtr, byte>)&CheckCanUse);
            ProjectileGrappleHooks.WantQuickGrapple();
        });

    internal static bool ModCanUseItem(Item item, Player player) => canUse == null || CanUseItem(item, player);

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

    static readonly ConcurrentDictionary<(Type, Type), HashSet<string>> OverrideNames = new();

    internal static bool Overrides(Type type, Type baseType, string name) =>
        OverrideNames.GetOrAdd((type, baseType), static key => FindOverrides(key.Item1, key.Item2)).Contains(name);

    internal static bool ModOverrides(ModItem item, string name) => Overrides(item.GetType(), typeof(ModItem), name);

    internal static ModItem? Overrider(Item item, int type, string name) =>
        ItemLoader.Get(type) is ModItem template && ModOverrides(template, name) ? ItemLoader.InstanceOf(item) : null;

    internal static bool WantsType(int type, string name) =>
        ItemLoader.Globals.Overriding(name).Length > 0 || ItemLoader.Get(type) is ModItem template && ModOverrides(template, name);

    internal static GlobalItem? Applied(GlobalItem?[]? slots, short index) => slots != null && index < slots.Length ? slots[index] : null;

    internal static int Selected(Player player) => player.selectedItemState.selected;

    internal static void TypeGate<THook>(int arg, Func<int, bool> wanted) where THook : Delegate
    {
        HookGates.Table<THook>(() => HookGates.Field(arg, Item.Class, "type"), () => ItemLoader.ItemCount);
        HookGates.Want<THook>(wanted);
    }

    internal static Hook? RawGated<TGate>(string ns, string type, string signature, IntPtr replacement, int arg, Func<int, bool> wanted) where TGate : Delegate
    {
        TypeGate<TGate>(arg, wanted);
        Type? outer = HookGates.Installing;
        HookGates.Installing = typeof(TGate);
        try { return Raw(ns, type, signature, replacement); }
        finally { HookGates.Installing = outer; }
    }

    internal static void WantTurnToAir()
    {
        HookGates.Define<On_Item.hook_TurnToAir>(() => new GateSpec
        {
            Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = 0, FieldSize = 4, Address = (IntPtr)AirScopes,
        });
        HookGates.Want<On_Item.hook_TurnToAir>();
    }

    internal static void EnterAirScope() => Interlocked.Increment(ref *AirScopes);

    internal static void ExitAirScope() => Interlocked.Decrement(ref *AirScopes);

    static HashSet<string> FindOverrides(Type type, Type baseType)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.DeclaringType != baseType && method.GetBaseDefinition().DeclaringType == baseType) names.Add(method.Name);
        }
        return names;
    }

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

    internal static Hook? Raw(GameClass type, string signature, IntPtr replacement)
    {
        try
        {
            return Hook.Install(type, signature, replacement);
        }
        catch (Exception e)
        {
            Log.Error($"item hooks: {type.FullName}.{signature} is not hooked: {e.Message}");
            return null;
        }
    }

    internal static Hook? Gated(GameClass type, string signature, IntPtr replacement, int arg, int pointerOffset, int fieldOffset, int fieldSize)
    {
        try
        {
            return Hook.InstallGated(type, signature, replacement, arg, pointerOffset, fieldOffset, fieldSize);
        }
        catch (Exception e)
        {
            Log.Error($"item hooks: {type.FullName}.{signature} is not hooked: {e.Message}");
            return null;
        }
    }

    internal static Hook? Gated(GameClass type, string signature, IntPtr replacement, Func<GateSpec> spec)
    {
        GateSpec gate;
        try { gate = spec(); }
        catch (Exception e)
        {
            Log.Warn($"item hooks: {type.FullName}.{signature} is hooked without a gate: {e.Message}");
            return Raw(type, signature, replacement);
        }
        try
        {
            return Hook.InstallGated(type, signature, replacement, gate, out _);
        }
        catch (Exception e)
        {
            Log.Error($"item hooks: {type.FullName}.{signature} is not hooked: {e.Message}");
            return null;
        }
    }

    internal static int FieldOffset(GameClass type, string field)
    {
        int slot = -1;
        return GameInterop.Offset(ref slot, type, field);
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
        bool allowed = PlayerLoader.All(player, nameof(ModPlayer.CanUseItem), item, static (m, i) => m.CanUseItem(i));
        if (ModItemOf(item) is ModItem m)
        {
            try { allowed &= m.CanUseItem(player); }
            catch (Exception e) { Report($"{m.FullName}.CanUseItem", e); }
        }
        short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.CanUseItem));
        if (overriding.Length == 0) return allowed;
        GlobalItem?[]? slots = ItemLoader.Globals.Slots(item);
        foreach (short index in overriding)
        {
            if (Applied(slots, index) is not GlobalItem g) continue;
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
        IntPtr outer = equipping;
        equipping = self;
        try { Original(ref call, hook, "Player.UpdateEquips"); }
        finally { equipping = outer; }
        try { CarriedItems(new Player(self), static (item, type, player, overriding) => UpdateInventory(item, type, player, overriding), nameof(GlobalItem.UpdateInventory)); }
        catch (Exception e)
        {
            Report("UpdateInventory", e);
        }
    }

    static void RefreshInfoAccsFromTeamPlayers(On_Player.orig_RefreshInfoAccsFromTeamPlayers orig, Player self)
    {
        if (equipping != IntPtr.Zero && equipping == self.Pointer)
        {
            try { CarriedItems(self, static (item, _, player, overriding) => UpdateInfoAccessory(item, player, overriding), nameof(GlobalItem.UpdateInfoAccessory)); }
            catch (Exception e) { Report("UpdateInfoAccessory", e); }
        }
        orig(self);
    }

    static void CarriedItems(Player player, Action<Item, int, Player, short[]> visit, string globalHook)
    {
        short[] overriding = ItemLoader.Globals.Overriding(globalHook);
        var items = player.inventory;
        int count = Math.Min(items.Length, 58);
        bool voidBag = false;
        for (int slot = 0; slot < count; slot++)
        {
            Item item = items[slot];
            if (item.IsNull) continue;
            int type = item.type;
            if (type == 4131) voidBag = true;
            if (type <= 0 || item.stack <= 0) continue;
            visit(item, type, player, overriding);
        }
        if (!voidBag) return;
        var bank = player.bank4;
        if (bank.IsNull) return;
        var bag = bank.item;
        if (bag.IsNull) return;
        var works = ItemID.Sets.WorksInVoidBag;
        for (int slot = 0; slot < bag.Length; slot++)
        {
            Item item = bag[slot];
            if (item.IsNull) continue;
            int type = item.type;
            if (type <= 0 || item.stack <= 0 || (type <= ItemID.Count && !(!works.IsNull && (uint)type < (uint)works.Length && works[type]))) continue;
            visit(item, type, player, overriding);
        }
    }

    static void UpdateInventory(Item item, int type, Player player, short[] overriding)
    {
        if (Overrider(item, type, nameof(ModItem.UpdateInventory)) is ModItem m)
        {
            try { m.UpdateInventory(player); }
            catch (Exception e) { Report($"{m.FullName}.UpdateInventory", e); }
        }
        if (overriding.Length == 0) return;
        GlobalItem?[]? slots = ItemLoader.Globals.Slots(item);
        foreach (short index in overriding)
        {
            if (Applied(slots, index) is not GlobalItem g) continue;
            try { g.UpdateInventory(item, player); }
            catch (Exception e) { Report($"{g.FullName}.UpdateInventory", e); }
        }
    }

    internal static void UpdateInfoAccessory(Item item, Player player) =>
        UpdateInfoAccessory(item, player, ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateInfoAccessory)));

    internal static void UpdateInfoAccessory(Item item, Player player, short[] overriding)
    {
        if (item.IsNull) return;
        int type = item.type;
        if (type <= 0 || item.stack <= 0) return;
        if (Overrider(item, type, nameof(ModItem.UpdateInfoAccessory)) is ModItem m)
        {
            try { m.UpdateInfoAccessory(player); }
            catch (Exception e) { Report($"{m.FullName}.UpdateInfoAccessory", e); }
        }
        if (overriding.Length == 0) return;
        GlobalItem?[]? slots = ItemLoader.Globals.Slots(item);
        foreach (short index in overriding)
        {
            if (Applied(slots, index) is not GlobalItem g) continue;
            try { g.UpdateInfoAccessory(item, player); }
            catch (Exception e) { Report($"{g.FullName}.UpdateInfoAccessory", e); }
        }
    }
}
