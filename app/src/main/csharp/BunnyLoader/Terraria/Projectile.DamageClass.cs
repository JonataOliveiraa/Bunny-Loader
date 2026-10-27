using BunnyLoader;
using BunnyLoader.Loaders;
using Terraria.ModLoader;

namespace Terraria;

// Os membros de DamageClass que o tModLoader poe no Projectile.
public readonly unsafe partial struct Projectile
{
    /// <summary>
    /// A classe de dano do projetil. Fora do SetDefaults do tipo, vale so para
    /// este projetil enquanto ele tiver o mesmo tipo.
    /// </summary>
    public DamageClass DamageType
    {
        get => DamageClassLoader.ProjectileClass(this);
        set => DamageClassLoader.SetProjectileClass(this, value);
    }

    public bool CountsAsClass(DamageClass damageClass) => DamageClassLoader.ProjectileClass(this).CountsAsClass(damageClass);

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());
}
