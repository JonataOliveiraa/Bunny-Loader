using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Projectile
{
    public DamageClass DamageType
    {
        get => DamageClassLoader.ProjectileClass(this);
        set => DamageClassLoader.SetProjectileClass(this, value);
    }

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());

    public bool CountsAsClass(DamageClass damageClass) => DamageType.CountsAsClass(damageClass);

    public static int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner = -1, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) =>
        NewProjectile(spawnSource, position.X, position.Y, velocity.X, velocity.Y, Type, Damage, KnockBack, Owner, ai0, ai1, ai2);

    public static int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner = -1, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) =>
        NewProjectile(spawnSource, X, Y, SpeedX, SpeedY, Type, Damage, KnockBack, Owner == -1 ? Main.myPlayer : Owner, ai0, ai1, ai2, default);

    public static Projectile NewProjectileDirect(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int type, int damage, float knockback, int owner = -1, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) =>
        Main.projectile[NewProjectile(spawnSource, position, velocity, type, damage, knockback, owner, ai0, ai1, ai2)];
}
