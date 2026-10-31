using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CsCollide.Content;

public sealed class CutterBolt : ModProjectile
{
    public static readonly List<string> Order = new();
    public static int BlockedCuts;

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.aiStyle = 0;
        Projectile.friendly = true;
        Projectile.damage = 10;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.timeLeft = 120;
    }

    public override bool? CanCutTiles() => Projectile.ai[0] == 1f ? false : null;

    public override void CutTiles()
    {
        if (Projectile.ai[0] == 1f) BlockedCuts++;
        else Order.Add("mod");
    }
}

public sealed class CutGlobal : GlobalProjectile
{
    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ModContent.ProjectileType<CutterBolt>();

    public override bool? CanCutTiles(Projectile projectile) => null;

    public override void CutTiles(Projectile projectile)
    {
        if (projectile.ai[0] != 1f) CutterBolt.Order.Add("global");
    }
}
