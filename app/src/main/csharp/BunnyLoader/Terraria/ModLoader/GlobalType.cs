using System;
using System.Linq;
using System.Reflection;

namespace Terraria.ModLoader;

public abstract class GlobalType : ModType
{
    public short StaticIndex { get; internal set; } = -1;

    public virtual bool InstancePerEntity => false;

    internal void ValidateInstanceFields()
    {
        if (InstancePerEntity) return;
        for (Type? type = GetType(); type != null && type.Assembly != typeof(GlobalType).Assembly; type = type.BaseType)
        {
            FieldInfo? field = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).FirstOrDefault();
            if (field != null)
                throw new InvalidOperationException($"{FullName}: the instance field '{field.Name}' needs InstancePerEntity => true (or make it static)");
        }
    }
}
