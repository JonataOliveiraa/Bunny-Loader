using BunnyLoader;
using BunnyLoader.Loaders;
using Terraria.ModLoader;

namespace Terraria;

// Os membros de DamageClass que o tModLoader poe no Item. A proxy e partial:
// entram nela mesma, com os nomes de la.
public readonly unsafe partial struct Item
{
    /// <summary>
    /// A classe de dano. Escrita no SetDefaults de um ModItem, vale para o tipo
    /// inteiro (Clone, NewItem, rede); fora dele, so para este item enquanto
    /// ele tiver o mesmo tipo.
    /// </summary>
    public DamageClass DamageType
    {
        get => DamageClassLoader.ItemClass(this);
        set => DamageClassLoader.SetItemClass(this, value);
    }

    public bool CountsAsClass(DamageClass damageClass) => DamageClassLoader.ItemClass(this).CountsAsClass(damageClass);

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());
}
