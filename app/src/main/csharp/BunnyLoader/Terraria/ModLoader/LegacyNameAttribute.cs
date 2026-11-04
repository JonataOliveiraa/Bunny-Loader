using System;

namespace Terraria.ModLoader;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class LegacyNameAttribute : Attribute
{
    public readonly string[] Names;

    public LegacyNameAttribute(params string[] names) => Names = names;
}
