using System;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGolf;

public sealed class CsGolfMod : Mod
{
    static int frame, pass, fail, step, slot, unloaded;
    static Item kept;

    static ILog Log => ModContent.GetInstance<CsGolfMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csgolf {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || step == 99) return;
        frame++;
        try
        {
            switch (step)
            {
                case 0 when frame == 30:
                    Check();
                    slot = self.selectedItem;
                    kept = self.inventory[slot].Clone();
                    self.inventory[slot] = new Item(unloaded);
                    Log.Info($"csgolf holding {unloaded} {self.inventory[slot].Name}");
                    step = 1;
                    break;
                case 1 when frame == 150:
                    Report("holding the unloaded item ran the golf checks", self.HeldItem.type == unloaded, self.HeldItem.Name);
                    self.inventory[slot] = kept;
                    step = 99;
                    Log.Info($"csgolf END pass={pass} fail={fail}");
                    break;
            }
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            step = 99;
            Log.Info($"csgolf END pass={pass} fail={fail}");
        }
    }

    static void Check()
    {
        int lastModItem = 0;
        for (int type = ItemID.Count; type < ItemLoader.ItemCount; type++)
        {
            if (ItemLoader.GetItem(type) != null) lastModItem = type;
            else unloaded = type;
        }
        int balls = ProjectileID.Sets.IsAGolfBall.Length, clubs = ItemID.Sets.IsAGolfClub.Length, tools = ItemID.Sets.CatchingTool.Length;
        Log.Info($"csgolf sets: items {ItemLoader.ItemCount}, last C# item {lastModItem}, last unloaded {unloaded}, IsAGolfClub {clubs}, CatchingTool {tools}, IsAGolfBall {balls}, projectiles {ProjectileLoader.ProjectileCount}");
        Report("ItemCount covers the unloaded items after the C# items", unloaded > lastModItem, $"{unloaded} > {lastModItem}");
        Report("C# item sets cover every item", clubs >= ItemLoader.ItemCount && tools >= ItemLoader.ItemCount, $"{clubs} {tools}");
        Report("ContentSamples has the unloaded item", ContentSamples.ItemsByType.TryGetValue(unloaded, out Item sample) && sample.type == unloaded);
        int outside = 0, golf = 0;
        for (int type = 1; type < ItemLoader.ItemCount; type++)
        {
            Item item = new Item(type);
            if ((uint)item.shoot >= (uint)balls)
            {
                outside++;
                if (outside <= 5) Log.Info($"csgolf shoot outside IsAGolfBall: {type} {item.Name} shoot={item.shoot}");
            }
            if (Item.IsAGolfingItem(item)) golf++;
        }
        Report("every item's shoot fits IsAGolfBall", outside == 0, $"{outside} outside");
        Report("vanilla and mod golf clubs are golfing items", Item.IsAGolfingItem(new Item(ItemID.GolfClubIron)) && Item.IsAGolfingItem(new Item(ModContent.Find<ModItem>("ExampleMod/ExampleGolfClub").Type)), $"{golf} golfing items");
        Report("a sword is not a golfing item", !Item.IsAGolfingItem(new Item(ItemID.CopperShortsword)));
    }
}
