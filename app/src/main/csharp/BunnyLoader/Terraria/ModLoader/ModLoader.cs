using System;
using System.Collections.Generic;
using BunnyLoader;

namespace Terraria.ModLoader;

public static class ModLoader
{
    static readonly HashSet<string> Ambiguous = new(StringComparer.Ordinal);

    public static Mod[] Mods => Host.Mods;

    public static Mod GetMod(string name) =>
        TryGetMod(name, out Mod result) ? result
            : throw new KeyNotFoundException($"ModLoader.GetMod: no C# mod '{name}' is loaded (use TryGetMod if it is optional)");

    public static bool TryGetMod(string name, out Mod result)
    {
        result = Find(name)!;
        return result != null;
    }

    public static bool HasMod(string name) => Find(name) != null;

    static Mod? Find(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Mod? found = null;
        int count = 0;
        foreach (Mod mod in Host.CreatedMods)
        {
            if (mod.Uid == name) return mod;
            if (mod.Name != name && mod.ManifestId != name) continue;
            found = mod;
            count++;
        }
        if (count < 2) return found;
        bool first;
        lock (Ambiguous) first = Ambiguous.Add(name);
        if (first) Log.Warn($"ModLoader: {count} C# mods are named '{name}'; ask for one by its uid");
        return null;
    }
}
