using System.Collections.Generic;
using System.Linq;

namespace Terraria.ModLoader;

public static class SystemLoader
{
    static readonly List<ModSystem> Systems = new();

    internal static void Add(ModSystem system)
    {
        lock (Systems) Systems.Add(system);
    }

    internal static ModSystem[] Of(Mod mod)
    {
        lock (Systems) return Systems.Where(s => s.Mod == mod).ToArray();
    }
}
