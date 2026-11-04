using System;
using System.Collections.Generic;
using Terraria.Utilities;

namespace Terraria;

public static partial class UtilsExtensions
{
    public static T Next<T>(this UnifiedRandom random, IReadOnlyList<T> values)
    {
        if (values.Count == 0) throw new ArgumentException("The collection must not be empty.", nameof(values));
        return values[random.Next(values.Count)];
    }

    public static int[] GetTrueIndexes(this bool[] values)
    {
        var result = new List<int>();
        for (int i = 0; i < values.Length; i++) if (values[i]) result.Add(i);
        return result.ToArray();
    }
}
