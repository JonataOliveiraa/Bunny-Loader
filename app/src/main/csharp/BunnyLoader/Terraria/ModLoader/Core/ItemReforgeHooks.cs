using System;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemReforgeHooks
{
    [ThreadStatic]
    static int popup;

    [ThreadStatic]
    static Item before;

    [ThreadStatic]
    static GameHandle? pin;

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.ReforgePrice)))
            ItemHooks.Once("ReforgePrice", () => On_GUIReforge.ItemReforgeCost += ItemReforgeCost);
        ItemHooks.Once("ReforgeAction", () =>
        {
            On_GUIReforgePopup.Draw += PopupDraw;
            On_Player.BuyItem += BuyItem;
            On_Item.Prefix_int += PrefixCheck;
            On_Item.Prefix_int_outbool += PrefixRoll;
        });
    }

    static int ItemReforgeCost(On_GUIReforge.orig_ItemReforgeCost orig, GUIReforge self, Item item)
    {
        int vanilla = orig(self, item);
        if (item.IsNull)
            return vanilla;
        long price = (long)item.value * item.stack;
        long start = price;
        bool discount = true;
        bool apply = ItemLoader.ReforgePrice(item, ref price, ref discount);
        if (apply && discount && price == start)
            return vanilla;
        if (apply)
        {
            Player player = Main.LocalPlayer;
            if (discount && player.discountAvailable)
                price = (long)(price * 0.8);
            price = (long)(price * player.currentShoppingSettings.PriceAdjustment);
            price /= 3;
        }

        return (int)Math.Clamp(price, 0, int.MaxValue);
    }

    static void PopupDraw(On_GUIReforgePopup.orig_Draw orig, GUIReforgePopup self)
    {
        popup++;
        try
        {
            orig(self);
        }
        finally
        {
            popup--;
        }
    }

    static bool BuyItem(On_Player.orig_BuyItem orig, Player self, long price, int customCurrency)
    {
        bool bought = orig(self, price, customCurrency);
        if (bought && popup > 0 && !GUIInstance.Active.IsNull)
        {
            Item original = GUIInstance.Active.GUIReforge.ReforgeItem;
            ItemLoader.PreReforge(original);
            before = original.Clone();
            pin = new GameHandle(before.Pointer);
        }
        return bought;
    }

    static bool PrefixCheck(On_Item.orig_Prefix_int orig, Item self, int prefixWeWant)
    {
        bool can = orig(self, prefixWeWant);
        return can && (popup == 0 || prefixWeWant != -3 || ItemLoader.CanReforge(self));
    }

    static bool PrefixRoll(On_Item.orig_Prefix_int_outbool orig, Item self, int prefixWeWant, out bool rolledPrefixIsTopTier)
    {
        bool rolled = orig(self, prefixWeWant, out rolledPrefixIsTopTier);
        if (popup > 0 && prefixWeWant == -2)
        {
            if (!before.IsNull && before.type == self.type)
                ItemLoader.KeepInstances(before, self);
            before = default;
            pin?.Dispose();
            pin = null;
            ItemLoader.PostReforge(self);
        }

        return rolled;
    }
}
