using Terraria.ID;

namespace Terraria;

public readonly partial struct Projectile
{
    public bool IsMinionOrSentryRelated => minion || sentry || ProjectileID.Sets.MinionShot[type] || ProjectileID.Sets.SentryShot[type];
}
