using System;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemManaHooks
{
    static bool globalMana;

    static bool checking;

    public static void Install(Func<string, bool> has, bool global)
    {
        if (!has(nameof(ModItem.ModifyManaCost)) && !has(nameof(ModItem.OnMissingMana)) && !has(nameof(ModItem.OnConsumeMana))) return;
        if (global) globalMana = true;
        ItemHooks.Once("Mana", () =>
        {
            On_Player.CheckMana += CheckMana;
            On_Player.CheckManaPredictWithoutUse += CheckManaPredictWithoutUse;
        });
    }

    static Item Held(Player player)
    {
        int slot = player.selectedItem;
        return slot >= 0 && slot < 59 ? player.inventory[slot] : default;
    }

    static float Cost(Player player, Item item)
    {
        float reduce = player.manaCost, mult = 1f;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.ModifyManaCost(player, ref reduce, ref mult); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyManaCost", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.ModifyManaCost(item, player, ref reduce, ref mult); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyManaCost", e); }
        }
        float value = reduce * mult;
        return float.IsFinite(value) ? Math.Max(0f, value) : player.manaCost;
    }

    static bool CheckMana(On_Player.orig_CheckMana orig, Player self, int amount, bool pay, bool blockQuickMana)
    {
        Item item = Held(self);
        if (checking || item.IsNull || item.stack <= 0 || !ItemCombatHooks.Wanted(globalMana, item)) return orig(self, amount, pay, blockQuickMana);
        float previous = self.manaCost;
        checking = true;
        try
        {
            self.manaCost = Cost(self, item);
            int needed = Math.Max(0, (int)(amount * self.manaCost));
            if (self.statMana < needed && !blockQuickMana) OnMissingMana(item, self, needed);
            int before = self.statMana;
            bool result = orig(self, amount, pay, blockQuickMana);
            if (result && pay && needed > 0)
            {
                int spent = before - self.statMana;
                int consumed = self.slowMagicUse ? Math.Max(0, spent) : spent >= 0 && spent < needed ? spent : needed;
                if (consumed > 0) OnConsumeMana(item, self, consumed);
            }
            return result;
        }
        finally
        {
            self.manaCost = previous;
            checking = false;
        }
    }

    static bool CheckManaPredictWithoutUse(On_Player.orig_CheckManaPredictWithoutUse orig, Player self, int amountBeforeManaCost, bool allowQuickMana)
    {
        Item item = Held(self);
        if (item.IsNull || item.stack <= 0 || !ItemCombatHooks.Wanted(globalMana, item)) return orig(self, amountBeforeManaCost, allowQuickMana);
        float previous = self.manaCost;
        self.manaCost = Cost(self, item);
        try { return orig(self, amountBeforeManaCost, allowQuickMana); }
        finally { self.manaCost = previous; }
    }

    static void OnMissingMana(Item item, Player player, int neededMana)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.OnMissingMana(player, neededMana); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.OnMissingMana", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.OnMissingMana(item, player, neededMana); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.OnMissingMana", e); }
        }
    }

    static void OnConsumeMana(Item item, Player player, int manaConsumed)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.OnConsumeMana(player, manaConsumed); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.OnConsumeMana", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.OnConsumeMana(item, player, manaConsumed); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.OnConsumeMana", e); }
        }
    }
}
