using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Hooks;
using BunnyLoader.Interop;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static unsafe class ShopSavingsHooks
{
    const int TavernkeepShop = 21;
    const int ShopFlag = 0, OpenFlag = 1, CloseFlag = 2;

    static readonly int* Flags = (int*)NativeMemory.AllocZeroed(sizeof(int) * 3);

    static readonly List<int> Currencies = new();

    static int shopIndex = -1;

    static bool swapped, textureSwapped;

    static int savedShop, savedMedals;

    static GameObject savedTexture;

    static GateSpec Gate(int index) =>
        new() { Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = index * sizeof(int), FieldSize = 4, Address = (IntPtr)Flags };

    internal static void Install() => ItemHooks.Once("ShopSavings", () =>
    {
        HookGates.Define<On_GUIShop.hook_Draw>(() => Gate(ShopFlag));
        HookGates.Want<On_GUIShop.hook_Draw>();
        On_GUIShop.Draw += Draw;
        HookGates.Define<On_Utils.hook_CoinsCombineStacks>(() => Gate(OpenFlag));
        HookGates.Want<On_Utils.hook_CoinsCombineStacks>();
        On_Utils.CoinsCombineStacks += CombineStacks;
        HookGates.Define<On_Language.hook_GetTextValue_string>(() => Gate(CloseFlag));
        HookGates.Want<On_Language.hook_GetTextValue_string>();
        On_Language.GetTextValue_string += TextValue;
    });

    internal static void ShopOpened(InventoryStorage storage, int type)
    {
        Currencies.Clear();
        shopIndex = -1;
        Flags[ShopFlag] = 0;
        if (type == TavernkeepShop || storage.IsNull) return;
        try
        {
            var items = storage.item;
            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                if (item.IsNull || item.type <= 0) continue;
                int currency = item.shopSpecialCurrency;
                if (currency != -1 && !Currencies.Contains(currency)) Currencies.Add(currency);
            }
            if (Currencies.Count == 0) return;
            foreach (int currency in Currencies)
            {
                int coin = CoinOf(currency);
                if (coin > 0 && coin != ItemID.DefenderMedal) Main.instance.LoadItem(coin);
            }
            shopIndex = type;
            Flags[ShopFlag] = 1;
        }
        catch (Exception e) { ItemHooks.Report("shop savings", e); }
    }

    static int CoinOf(int currency)
    {
        if (currency == CustomCurrencyID.DefenderMedals) return ItemID.DefenderMedal;
        var all = new GameDictionary(CustomCurrencyManager._currencies);
        if (all.IsNull || !all.TryGetValue(currency, out IntPtr system) || system == IntPtr.Zero) return 0;
        var values = new GameDictionary(new CustomCurrencySystem(system)._valuePerUnit);
        if (values.IsNull) return 0;
        int coin = 0, worth = int.MaxValue;
        foreach (KeyValuePair<int, int> entry in values.IntEntries())
        {
            if (entry.Value >= worth) continue;
            coin = entry.Key;
            worth = entry.Value;
        }
        return coin;
    }

    static int Current()
    {
        int hovered = ItemSlot._customCurrencyForSavings;
        return Currencies.Contains(hovered) ? hovered : Currencies[0];
    }

    static void Draw(On_GUIShop.orig_Draw orig, GUIShop self)
    {
        bool wanted = false;
        try { wanted = shopIndex > 0 && Currencies.Count > 0 && Main.npcShop == shopIndex; }
        catch (Exception e) { ItemHooks.Report("shop savings", e); }
        if (!wanted)
        {
            orig(self);
            return;
        }
        Flags[OpenFlag] = 1;
        try { orig(self); }
        finally
        {
            Flags[OpenFlag] = 0;
            Flags[CloseFlag] = 0;
            Restore();
        }
    }

    static long CombineStacks(On_Utils.orig_CoinsCombineStacks orig, out bool overFlowing, GameArray<long> coinCounts)
    {
        long result = orig(out overFlowing, coinCounts);
        Flags[OpenFlag] = 0;
        try { Swap(); }
        catch (Exception e)
        {
            Restore();
            ItemHooks.Report("shop savings", e);
        }
        Flags[CloseFlag] = 1;
        return result;
    }

    static string? TextValue(On_Language.orig_GetTextValue_string orig, string? key)
    {
        Flags[CloseFlag] = 0;
        Restore();
        return orig(key);
    }

    static void Swap()
    {
        int currency = Current();
        int coin = CoinOf(currency);
        if (coin <= 0) return;
        savedShop = Main.npcShop;
        savedMedals = CustomCurrencyID.DefenderMedals;
        swapped = true;
        Main.npcShop = TavernkeepShop;
        CustomCurrencyID.DefenderMedals = currency;
        if (coin == ItemID.DefenderMedal) return;
        var textures = TextureAssets.Item_.Raw;
        savedTexture = textures[ItemID.DefenderMedal];
        textures[ItemID.DefenderMedal] = textures[coin];
        textureSwapped = true;
    }

    static void Restore()
    {
        if (!swapped) return;
        swapped = false;
        try
        {
            Main.npcShop = savedShop;
            CustomCurrencyID.DefenderMedals = savedMedals;
            if (textureSwapped) TextureAssets.Item_.Raw[ItemID.DefenderMedal] = savedTexture;
        }
        catch (Exception e) { ItemHooks.Report("shop savings", e); }
        finally { textureSwapped = false; }
    }
}
