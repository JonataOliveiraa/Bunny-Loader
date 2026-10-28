using System;
using CsOpen.Common;
using CsOpen.Content.Items;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsOpen;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsOpenMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
        TestBag.RightClicks = 0;
        OpenGlobal.CrateRightClicks = OpenGlobal.AcornRightClicks = 0;
        LockedBag.Closed = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        try { Run(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        LockedBag.Closed = false;
        Log.Info($"csopen END pass={pass} fail={fail}");
    }

    static void Run(Player p)
    {
        int bag = ModContent.ItemType<Bag>(), keep = ModContent.ItemType<KeepBag>(), locked = ModContent.ItemType<LockedBag>();
        Report("CanRightClick true makes the item an openable bag", ItemID.Sets.OpenableBag[bag] && ItemID.Sets.OpenableBag[keep],
            $"bag={ItemID.Sets.OpenableBag[bag]} keep={ItemID.Sets.OpenableBag[keep]}");
        int category = (int)VirtualControllerInputState.GetItemCategory(bag);
        Report("the touch controls treat it as a bag", category == (int)VirtualControllerInputState.Category.NonFireItems, $"category={category}");

        TestBag.RightClicks = 0;
        bool opened = ItemSlot.TryOpenContainer_GrantItems(Make(bag), p);
        Report("RightClick runs and the bag is spent", opened && TestBag.RightClicks == 1, $"opened={opened} calls={TestBag.RightClicks}");

        TestBag.RightClicks = 0;
        opened = ItemSlot.TryOpenContainer_GrantItems(Make(keep), p);
        Report("ConsumeItem false: RightClick runs and the bag is kept", !opened && TestBag.RightClicks == 1, $"opened={opened} calls={TestBag.RightClicks}");

        LockedBag.Closed = true;
        TestBag.RightClicks = 0;
        opened = ItemSlot.TryOpenContainer_GrantItems(Make(locked), p);
        Report("CanRightClick false on this item: it does not open", !opened && TestBag.RightClicks == 0, $"opened={opened} calls={TestBag.RightClicks}");
        LockedBag.Closed = false;

        Report("GlobalItem.CanRightClick makes a vanilla item openable", ItemID.Sets.OpenableBag[ItemID.Acorn], "");
        opened = ItemSlot.TryOpenContainer_GrantItems(Make(ItemID.Acorn), p);
        Report("GlobalItem.RightClick on the item it made openable", opened && OpenGlobal.AcornRightClicks == 1,
            $"opened={opened} calls={OpenGlobal.AcornRightClicks}");

        opened = ItemSlot.TryOpenContainer_GrantItems(Make(ItemID.WoodenCrate), p);
        Report("GlobalItem.RightClick on a vanilla crate, and the game still opens it", opened && OpenGlobal.CrateRightClicks == 1,
            $"opened={opened} calls={OpenGlobal.CrateRightClicks}");
    }

    static Item Make(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csopen {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
