using System;
using System.Collections.Generic;

namespace Terraria.ModLoader;

public static class ModTypeLookup<T> where T : IModType
{
    static readonly Dictionary<string, T> Instances = new(StringComparer.Ordinal);

    public static void Register(T instance)
    {
        RegisterName(instance, instance.Name);
        if (instance is ModType type) Core.CustomContentSetup.Register(type);
    }

    public static void RegisterLegacyNames(T instance, params string[] legacyNames)
    {
        foreach (string name in legacyNames) RegisterName(instance, name);
    }

    static void RegisterName(T instance, string name)
    {
        string key = instance.Mod.Name + "/" + name;
        lock (Instances)
        {
            if (!Instances.TryAdd(key, instance)) throw new InvalidOperationException($"Duplicate {typeof(T).Name} name '{key}'.");
        }
    }

    internal static T Get(string name) { lock (Instances) return Instances[name]; }
    internal static T Get(string mod, string name) => Get(mod + "/" + name);
    internal static bool TryGetValue(string name, out T value) { lock (Instances) return Instances.TryGetValue(name, out value!); }
    internal static bool TryGetValue(string mod, string name, out T value) => TryGetValue(mod + "/" + name, out value);
}
