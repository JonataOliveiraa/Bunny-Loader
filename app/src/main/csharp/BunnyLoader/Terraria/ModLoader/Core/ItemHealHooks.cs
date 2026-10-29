using System.Runtime.InteropServices;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class ItemHealHooks
{
    static Hook? quickHeal, quickMana, applyLifeAndMana, getItemToUse, tryChoosing, applyPotionDelay, addBuff, resetHunger;

    static IntPtr quickPlayer;
    static IntPtr selectingPlayer;
    static PotionScope? potion;

    sealed class PotionScope
    {
        public IntPtr Player;
        public IntPtr Item;
        public int Previous;
        public bool Blocked;
    }

    public static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.GetHealLife)) || has(nameof(ModItem.GetHealMana)))
        {
            ItemHooks.Once("QuickHeal", () => quickHeal = ItemHooks.Raw("void QuickHeal()",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, void>)&QuickHeal));
            ItemHooks.Once("QuickMana", () => quickMana = ItemHooks.Raw("void QuickMana()",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, void>)&QuickMana));
            ItemHooks.Once("ApplyLifeAndOrMana", () => applyLifeAndMana = ItemHooks.Raw("void ApplyLifeAndOrMana(Item item)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&ApplyLifeAndOrMana));
        }
        if (has(nameof(ModItem.GetHealLife)))
        {
            ItemHooks.Once("TryChoosingItem", () => tryChoosing = ItemHooks.Raw("void QuickHeal_GetItemToUse_TryChoosingItem(int lifeDifference, ref Item bestItem, ref int bestDifference, Item nextItem)",
                (IntPtr)(delegate* unmanaged<int, IntPtr*, int*, IntPtr, IntPtr, void>)&TryChoosingItem));
            ItemHooks.Once("GetItemToUse", () => getItemToUse = ItemHooks.Raw("Item QuickHeal_GetItemToUse()",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr>)&GetItemToUse));
        }
        if (has(nameof(ModItem.ModifyPotionDelay)) || has(nameof(ModItem.ApplyPotionDelay)))
        {
            ItemHooks.Once("AddBuff", () => addBuff = ItemHooks.Raw("void AddBuff(int type, int time, bool fromNetPvP)",
                (IntPtr)(delegate* unmanaged<IntPtr, int, int, byte, IntPtr, void>)&AddBuff));
            ItemHooks.Once("ResetHunger", () => resetHunger = ItemHooks.Raw("void TryToResetHungerToNeutral()",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, void>)&TryToResetHungerToNeutral));
            ItemHooks.Once("ApplyPotionDelay", () => applyPotionDelay = ItemHooks.Raw("void ApplyPotionDelay(Item sItem)",
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&ApplyPotionDelay));
        }
    }

    static int Clamp(long value, int current) => (int)Math.Max(0, Math.Min(int.MaxValue - Math.Max(0, current), value));

    static int Heal(Item item, Player player, bool life, int baseValue, bool quick, int current)
    {
        if (baseValue <= 0) return baseValue;
        int value = baseValue;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try
            {
                if (life) m.GetHealLife(player, quick, ref value);
                else m.GetHealMana(player, quick, ref value);
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{(life ? "GetHealLife" : "GetHealMana")}", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                if (life) g.GetHealLife(item, player, quick, ref value);
                else g.GetHealMana(item, player, quick, ref value);
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.{(life ? "GetHealLife" : "GetHealMana")}", e); }
        }
        if (life) PlayerLoader.Each(player, nameof(ModPlayer.GetHealLife), (item, quick), ref value, static (m, s, ref v) => m.GetHealLife(s.item, s.quick, ref v));
        else PlayerLoader.Each(player, nameof(ModPlayer.GetHealMana), (item, quick), ref value, static (m, s, ref v) => m.GetHealMana(s.item, s.quick, ref v));
        return Clamp(value, current);
    }

    [UnmanagedCallersOnly]
    static void QuickHeal(IntPtr self, IntPtr method) => Quick(quickHeal, self, method);

    [UnmanagedCallersOnly]
    static void QuickMana(IntPtr self, IntPtr method) => Quick(quickMana, self, method);

    static void Quick(Hook? hook, IntPtr self, IntPtr method)
    {
        if (hook == null) return;
        IntPtr outer = quickPlayer;
        quickPlayer = self;
        try { ItemHooks.Call(hook, self, method, "Player.QuickHeal/QuickMana"); }
        finally { quickPlayer = outer; }
    }

    [UnmanagedCallersOnly]
    static void ApplyLifeAndOrMana(IntPtr self, IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = applyLifeAndMana;
        if (hook == null) return;
        var item = new Item(itemPointer);
        int life = 0, mana = 0;
        bool changed = false;
        try
        {
            var player = new Player(self);
            life = item.healLife;
            mana = item.healMana;
            bool quick = quickPlayer == self;
            item.healLife = Heal(item, player, true, life, quick, player.statLife);
            item.healMana = Heal(item, player, false, mana, quick, player.statMana);
            changed = true;
        }
        catch (Exception e) { ItemHooks.Report("GetHealLife/GetHealMana", e); }
        try { ItemHooks.Call(hook, self, itemPointer, method, "Player.ApplyLifeAndOrMana"); }
        finally
        {
            if (changed)
            {
                item.healLife = life;
                item.healMana = mana;
            }
        }
    }

    [UnmanagedCallersOnly]
    static IntPtr GetItemToUse(IntPtr self, IntPtr method)
    {
        Hook? hook = getItemToUse;
        if (hook == null) return IntPtr.Zero;
        IntPtr outer = selectingPlayer;
        selectingPlayer = self;
        try { return (IntPtr)ItemHooks.Call(hook, self, method, "Player.QuickHeal_GetItemToUse"); }
        finally { selectingPlayer = outer; }
    }

    [UnmanagedCallersOnly]
    static void TryChoosingItem(int lifeDifference, IntPtr* bestItem, int* bestDifference, IntPtr nextItem, IntPtr method)
    {
        Hook? hook = tryChoosing;
        if (hook == null) return;
        var item = new Item(nextItem);
        int life = 0;
        bool changed = false;
        try
        {
            if (selectingPlayer != IntPtr.Zero && !item.IsNull && item.stack > 0 && item.potion && item.healLife > 0)
            {
                var player = new Player(selectingPlayer);
                life = item.healLife;
                item.healLife = Heal(item, player, true, life, true, player.statLife);
                changed = true;
            }
        }
        catch (Exception e) { ItemHooks.Report("GetHealLife (quick heal choice)", e); }
        try
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(lifeDifference);
            call.Arg((IntPtr)bestItem);
            call.Arg((IntPtr)bestDifference);
            call.Arg(nextItem);
            call.Arg(method);
            ItemHooks.Original(ref call, hook, "Player.QuickHeal_GetItemToUse_TryChoosingItem");
        }
        finally
        {
            if (changed) item.healLife = life;
        }
    }

    [UnmanagedCallersOnly]
    static void ApplyPotionDelay(IntPtr self, IntPtr itemPointer, IntPtr method)
    {
        Hook? hook = applyPotionDelay;
        if (hook == null) return;
        var player = new Player(self);
        PotionScope? outer = potion;
        var scope = new PotionScope { Player = self, Item = itemPointer };
        bool completed = false;
        try { scope.Previous = player.potionDelay; }
        catch (Exception e) { ItemHooks.Report("ApplyPotionDelay", e); }
        potion = scope;
        try
        {
            ItemHooks.Call(hook, self, itemPointer, method, "Player.ApplyPotionDelay");
            completed = true;
        }
        finally
        {
            potion = outer;
            try
            {
                if (scope.Blocked || !completed) player.potionDelay = scope.Previous;
            }
            catch (Exception e) { ItemHooks.Report("ApplyPotionDelay", e); }
        }
    }

    [UnmanagedCallersOnly]
    static void AddBuff(IntPtr self, int type, int time, byte fromNetPvP, IntPtr method)
    {
        Hook? hook = addBuff;
        if (hook == null) return;
        PotionScope? scope = potion;
        if (scope == null || scope.Player != self || type != BuffID.PotionSickness)
        {
            AddBuffOriginal(hook, self, type, time, fromNetPvP, method);
            return;
        }
        int delay = time;
        try
        {
            var player = new Player(self);
            var item = new Item(scope.Item);
            player.potionDelay = scope.Previous;
            bool allowed = true;
            ModItem? m = ItemHooks.ModItemOf(item);
            GlobalItem?[] globals = ItemHooks.GlobalsOf(item);
            if (m != null)
            {
                try { m.ModifyPotionDelay(player, ref delay); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyPotionDelay", e); }
            }
            foreach (GlobalItem? g in globals)
            {
                if (g == null) continue;
                try { g.ModifyPotionDelay(item, player, ref delay); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyPotionDelay", e); }
            }
            delay = Clamp(delay, 0);
            allowed &= PlayerLoader.All(player, nameof(ModPlayer.ApplyPotionDelay), (item, delay), static (pm, s) => pm.ApplyPotionDelay(s.item, s.delay));
            if (m != null)
            {
                try { allowed &= m.ApplyPotionDelay(player, delay); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.ApplyPotionDelay", e); }
            }
            foreach (GlobalItem? g in globals)
            {
                if (g == null) continue;
                try { allowed &= g.ApplyPotionDelay(item, player, delay); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.ApplyPotionDelay", e); }
            }
            scope.Blocked = delay <= 0 || !allowed;
            if (scope.Blocked) return;
            player.potionDelay = delay;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModifyPotionDelay/ApplyPotionDelay", e);
            delay = time;
        }
        AddBuffOriginal(hook, self, type, delay, fromNetPvP, method);
    }

    static void AddBuffOriginal(Hook hook, IntPtr self, int type, int time, byte fromNetPvP, IntPtr method)
    {
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(type);
        call.Arg(time);
        call.Arg(fromNetPvP);
        call.Arg(method);
        ItemHooks.Original(ref call, hook, "Player.AddBuff");
    }

    [UnmanagedCallersOnly]
    static void TryToResetHungerToNeutral(IntPtr self, IntPtr method)
    {
        Hook? hook = resetHunger;
        if (hook == null) return;
        PotionScope? scope = potion;
        if (scope != null && scope.Blocked && scope.Player == self) return;
        ItemHooks.Call(hook, self, method, "Player.TryToResetHungerToNeutral");
    }
}
