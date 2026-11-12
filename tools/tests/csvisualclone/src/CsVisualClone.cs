using System;
using log4net;
using Terraria;
using Terraria.ModLoader;

namespace CsVisualClone;

public sealed class CountedItem : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + Terraria.ID.ItemID.WoodenSword;

    public int Counter;

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.useStyle = 1;
        Item.useTime = 20;
        Item.useAnimation = 20;
    }
}

public sealed class CountedGlobal : GlobalItem
{
    public override bool InstancePerEntity => true;

    public int Counter;
}

public sealed class CsVisualCloneMod : Mod
{
    static int frame, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsVisualCloneMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csvisualclone {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        self.creativeGodMode = true;
        try { Step(self, ++frame); }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish();
        }
    }

    static void Step(Player self, int f)
    {
        if (f == 30)
        {
            self.inventory[self.selectedItem].SetDefaults(ModContent.ItemType<CountedItem>(), default);
        }
        if (f < 30) return;
        Item held = self.inventory[self.selectedItem];
        if (held.ModItem is CountedItem counted) counted.Counter = f;
        held.GetGlobalItem<CountedGlobal>().Counter = f;
        if (f != 90) return;
        Item visual = self.lastVisualizedSelectedItem;
        CountedItem? copy = visual.IsNull ? null : visual.ModItem as CountedItem;
        Report("the held copy has its own ModItem", copy != null && !ReferenceEquals(copy, held.ModItem),
            $"type={(visual.IsNull ? -1 : visual.type)} copy={(copy == null ? "null" : "set")}");
        Report("the held copy ModItem follows the held item", copy != null && copy.Counter >= f - 2, $"counter={copy?.Counter} frame={f}");
        int global = visual.IsNull ? -1 : visual.GetGlobalItem<CountedGlobal>().Counter;
        Report("the held copy GlobalItem follows the held item", global >= f - 2, $"counter={global} frame={f}");
        Finish();
    }

    static void Finish()
    {
        done = true;
        Log.Info($"csvisualclone pass={pass} fail={fail}");
        Log.Info("csvisualclone END");
    }
}
