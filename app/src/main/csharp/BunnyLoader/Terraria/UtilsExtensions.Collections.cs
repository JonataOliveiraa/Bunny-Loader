using System;
using System.Collections.Generic;
using Terraria.Utilities;

namespace Terraria;

public static partial class UtilsExtensions
{
    public static bool IndexInRange<T>(this T[] t, int index) => index >= 0 && index < t.Length;

    public static bool IndexInRange<T>(this List<T> t, int index) => index >= 0 && index < t.Count;

    public static int[] GetTrueIndexes(this bool[] values)
    {
        var result = new List<int>();
        for (int i = 0; i < values.Length; i++) if (values[i]) result.Add(i);
        return result.ToArray();
    }
}
