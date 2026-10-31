using Terraria;
using Terraria.ModLoader;

namespace CsNpcProj.Content;

public sealed class ProbeNpc : ModNPC
{
    public static bool? Decision;
    public static int Done = -1, HitDamage = -1, HitBy = -1;

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100000;
        NPC.damage = 0;
        NPC.aiStyle = 0;
        NPC.townNPC = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }

    public override bool? CanBeHitByProjectile(Projectile projectile) => Decision;

    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) => Checks.Mark("IncomingN");

    public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) => Checks.Mark("ModifyN");

    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        Checks.Mark("OnN");
        Done = damageDone;
        HitDamage = hit.Damage;
        HitBy = projectile.type;
    }

    public override bool CheckDead() => false;
}

public sealed class ProbeGlobal : GlobalNPC
{
    public static bool? Decision;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<ProbeNpc>();

    public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile) => Decision;

    public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) => Checks.Mark("ModifyG");

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) => Checks.Mark("OnG");
}

public sealed class TestShot : ModProjectile
{
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 8;
        Projectile.aiStyle = 0;
        Projectile.friendly = true;
        Projectile.penetrate = 1;
        Projectile.tileCollide = false;
        Projectile.timeLeft = 60;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => Checks.Mark("ModifyP");

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Checks.Mark("OnP");
}
