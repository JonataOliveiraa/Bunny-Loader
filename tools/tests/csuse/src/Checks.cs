using CsUse.Content.Items;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsUse;

public static class Checks
{
    static int pass, fail;

    static ILog Log => ModContent.GetInstance<CsUseMod>().Logger;

    public static void Run(Player player)
    {
        Counts.Clear();
        Item blocked = Make(ModContent.ItemType<BlockedItem>());
        Item used = Make(ModContent.ItemType<UsedItem>());
        Item plain = Make(ModContent.ItemType<PlainItem>());
        Item vetoed = Make(ItemID.WoodenSword);

        Report("CanUseItem false blocks", !player.ItemCheck_CheckCanUse_Inner(blocked, false) && Counts.Get("Blocked.CanUseItem") > 0,
            $"calls={Counts.Get("Blocked.CanUseItem")}");
        Report("GlobalItem CanUseItem false blocks a vanilla item", !player.ItemCheck_CheckCanUse_Inner(vetoed, false) && Counts.Get("Global.CanUseItem") > 0,
            $"calls={Counts.Get("Global.CanUseItem")}");
        player.ItemCheck_CheckCanUse_Inner(used, false);
        Report("CanUseItem asked for an allowed item", Counts.Get("Used.CanUseItem") > 0, "");

        player.ItemCheck_StartActualUse(used);
        Report("UseItem", Counts.Get("Used.UseItem") == 1 && Counts.Get("Global.UseItem") >= 1,
            $"mod={Counts.Get("Used.UseItem")} global={Counts.Get("Global.UseItem")}");

        player.ApplyItemAnimation(used);
        Report("UseAnimation runs before the game's calculation", Counts.Get("Used.UseAnimation") == 2 && player.itemAnimationMax == 33,
            $"calls={Counts.Get("Used.UseAnimation")} itemAnimationMax={player.itemAnimationMax}");

        var frame = new Rectangle(0, 0, 40, 40);
        player.itemRotation = 0f;
        player.ItemCheck_ApplyUseStyle(0f, plain, frame);
        Vector2 without = player.itemLocation;
        float rotation = player.itemRotation;
        player.itemRotation = 0f;
        player.ItemCheck_ApplyUseStyle(0f, used, frame);
        Vector2 with = player.itemLocation;
        Report("UseStyle and HoldItem", Counts.Get("Used.UseStyle") == 1 && Counts.Get("Used.HoldItem") >= 1 && Counts.Get("Global.UseStyle") >= 2,
            $"style={Counts.Get("Used.UseStyle")} hold={Counts.Get("Used.HoldItem")} global={Counts.Get("Global.UseStyle")}");
        float dx = 10f * player.direction, dy = 5f * player.gravDir;
        float cos = System.MathF.Cos(rotation), sin = System.MathF.Sin(rotation);
        var expected = new Vector2(without.X + dx * cos - dy * sin, without.Y + dx * sin + dy * cos);
        Report("HoldoutOffset moves the item", System.MathF.Abs(with.X - expected.X) < 0.01f && System.MathF.Abs(with.Y - expected.Y) < 0.01f,
            $"without=({without.X}, {without.Y}) with=({with.X}, {with.Y}) expected=({expected.X}, {expected.Y})");

        player.ItemCheck_ApplyHoldStyle(0f, used, frame);
        Report("HoldStyle", Counts.Get("Used.HoldStyle") == 1, $"calls={Counts.Get("Used.HoldStyle")}");

        int slot = EmptySlot(player);
        if (slot < 0)
        {
            Report("UpdateInventory", false, "no free inventory slot");
        }
        else
        {
            player.inventory[slot].SetDefaults(ModContent.ItemType<UsedItem>(), default);
            player.UpdateEquips(player.whoAmI);
            Report("UpdateInventory", Counts.Get("Used.UpdateInventory") >= 1 && Counts.Get("Global.UpdateInventory") >= 1,
                $"mod={Counts.Get("Used.UpdateInventory")} global={Counts.Get("Global.UpdateInventory")}");
            player.inventory[slot].TurnToAir();
        }
        Log.Info($"csuse END pass={pass} fail={fail}");
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
        Log.Info($"csuse {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
