using System;
using System.Collections.Generic;
using CsBossBar.Content;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBossBar;

public static class Checks
{
    public static readonly List<Color> Backings = new();
    public static bool InBar;
    static readonly List<string> Order = new();
    static int pass, fail, frames, npc = -1, king = -1, mark, vanillaBefore;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsBossBarMod>().Logger;

    public static void Mark(string step)
    {
        if (Order.Count < 16) Order.Add(step);
    }

    public static void Reset()
    {
        pass = fail = frames = 0;
        npc = king = -1;
        done = false;
        TestBar.Hide = TestBar.Tint = InBar = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60)
            {
                npc = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + 80, (int)p.Center.Y - 40, ModContent.NPCType<BarNpc>());
                Order.Clear();
            }
            if (frames == 160) CheckShown();
            if (frames == 170)
            {
                Main.npc[npc].life = 1234;
                TestBar.Tint = true;
                Backings.Clear();
            }
            if (frames == 230) CheckLife();
            if (frames == 240)
            {
                TestBar.Tint = false;
                TestBar.Hide = true;
            }
            if (frames == 250) mark = TestBar.Pres;
            if (frames == 320) CheckHidden();
            if (frames == 330) SpawnKing(p);
            if (frames == 450) CheckVanilla();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            foreach (int index in new[] { npc, king })
                if ((uint)index < 200u) Main.npc[index].active = false;
            Log.Info($"csbossbar END pass={pass} fail={fail}");
        }
    }

    static void CheckShown()
    {
        NPC n = Main.npc[npc];
        Report("NPC.BossBar keeps the ModBossBar set in SetDefaults", n.BossBar == ModContent.GetInstance<TestBar>(), $"bar={n.BossBar?.FullName ?? "null"}");
        string order = string.Join(",", Order.GetRange(0, Math.Min(4, Order.Count)));
        Report("a non-boss NPC with a ModBossBar gets the bar, in the tModLoader draw order", order == "PreG,PreN,PostN,PostG" && TestBar.Infos > 0,
            $"order={string.Join(",", Order)} infos={TestBar.Infos}");
        TestBar bar = ModContent.GetInstance<TestBar>();
        Report("the bar collects the NPC life and takes the icon frame",
            bar.LifeMax == 5000 && bar.Life == 5000 && TestBar.IconCalls > 0 && TestBar.LastFrame.Width == 13 && TestBar.LastFrame.Height == 13,
            $"life={bar.Life}/{bar.LifeMax} icons={TestBar.IconCalls} frame={TestBar.LastFrame.Width}x{TestBar.LastFrame.Height}");
    }

    static void CheckLife()
    {
        TestBar bar = ModContent.GetInstance<TestBar>();
        Report("the bar follows the NPC life", bar.Life == 1234, $"life={bar.Life}");
        bool tinted = Backings.Exists(c => c.R > 0 && c.G == 0 && c.B == 0);
        Report("BarColor from PreDraw tints the mobile bar backing", tinted,
            $"backings={Backings.Count} sample={(Backings.Count > 0 ? $"{Backings[0].R},{Backings[0].G},{Backings[0].B}" : "none")}");
    }

    static void CheckHidden()
    {
        Report("ModifyInfo false hides the bar", TestBar.Pres == mark, $"draws after hiding={TestBar.Pres - mark}");
        Main.npc[npc].active = false;
        npc = -1;
        TestBar.Hide = false;
    }

    static void SpawnKing(Player p)
    {
        vanillaBefore = TestGlobalBar.Vanilla;
        king = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X - 120, (int)p.Center.Y - 40, NPCID.KingSlime);
        NPC k = Main.npc[king];
        k.friendly = true;
        k.damage = 0;
        k.dontTakeDamage = true;
    }

    static void CheckVanilla()
    {
        Report("GlobalBossBar runs on a vanilla boss bar", TestGlobalBar.Vanilla > vanillaBefore, $"draws={TestGlobalBar.Vanilla - vanillaBefore} king={king}");
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbossbar {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
