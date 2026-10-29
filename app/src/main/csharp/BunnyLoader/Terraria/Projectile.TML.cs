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
}
