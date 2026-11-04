using System;
using System.Collections.Generic;
using Terraria.ID;

namespace Terraria.ModLoader;

public static unsafe partial class ItemLoader
{
    static readonly Dictionary<string, int> UseStyles = new();
    public static int UseStyleCount { get; private set; } = ItemUseStyleID.Count;

    public static int RegisterUseStyle(Mod mod, string useStyleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(useStyleName);
        string key = mod.Name + "/" + useStyleName;
        lock (Gate)
        {
            if (UseStyles.ContainsKey(key)) throw new ArgumentException($"Item use style '{key}' is already registered.");
            if (UseStyleCount > byte.MaxValue) throw new InvalidOperationException("The mobile item use-style limit has been reached.");
            int style = UseStyleCount++;
            UseStyles.Add(key, style);
            return style;
        }
    }
}
