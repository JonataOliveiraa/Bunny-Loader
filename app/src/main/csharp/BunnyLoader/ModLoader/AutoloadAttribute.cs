using System;
using System.Linq;

namespace Terraria.ModLoader;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AutoloadAttribute : Attribute
{
    static readonly AutoloadAttribute Default = new();

    public readonly bool Value;

    public ModSide Side { get; set; } = ModSide.Both;

    public bool NeedsAutoloading => Value && Side != ModSide.Server;

    public AutoloadAttribute(bool value = true) => Value = value;

    public static AutoloadAttribute GetValue(Type type) =>
        type.GetCustomAttributes(typeof(AutoloadAttribute), true).OfType<AutoloadAttribute>().FirstOrDefault() ?? Default;
}
