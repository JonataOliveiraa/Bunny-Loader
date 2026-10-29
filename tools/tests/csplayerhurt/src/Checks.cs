using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPlayerHurt;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPlayerHurtMod>().Logger;

    static int Calls(string name) => HurtPlayer.Calls.GetValueOrDefault(name);

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        bool god = p.creativeGodMode;
        int life = p.statLife;
        p.creativeGodMode = false;
        try { Run(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally
        {
            HurtPlayer.Mode = "";
            p.creativeGodMode = god;
            p.statLife = life;
            p.immune = false;
            p.immuneTime = 0;
        }
        Log.Info($"csplayerhurt END pass={pass} fail={fail}");
    }

    static double Hit(Player p, PlayerDeathReason source, int damage, string mode)
    {
        HurtPlayer.Mode = mode;
        p.immune = false;
        p.immuneTime = 0;
        var cooldowns = p.hurtCooldowns;
        for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = 0;
        p.statLifeMax2 = Math.Max(p.statLifeMax2, 500);
        p.statLife = 400;
        try { return p.Hurt(source, damage, 0, false, true, false, -1, true); }
        finally { HurtPlayer.Mode = ""; }
    }

    static void Run(Player p)
    {
        PlayerDeathReason custom = PlayerDeathReason.ByCustomReason("csplayerhurt");
        int defense = p.statDefense;
        int modify = Calls(nameof(HurtPlayer.ModifyHurt)), onHurt = Calls(nameof(HurtPlayer.OnHurt)), post = Calls(nameof(HurtPlayer.PostHurt));
        double plain = Hit(p, custom, 50, "");
        Report("ModifyHurt, OnHurt and PostHurt on a plain hit", plain > 0 && Calls(nameof(HurtPlayer.ModifyHurt)) == modify + 1 &&
            Calls(nameof(HurtPlayer.OnHurt)) == onHurt + 1 && Calls(nameof(HurtPlayer.PostHurt)) == post + 1 && HurtPlayer.LastInfo.Damage == (int)plain,
            $"done={plain} info={HurtPlayer.LastInfo.Damage} defense={defense}");

        double hundred = Hit(p, custom, 100, "");
        double doubled = Hit(p, custom, 50, "double");
        Report("SourceDamage doubles the hit before the defense", doubled == hundred && HurtPlayer.LastInfo.SourceDamage == 100,
            $"doubled={doubled} hundred={hundred} source={HurtPlayer.LastInfo.SourceDamage}");

        double half = Hit(p, custom, 50, "half");
        int expected = (int)(Main.CalculateDamagePlayersTake(50, defense) * 0.5);
        Report("FinalDamage halves the hit after the defense", (int)half == expected && HurtPlayer.LastInfo.Damage == expected,
            $"half={half} expected={expected} info={HurtPlayer.LastInfo.Damage}");

        int savedDefense = p.statDefense;
        try
        {
            p.statDefense = 20;
            double armoredPlain = Hit(p, custom, 50, "");
            double armoredHalf = Hit(p, custom, 50, "half");
            int armoredExpected = (int)(Main.CalculateDamagePlayersTake(50, 20) * 0.5);
            Report("FinalDamage works through the defense", (int)armoredPlain == (int)Main.CalculateDamagePlayersTake(50, 20) && (int)armoredHalf == armoredExpected,
                $"plain={armoredPlain} half={armoredHalf} expected={armoredExpected}");
        }
        finally { p.statDefense = savedDefense; }

        int lifeBefore;
        onHurt = Calls(nameof(HurtPlayer.OnHurt));
        double immune = Hit(p, custom, 50, "immune");
        lifeBefore = p.statLife;
        Report("ImmuneTo true skips the hit", immune == 0 && lifeBefore == 400 && Calls(nameof(HurtPlayer.OnHurt)) == onHurt, $"done={immune} life={lifeBefore}");
        double dodge = Hit(p, custom, 50, "dodge");
        Report("FreeDodge true skips the hit", dodge == 0 && p.statLife == 400, $"done={dodge} life={p.statLife}");
        double consumable = Hit(p, custom, 50, "consumable");
        Report("ConsumableDodge true skips the hit", consumable == 0 && p.statLife == 400, $"done={consumable} life={p.statLife}");
        double cancel = Hit(p, custom, 50, "cancel");
        Report("Cancel in ModifyHurt skips the hit", cancel == 0 && p.statLife == 400 && Calls(nameof(HurtPlayer.OnHurt)) == onHurt,
            $"done={cancel} life={p.statLife}");

        Hit(p, custom, 20, "");
        int plainImmune = p.immuneTime;
        Hit(p, custom, 20, "immunity");
        Report("AddImmuneTime in PostHurt adds to the immune time", p.immuneTime == plainImmune + 60, $"plain={plainImmune} with={p.immuneTime}");

        Npc(p);
        Proj(p);
    }

    static void Npc(Player p)
    {
        int index = NPC.NewNPC(default, (int)p.position.X + 200, (int)p.position.Y, NPCID.BlueSlime);
        if ((uint)index >= 200u)
        {
            Report("an NPC to be hit by", false, $"index={index}");
            return;
        }
        try
        {
            PlayerDeathReason byNpc = PlayerDeathReason.ByNPC(index);
            int can = Calls(nameof(HurtPlayer.CanBeHitByNPC)), modify = Calls(nameof(HurtPlayer.ModifyHitByNPC)), on = Calls(nameof(HurtPlayer.OnHitByNPC));
            double hit = Hit(p, byNpc, 20, "");
            Report("CanBeHitByNPC, ModifyHitByNPC and OnHitByNPC for an NPC's hit", hit > 0 && Calls(nameof(HurtPlayer.CanBeHitByNPC)) == can + 1 &&
                Calls(nameof(HurtPlayer.ModifyHitByNPC)) == modify + 1 && Calls(nameof(HurtPlayer.OnHitByNPC)) == on + 1 && HurtPlayer.LastSlot == -1,
                $"done={hit} slot={HurtPlayer.LastSlot}");
            double refused = Hit(p, byNpc, 20, "noNpc");
            Report("CanBeHitByNPC false skips the NPC's hit", refused == 0 && p.statLife == 400, $"done={refused}");
        }
        finally { Main.npc[index].active = false; }
    }

    static void Proj(Player p)
    {
        int index = Projectile.NewProjectile(default, p.Center + new Vector2(0, -200), Vector2.Zero, ProjectileID.WoodenArrowHostile, 10, 0f, Main.myPlayer);
        if ((uint)index >= 1000u)
        {
            Report("a projectile to be hit by", false, $"index={index}");
            return;
        }
        try
        {
            PlayerDeathReason byProjectile = PlayerDeathReason.ByProjectile(Main.myPlayer, index);
            int modify = Calls(nameof(HurtPlayer.ModifyHitByProjectile)), on = Calls(nameof(HurtPlayer.OnHitByProjectile));
            double hit = Hit(p, byProjectile, 20, "");
            Report("ModifyHitByProjectile and OnHitByProjectile for a projectile's hit", hit > 0 &&
                Calls(nameof(HurtPlayer.ModifyHitByProjectile)) == modify + 1 && Calls(nameof(HurtPlayer.OnHitByProjectile)) == on + 1, $"done={hit}");
            double refused = Hit(p, byProjectile, 20, "noProjectile");
            Report("CanBeHitByProjectile false skips the projectile's hit", refused == 0 && p.statLife == 400, $"done={refused}");
        }
        finally { Main.projectile[index].active = false; }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csplayerhurt {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
