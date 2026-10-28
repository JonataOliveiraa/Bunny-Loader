using System;
using System.Collections.Generic;
using BunnyLoader;

namespace Terraria.ModLoader;

// Os mods C# carregados, como o ModLoader do tModLoader. O nome pedido e o
// Name do mod (o do assembly), o "id" do manifesto ou o uid.
//
// Os mods carregam um a um, na ordem do launcher com as dependencias antes:
// no Load, um mod acha a si mesmo e os que carregaram antes dele (as
// dependencias dele estao entre eles); do PostSetupContent em diante, todos.
public static class ModLoader
{
    static readonly HashSet<string> Ambiguous = new(StringComparer.Ordinal);

    // Os que carregaram inteiros, na ordem de carga.
    public static Mod[] Mods => Host.Mods;

    // Erro se nao ha (para um mod opcional, TryGetMod).
    public static Mod GetMod(string name) =>
        TryGetMod(name, out Mod result) ? result
            : throw new KeyNotFoundException($"ModLoader.GetMod: no C# mod '{name}' is loaded (use TryGetMod if it is optional)");

    public static bool TryGetMod(string name, out Mod result)
    {
        result = Find(name)!;
        return result != null;
    }

    public static bool HasMod(string name) => Find(name) != null;

    // O uid acha direto; o Name e o id do manifesto, so se um mod so os tem.
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
