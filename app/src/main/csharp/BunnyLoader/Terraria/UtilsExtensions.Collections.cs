using System;
using System.Collections.Generic;
using Terraria.Utilities;

namespace Terraria;

public static partial class UtilsExtensions
{
    public static int[] GetTrueIndexes(this bool[] values)
    {
        var result = new List<int>();
        for (int i = 0; i < values.Length; i++) if (values[i]) result.Add(i);
        return result.ToArray();
    }
}
