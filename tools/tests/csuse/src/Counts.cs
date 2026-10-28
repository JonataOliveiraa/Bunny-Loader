using System.Collections.Generic;

namespace CsUse;

public static class Counts
{
    static readonly Dictionary<string, int> values = new();

    public static void Add(string name) => values[name] = Get(name) + 1;

    public static int Get(string name) => values.TryGetValue(name, out int value) ? value : 0;

    public static void Clear() => values.Clear();
}
