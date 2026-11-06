using System;
using System.Linq;
using System.Reflection;

namespace Terraria.ModLoader.IO;

public interface TagSerializable
{
    TagCompound SerializeData();
}

internal static class TagSerializableIO
{
    internal static TagCompound Serialize(TagSerializable value)
    {
        TagCompound tag = value.SerializeData();
        tag["<type>"] = value.GetType().FullName!;
        return tag;
    }

    internal static object Deserialize(Type type, TagCompound tag)
    {
        Type target = type;
        if (tag.TryGet("<type>", out string name) && name != type.FullName)
        {
            Type? subtype = type.Assembly.GetType(name) ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
            if (subtype != null && type.IsAssignableFrom(subtype))
                target = subtype;
        }

        FieldInfo? field = target.GetField("DESERIALIZER", BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is not Delegate deserializer)
            throw new ArgumentException($"Missing deserializer for type '{target.FullName}'");
        return deserializer.DynamicInvoke(tag)!;
    }
}
