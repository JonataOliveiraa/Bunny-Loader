using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class RarityLoader
{
    public const int VanillaCount = 12;

    static readonly object Gate = new();

    static ModRarity?[] rarities = Array.Empty<ModRarity?>();

    static Hook? popup, prefix, mouseText;

    public static int RarityCount => Host.Native()->RarityCount();

    public static ModRarity? GetRarity(int type)
    {
        ModRarity?[] all = Volatile.Read(ref rarities);
        uint index = (uint)(type - VanillaCount);
        return index < (uint)all.Length ? all[index] : null;
    }

    internal static void Register(ModRarity rarity)
    {
        rarity.Type = Host.Native()->AllocateRarity();
        lock (Gate)
        {
            int index = rarity.Type - VanillaCount;
            ModRarity?[] all = rarities;
            if (index >= all.Length)
            {
                var grown = new ModRarity?[Math.Max(index + 1, all.Length * 2)];
                Array.Copy(all, grown, all.Length);
                all = grown;
            }
            all[index] = rarity;
            Volatile.Write(ref rarities, all);
        }
        ItemHooks.Once("Rarities", Install);
    }

    internal static Color ColorOf(ModRarity rarity)
    {
        try { return rarity.RarityColor; }
        catch (Exception e)
        {
            ItemHooks.Report($"{rarity.FullName}.RarityColor", e);
            return Color.White;
        }
    }

    static void Install()
    {
        popup = ItemHooks.Raw("Terraria", "Item", "Color GetPopupRarityColor(int itemRarity)",
            (IntPtr)(delegate* unmanaged<int, IntPtr, uint>)&GetPopupRarityColor);
        prefix = ItemHooks.Raw("Terraria", "Item", "bool Prefix(int prefixWeWant, out bool rolledPrefixIsTopTier)",
            (IntPtr)(delegate* unmanaged<IntPtr, int, byte*, IntPtr, byte>)&Prefix);
        mouseText = ItemHooks.Raw("Terraria", "Main", "void MouseTextInner(Main.MouseTextCache info, bool worldMouse)",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte, IntPtr, void>)&MouseTextInner);
        ItemHooks.Once("TooltipDraw", ItemTooltipDraw.Install);
    }

    [UnmanagedCallersOnly]
    static uint GetPopupRarityColor(int rarity, IntPtr method)
    {
        Hook? hook = popup;
        if (hook == null) return uint.MaxValue;
        try
        {
            if (GetRarity(rarity) is ModRarity r)
            {
                Color c = ColorOf(r);
                return *(uint*)&c;
            }
        }
        catch (Exception e) { ItemHooks.Report("RarityColor", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(rarity);
        call.Arg(method);
        return (uint)ItemHooks.Original(ref call, hook, "Item.GetPopupRarityColor");
    }

    [UnmanagedCallersOnly]
    static byte Prefix(IntPtr self, int want, byte* topTier, IntPtr method)
    {
        Hook? hook = prefix;
        if (hook == null) return 0;
        var item = new Item(self);
        int baseRare = 0;
        try { baseRare = item.rare; }
        catch (Exception e) { ItemHooks.Report("GetPrefixedRarity", e); }
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(want);
        call.Arg((IntPtr)topTier);
        call.Arg(method);
        byte ok = (byte)(ItemHooks.Original(ref call, hook, "Item.Prefix") & 0xFF);
        if (ok != 0 && want != -3 && GetRarity(baseRare) is ModRarity r)
        {
            try { Prefixed(item, r); }
            catch (Exception e) { ItemHooks.Report($"{r.FullName}.GetPrefixedRarity", e); }
        }
        return ok;
    }

    static void Prefixed(Item item, ModRarity rarity)
    {
        float value = 1f;
        if (item.prefix != 0)
            item.TryGetPrefixStatMultipliersForItem(item.prefix, out _, out _, out _, out _, out _, out _, out _, out _, out _, out value);
        int offset = value >= 1.2f ? 2 : value >= 1.05f ? 1 : value <= 0.8f ? -2 : value <= 0.95f ? -1 : 0;
        int rare = rarity.GetPrefixedRarity(offset, value);
        if (rare > -11) rare = Math.Clamp(rare, -1, RarityCount - 1);
        item.rare = rare;
    }

    [UnmanagedCallersOnly]
    static void MouseTextInner(IntPtr self, IntPtr info, byte worldMouse, IntPtr method)
    {
        Hook? hook = mouseText;
        if (hook == null) return;
        bool recolored = false;
        try
        {
            var cache = (Main.MouseTextCache*)info;
            if (info != IntPtr.Zero && cache->diff == 0 && GetRarity(cache->rare) is ModRarity r && Main.HoverItem.type < 1 &&
                string.IsNullOrEmpty(cache->buffTooltip.ToString()) && cache->cursorText.ToString() is string text && text.Length > 0)
            {
                ItemTooltipDraw.Recolor(text, ColorOf(r));
                recolored = true;
            }
        }
        catch (Exception e) { ItemHooks.Report("RarityColor (mouse text)", e); }
        try
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(self);
            call.Arg(info);
            call.Arg(worldMouse);
            call.Arg(method);
            ItemHooks.Original(ref call, hook, "Main.MouseTextInner");
        }
        finally
        {
            if (recolored) ItemTooltipDraw.EndRecolor();
        }
    }
}
