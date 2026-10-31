using System;
using System.Collections.Generic;
using CsProjHit.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Log = CsProjHit.Content.Log;

namespace CsProjHit;

public static class Checks
{
    static int pass, fail, frames, npcA = -1, npcB = -1, needleA = -1, needleB = -1, arrow = -1, lifeB;
    static bool done;

    static ILog Logger => ModContent.GetInstance<CsProjHitMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        npcA = npcB = needleA = needleB = arrow = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 30) Spawn(p);
            if (frames == 120) Arrow(p);
            if (frames != 240) return;
            done = true;
            Run(p);
            Hurt(p);
        }
        catch (Exception e)
        {
            done = true;
            Report("run", false, e.ToString());
        }
        finally
        {
            if (done) Cleanup();
        }
        if (done) Logger.Info($"csprojhit END pass={pass} fail={fail}");
    }

    static int Slime(Player p, float offset)
    {
        int index = NPC.NewNPC(default, (int)(p.Center.X + offset), (int)(p.position.Y + p.height), NPCID.BlueSlime);
        NPC npc = Main.npc[index];
        npc.lifeMax = 100000;
        npc.life = 100000;
        npc.defense = 10;
        npc.townNPC = true;
        npc.aiStyle = 0;
        npc.damage = 0;
        return index;
    }

    static void Spawn(Player p)
    {
        npcA = Slime(p, 300f);
        npcB = Slime(p, -300f);
        lifeB = Main.npc[npcB].life;
        Needle.Blocked = npcB;
        int type = ModContent.ProjectileType<Needle>();
        needleA = Projectile.NewProjectile(p.GetSource_FromThis(), Main.npc[npcA].Center, Vector2.Zero, type, 10, 0f, p.whoAmI, npcA);
        needleB = Projectile.NewProjectile(p.GetSource_FromThis(), Main.npc[npcB].Center, Vector2.Zero, type, 10, 0f, p.whoAmI, npcB);
        int bullet = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(0f, -600f), Vector2.Zero, ProjectileID.Bullet, 0, 0f, p.whoAmI);
        Projectile needle = Main.projectile[needleA];
        Report("GlobalProjectile instances by AppliesToEntity, after SetDefaults",
            needle.TryGetGlobalProjectile(out HitGlobal g) && g.Defaulted && !Main.projectile[bullet].TryGetGlobalProjectile<HitGlobal>(out _) &&
            needle.GetGlobalProjectile<HitGlobal>() != Main.projectile[needleB].GetGlobalProjectile<HitGlobal>(),
            $"found={needle.TryGetGlobalProjectile<HitGlobal>(out _)} bullet={Main.projectile[bullet].TryGetGlobalProjectile<HitGlobal>(out _)}");
        Main.projectile[bullet].Kill();
    }

    static void Arrow(Player p)
    {
        NPC target = Main.npc[npcA];
        arrow = Projectile.NewProjectile(p.GetSource_FromThis(), target.Center + new Vector2(0f, -4f), new Vector2(0f, 2f), ProjectileID.WoodenArrowFriendly, 5, 0f, p.whoAmI);
    }

    static void Run(Player p)
    {
        int a = Needle.HitsOn.GetValueOrDefault(npcA), b = Needle.HitsOn.GetValueOrDefault(npcB);
        Report("the mod projectile hits its NPC through Damage_PVE", a >= 3 && Needle.Consistent && Needle.LastDone >= 1000 && !Needle.LastHit.Crit &&
            Needle.LastHit.Damage == Needle.LastDone && Needle.LastHit.DamageType == DamageClass.Ranged,
            $"hits={a} consistent={Needle.Consistent} done={Needle.LastDone} crit={Needle.LastHit.Crit} type={Needle.LastHit.DamageType?.Name}");
        Report("CanHitNPC false keeps the other NPC untouched", b == 0 && Main.npc[npcB].life == lifeB && Log.Of(nameof(Needle.CanHitNPC)) > a,
            $"hits={b} life {lifeB}->{Main.npc[npcB].life} canHit={Log.Of(nameof(Needle.CanHitNPC))}");
        string order = string.Join(",", Log.HitOrder);
        Report("ModifyHitNPC order: projectile, global, player, player WithProj", order == "mod,global,player,playerProj", order);
        Report("ModPlayer WithProj hooks run for its projectile", Log.Of(nameof(HitPlayer.OnHitNPCWithProj)) == a && Log.Of(nameof(HitPlayer.CanHitNPCWithProj)) > 0,
            $"onHit={Log.Of(nameof(HitPlayer.OnHitNPCWithProj))} hits={a} canHit={Log.Of(nameof(HitPlayer.CanHitNPCWithProj))}");
        HitGlobal ga = Main.projectile[needleA].GetGlobalProjectile<HitGlobal>(), gb = Main.projectile[needleB].GetGlobalProjectile<HitGlobal>();
        Report("each projectile keeps its own global", ga.Hits == a && gb.Hits == 0, $"a={ga.Hits} b={gb.Hits}");
        Report("a game projectile runs the global's OnSpawn, AI, OnHitNPC and kill", Log.Of("ArrowSpawn") == 1 && Log.Of("ArrowAI") >= 1 &&
            Log.Of("ArrowHit") == 1 && Log.Of("ArrowPreKill") == 1 && Log.Of("ArrowOnKill") == 1 && !Main.projectile[arrow].active,
            $"sources={string.Join(";", Log.Sources)} spawn={Log.Of("ArrowSpawn")} ai={Log.Of("ArrowAI")} hit={Log.Of("ArrowHit")} pre={Log.Of("ArrowPreKill")} on={Log.Of("ArrowOnKill")}");
    }

    static double Hit(Player p, int spike)
    {
        p.immune = false;
        p.immuneTime = 0;
        var cooldowns = p.hurtCooldowns;
        for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = 0;
        p.statLifeMax2 = Math.Max(p.statLifeMax2, 500);
        p.statLife = 400;
        return p.Hurt(PlayerDeathReason.ByProjectile(255, spike), 20, 0, false, true, false, -1, true);
    }

    static void Hurt(Player p)
    {
        int spike = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(0f, -2000f), Vector2.Zero, ModContent.ProjectileType<Spike>(), 20, 0f, 255);
        bool god = p.creativeGodMode;
        int life = p.statLife;
        p.creativeGodMode = false;
        try
        {
            double done = Hit(p, spike);
            string order = string.Join(",", Log.HurtOrder);
            Report("a hostile projectile runs ModifyHitPlayer and OnHitPlayer", done > 0 && Spike.LastDamage == (int)done && Log.Of(nameof(Spike.OnHitPlayer)) == 1 &&
                Log.Of("GlobalOnHitPlayer") == 1 && order == "mod,global,player", $"done={done} last={Spike.LastDamage} order={order}");
            Spike.Block = true;
            double blocked = Hit(p, spike);
            Spike.Block = false;
            Report("CanHitPlayer false stops the hit", blocked == 0 && Log.Of(nameof(Spike.OnHitPlayer)) == 1, $"done={blocked} onHit={Log.Of(nameof(Spike.OnHitPlayer))}");
        }
        finally
        {
            p.creativeGodMode = god;
            p.statLife = life;
            p.immune = false;
            p.immuneTime = 0;
            Main.projectile[spike].Kill();
        }
    }

    static void Cleanup()
    {
        foreach (int i in new[] { needleA, needleB })
            if (i >= 0 && Main.projectile[i].active) Main.projectile[i].Kill();
        foreach (int i in new[] { npcA, npcB })
            if (i >= 0) Main.npc[i].active = false;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Logger.Info($"csprojhit {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
