using CsHeal.Content.Items;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsHeal;

public static class Checks
{
    static int pass, fail;

    static ILog Log => ModContent.GetInstance<CsHealMod>().Logger;

    public static void Run(Player player)
    {
        Item heal = Make(ModContent.ItemType<HealPotion>());
        player.statLife = 1;
        HealPotion.SawQuick = null;
        player.ApplyLifeAndOrMana(heal);
        Report("GetHealLife adds to the heal", player.statLife == 1 + 50 + 25 && HealPotion.SawQuick == false && heal.healLife == 50,
            $"statLife={player.statLife} quick={HealPotion.SawQuick} healLife after={heal.healLife}");

        Item mana = Make(ItemID.LesserManaPotion);
        int baseMana = mana.healMana;
        player.statManaMax2 = 1000;
        player.statMana = 0;
        player.ApplyLifeAndOrMana(mana);
        Report("GlobalItem GetHealMana doubles a vanilla potion", player.statMana == baseMana * 2 && mana.healMana == baseMana,
            $"statMana={player.statMana} base={baseMana}");

        Item calm = Make(ModContent.ItemType<CalmPotion>());
        player.ClearBuff(BuffID.PotionSickness);
        player.potionDelay = 0;
        player.ApplyPotionDelay(calm);
        Report("ApplyPotionDelay false: no sickness", player.potionDelay == 0 && !player.HasBuff(BuffID.PotionSickness),
            $"potionDelay={player.potionDelay}");

        player.ClearBuff(BuffID.PotionSickness);
        player.potionDelay = 0;
        player.ApplyPotionDelay(heal);
        Report("ModifyPotionDelay sets the delay", player.potionDelay == 600 && player.HasBuff(BuffID.PotionSickness),
            $"potionDelay={player.potionDelay}");

        int slot = EmptySlot(player);
        if (slot < 0)
        {
            Report("quick heal", false, "no free inventory slot");
        }
        else
        {
            player.ClearBuff(BuffID.PotionSickness);
            player.potionDelay = 0;
            player.inventory[slot].SetDefaults(ModContent.ItemType<HealPotion>(), default);
            player.statLife = 1;
            HealPotion.SawQuick = null;
            player.QuickHeal();
            Report("quick heal passes quickHeal true", HealPotion.SawQuick == true && player.statLife > 1,
                $"quick={HealPotion.SawQuick} statLife={player.statLife}");
            if (player.inventory[slot].type == ModContent.ItemType<HealPotion>()) player.inventory[slot].TurnToAir();
        }
        player.ClearBuff(BuffID.PotionSickness);
        player.potionDelay = 0;
        player.statLife = player.statLifeMax2;
        Log.Info($"csheal END pass={pass} fail={fail}");
    }

    static Item Make(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static int EmptySlot(Player player)
    {
        for (int slot = 0; slot < 50; slot++)
        {
            Item item = player.inventory[slot];
            if (item.type <= 0 || item.stack <= 0) return slot;
        }
        return -1;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csheal {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
