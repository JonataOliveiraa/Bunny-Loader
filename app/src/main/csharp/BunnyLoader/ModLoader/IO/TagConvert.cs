using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Terraria.ModLoader.IO;

internal static class TagConvert
{
    public static object Normalize(object value)
    {
        switch (value)
        {
            case bool b:
                return (byte)(b ? 1 : 0);
            case byte or short or int or long or float or double or string or byte[] or int[] or TagCompound:
                return value;
            case sbyte sb:
                return (byte)sb;
            case ushort us:
                return (short)us;
            case uint ui:
                return (int)ui;
            case ulong ul:
                return (long)ul;
            case char c:
                return (short)c;
            case Enum e:
                return Convert.ToInt32(e, CultureInfo.InvariantCulture);
            case IList list:
                var normalized = new List<object>(list.Count);
                foreach (object? element in list)
                {
                    if (element == null) throw new ArgumentException("TagCompound: a list cannot hold null");
                    normalized.Add(Normalize(element));
                }
                return normalized;
        }
        throw new ArgumentException($"TagCompound: {value.GetType().Name} cannot be saved (use numbers, bool, string, byte[], int[], lists and TagCompound)");
    }

    public static object Copy(object value) => value switch
    {
        byte[] bytes => bytes.Clone(),
        int[] ints => ints.Clone(),
        TagCompound tag => tag.Clone(),
        List<object> list => list.ConvertAll(Copy),
        _ => value,
    };

    public static T To<T>(object? value) => (T)To(typeof(T), value)!;

    static object? To(Type type, object? value)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;
        if (value == null) return Empty(type);
        if (target.IsInstanceOfType(value) && target != typeof(object)) return value;
        if (target == typeof(object)) return value;
        if (target == typeof(bool)) return Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
        if (target.IsEnum) return Enum.ToObject(target, Convert.ToInt64(value, CultureInfo.InvariantCulture));
        if (target.IsPrimitive || target == typeof(decimal)) return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        if (target == typeof(string)) return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (value is not IList source) throw new InvalidCastException();
        if (target.IsArray)
        {
            Type element = target.GetElementType()!;
            Array array = Array.CreateInstance(element, source.Count);
            for (int i = 0; i < source.Count; i++) array.SetValue(To(element, source[i]), i);
            return array;
        }
        if (target.IsGenericType && IsListType(target.GetGenericTypeDefinition()))
        {
            Type element = target.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
            foreach (object? item in source) list.Add(To(element, item));
            return list;
        }
        throw new InvalidCastException();
    }

    static object? Empty(Type type)
    {
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        if (type.IsGenericType && IsListType(type.GetGenericTypeDefinition()))
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]));
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    static bool IsListType(Type definition) =>
        definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(ICollection<>) ||
        definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>);
}
