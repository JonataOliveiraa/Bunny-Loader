using System;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CsThief;

public sealed class CsThiefMod : Mod
{
    const int Amount = 100;
    const int GlobalLootDrop = 1;

    static int frame, pass, fail, thief, dropped, itemType, before;

    static ILog Log => ModContent.GetInstance<CsThiefMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csthief {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer)
            return;
        self.creativeGodMode = true;
        frame++;
        try
        {
            Step(self);
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
        }
    }

    static int OnGround()
    {
        int total = 0;
        foreach (WorldItem item in Main.ActiveItems)
            if (item.type == itemType)
                total += item.stack;
        return total;
    }

    static int Stolen() => (int)Main.npc[thief].ModNPC.GetType().GetField("StolenItems")!.GetValue(Main.npc[thief].ModNPC)!;

    static void Step(Player self)
    {
        if (frame > 60 && frame < 180 && Main.item[dropped].active)
            Main.npc[thief].Center = Main.item[dropped].Center;
        switch (frame)
        {
            case 60:
                itemType = ModContent.Find<ModItem>("ExampleMod/ExampleItem").Type;
                before = OnGround();
                int x = (int)self.Center.X + 40 * 16;
                int y = (int)self.Center.Y - 4 * 16;
                thief = NPC.NewNPC(self.GetSource_FromThis(), x, y, ModContent.Find<ModNPC>("ExampleMod/ExampleZombieThief").Type);
                dropped = Item.NewItem(self.GetSource_FromThis(), new Vector2(x, y), itemType, Amount);
                break;
            case 180:
                Report("the stack is stolen only once", Stolen() == Amount, $"stolen={Stolen()} wanted={Amount}");
                Report("the stolen item leaves the ground", OnGround() == before, $"ground={OnGround()} before={before}");
                Main.npc[thief].StrikeInstantKill();
                break;
            case 200:
                Report("the kill gives back what was stolen", OnGround() == before + Amount + GlobalLootDrop, $"ground={OnGround()} wanted={before + Amount + GlobalLootDrop}");
                Log.Info($"csthief END pass={pass} fail={fail}");
                break;
        }
    }
}
