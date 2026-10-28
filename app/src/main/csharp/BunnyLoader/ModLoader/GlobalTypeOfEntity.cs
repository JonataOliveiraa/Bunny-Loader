using System;

namespace Terraria.ModLoader;

public abstract class GlobalType<TEntity, TGlobal> : GlobalType where TGlobal : GlobalType<TEntity, TGlobal>
{
    public virtual bool AppliesToEntity(TEntity entity, bool lateInstantiation) => true;

    public virtual TGlobal NewInstance(TEntity target)
    {
        if (!InstancePerEntity) return (TGlobal)this;
        var instance = (TGlobal)Activator.CreateInstance(GetType(), true)!;
        instance.Mod = Mod;
        instance.StaticIndex = StaticIndex;
        return instance;
    }

    public virtual TGlobal Clone(TEntity? from, TEntity to)
    {
        if (!InstancePerEntity) throw new InvalidOperationException($"{FullName}: Clone is only for InstancePerEntity globals");
        return (TGlobal)MemberwiseClone();
    }
}
