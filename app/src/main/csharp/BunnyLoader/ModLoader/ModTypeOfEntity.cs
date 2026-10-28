using System;

namespace Terraria.ModLoader;

public abstract class ModType<TEntity, TModType> : ModType where TModType : ModType<TEntity, TModType>
{
    TEntity entity = default!;

    public TEntity Entity
    {
        get => entity;
        internal set => entity = value;
    }

    internal ref TEntity EntityRef => ref entity;

    protected virtual bool CloneNewInstances => false;

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
