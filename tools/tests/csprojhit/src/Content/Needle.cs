using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsProjHit.Content;

public static class Log
{
    public static readonly Dictionary<string, int> Calls = new();
    public static readonly List<string> HitOrder = new();
    public static readonly List<string> HurtOrder = new();
    public static readonly List<string> Sources = new();

    public static void Add(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public static int Of(string name) => Calls.GetValueOrDefault(name);

    public static void Hit(Projectile projectile, string who)
    {
        if (projectile.type == ModContent.ProjectileType<Needle>() && HitOrder.Count < 4) HitOrder.Add(who);
    }
}

public class Needle : ModProjectile
{
    public static int Blocked = -1, LifeBefore;
    public static bool Consistent = true;
    public static NPC.HitInfo LastHit;
    public static int LastDone;
    public static readonly Dictionary<int, int> HitsOn = new();

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.aiStyle = 0;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI()
    {
        NPC target = Main.npc[(int)Projectile.ai[0]];
        if (!target.active) return;
        Projectile.Center = target.Center;
        Projectile.velocity = Vector2.Zero;
    }

    public override bool? CanHitNPC(NPC target)
    {
        Log.Add(nameof(CanHitNPC));
        return target.whoAmI == Blocked ? false : null;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        Log.Hit(Projectile, "mod");
        LifeBefore = target.life;
        modifiers.DisableCrit();
        modifiers.FinalDamage.Flat += 1000;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        LastHit = hit;
        LastDone = damageDone;
        Consistent &= LifeBefore - target.life == damageDone;
        HitsOn[target.whoAmI] = HitsOn.GetValueOrDefault(target.whoAmI) + 1;
    }
}

public class Spike : ModProjectile
{
    public static bool Block;
    public static int LastDamage = -1;

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.aiStyle = 0;
        Projectile.friendly = false;
        Projectile.hostile = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3600;
        Projectile.tileCollide = false;
    }

    public override bool CanHitPlayer(Player target) => !Block;

    public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) => Log.HurtOrder.Add("mod");

    public override void OnHitPlayer(Player target, Player.HurtInfo info)
    {
        Log.Add(nameof(OnHitPlayer));
        LastDamage = info.Damage;
    }
}

public class HitGlobal : GlobalProjectile
{
    public int Hits;
    public bool Defaulted;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) =>
        entity.type == ModContent.ProjectileType<Needle>() || entity.type == ModContent.ProjectileType<Spike>() || entity.type == ProjectileID.WoodenArrowFriendly;

    public override void SetDefaults(Projectile entity) => Defaulted = true;

    public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source)
    {
        if (projectile.type != ProjectileID.WoodenArrowFriendly) return;
        Log.Add("ArrowSpawn");
        Log.Sources.Add($"{projectile.owner}@{(int)projectile.position.X},{(int)projectile.position.Y} npc={projectile.npcProj}");
    }

    public override void AI(Projectile projectile)
    {
        if (projectile.type == ProjectileID.WoodenArrowFriendly) Log.Add("ArrowAI");
    }

    public override bool PreKill(Projectile projectile, int timeLeft)
    {
        if (projectile.type == ProjectileID.WoodenArrowFriendly) Log.Add("ArrowPreKill");
        return true;
    }

    public override void OnKill(Projectile projectile, int timeLeft)
    {
        if (projectile.type == ProjectileID.WoodenArrowFriendly) Log.Add("ArrowOnKill");
    }

    public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) => Log.Hit(projectile, "global");

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
    {
        Hits++;
        if (projectile.type == ProjectileID.WoodenArrowFriendly) Log.Add("ArrowHit");
    }

    public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers) => Log.HurtOrder.Add("global");

    public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info) => Log.Add("GlobalOnHitPlayer");
}

public class HitPlayer : ModPlayer
{
    public override bool? CanHitNPCWithProj(Projectile proj, NPC target)
    {
        Log.Add(nameof(CanHitNPCWithProj));
        return null;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        for (int i = 0; i < 1000; i++)
        {
            Projectile p = Main.projectile[i];
            if (p.active && p.type == ModContent.ProjectileType<Needle>() && (int)p.ai[0] == target.whoAmI)
            {
                Log.Hit(p, "player");
                break;
            }
        }
    }

    public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) => Log.Hit(proj, "playerProj");

    public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (proj.type == ModContent.ProjectileType<Needle>()) Log.Add(nameof(OnHitNPCWithProj));
    }

    public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) => Log.HurtOrder.Add("player");
}
