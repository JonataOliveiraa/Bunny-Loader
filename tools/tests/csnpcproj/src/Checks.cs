using System;
using System.Collections.Generic;
using CsNpcProj.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcProj;

public static class Checks
{
    static readonly List<string> Order = new();
    static int pass, fail, frames, npc = -1, lifeBefore;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsNpcProjMod>().Logger;

    public static void Mark(string step) => Order.Add(step);

    public static void Reset()
    {
        pass = fail = frames = 0;
        npc = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 60) npc = NPC.NewNPC(p.GetSource_FromThis(), (int)p.Center.X + 80, (int)p.Center.Y - 40, ModContent.NPCType<ProbeNpc>());
            if (frames == 70) Shoot(p, ModContent.ProjectileType<TestShot>(), null, null);
            if (frames == 90) CheckOrder();
            if (frames == 100) Shoot(p, ProjectileID.WoodenArrowFriendly, false, null);
            if (frames == 120) CheckBlocked("a GlobalNPC false stops the projectile");
            if (frames == 130) Shoot(p, ProjectileID.WoodenArrowFriendly, null, false);
            if (frames == 150) CheckBlocked("a ModNPC false stops the projectile");
            if (frames == 160) Shoot(p, ProjectileID.WoodenArrowFriendly, null, null);
            if (frames == 180) CheckVanilla();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            if ((uint)npc < 200u) Main.npc[npc].active = false;
            Log.Info($"csnpcproj END pass={pass} fail={fail}");
        }
    }

    static void Shoot(Player p, int type, bool? global, bool? own)
    {
        NPC target = Main.npc[npc];
        Order.Clear();
        ProbeNpc.Done = ProbeNpc.HitDamage = ProbeNpc.HitBy = -1;
        ProbeGlobal.Decision = global;
        ProbeNpc.Decision = own;
        target.immune[p.whoAmI] = 0;
        lifeBefore = target.life;
        Projectile.NewProjectile(p.GetSource_FromThis(), target.Center - new Vector2(12f, 0f), new Vector2(4f, 0f), type, 20, 0f, p.whoAmI);
    }

    static void CheckOrder()
    {
        string order = string.Join(",", Order);
        Report("the NPC side runs in tModLoader's order around a mod projectile", order == "IncomingN,ModifyP,ModifyN,ModifyG,OnP,OnN,OnG" &&
            ProbeNpc.Done > 0 && ProbeNpc.HitDamage == ProbeNpc.Done && ProbeNpc.HitBy == ModContent.ProjectileType<TestShot>(),
            $"order={order} done={ProbeNpc.Done} hit={ProbeNpc.HitDamage}");
    }

    static void CheckBlocked(string name)
    {
        NPC target = Main.npc[npc];
        Report(name, Order.Count == 0 && target.life == lifeBefore, $"order={string.Join(",", Order)} life={target.life}/{lifeBefore}");
    }

    static void CheckVanilla()
    {
        NPC target = Main.npc[npc];
        Report("a game projectile reaches the NPC hooks", string.Join(",", Order) == "IncomingN,ModifyN,ModifyG,OnN,OnG" &&
            ProbeNpc.HitBy == ProjectileID.WoodenArrowFriendly && lifeBefore - target.life == ProbeNpc.Done,
            $"order={string.Join(",", Order)} by={ProbeNpc.HitBy} lost={lifeBefore - target.life} done={ProbeNpc.Done}");
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csnpcproj {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
