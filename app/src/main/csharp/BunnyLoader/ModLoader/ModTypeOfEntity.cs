using System;

namespace Terraria.ModLoader;

// O conteudo preso a uma entidade do jogo (o ModItem a um Item...). O
// registrado e o molde; cada entidade do tipo ganha a sua instancia, criada
// por NewInstance.
public abstract class ModType<TEntity, TModType> : ModType where TModType : ModType<TEntity, TModType>
{
    // A entidade desta instancia; no molde, a do CreateTemplateEntity.
    public TEntity Entity { get; internal set; } = default!;

    // true: cada instancia nova e uma copia do molde (Clone), com os campos
    // dele; false: uma instancia limpa, criada pelo construtor sem parametros.
    protected virtual bool CloneNewInstances => false;

    // Copia rasa: campos de referencia (listas, arrays) ficam divididos com o
    // original. Sobrescreva para copiar fundo o que precisar.
    public virtual TModType Clone(TEntity newEntity)
    {
        var copy = (TModType)MemberwiseClone();
        copy.Entity = newEntity;
        return copy;
    }

    public virtual TModType NewInstance(TEntity entity)
    {
        if (CloneNewInstances) return Clone(entity);
        var instance = (TModType)Activator.CreateInstance(GetType(), true)!;
        instance.Mod = Mod;
        instance.Entity = entity;
        return instance;
    }

    protected sealed override void InitTemplateInstance() => Entity = CreateTemplateEntity();

    protected abstract TEntity CreateTemplateEntity();
}
