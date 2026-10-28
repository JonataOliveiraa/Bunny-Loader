using System;
using System.Collections.Generic;

namespace Terraria.ModLoader;

// O conteudo registrado pelo tipo da classe, como o ModContent do tModLoader.
public static class ModContent
{
    static readonly Dictionary<Type, object> Instances = new();

    // A instancia da classe (o molde de um ModType, ou o Mod); erro se ela
    // nao foi registrada.
    public static T GetInstance<T>() where T : class
    {
        lock (Instances)
        {
            if (Instances.TryGetValue(typeof(T), out object? instance)) return (T)instance;
        }
        throw new KeyNotFoundException($"{typeof(T).FullName} is not registered");
    }

    public static int ItemType<T>() where T : ModItem => GetInstance<T>().Type;

    // O ModItem do tipo, ou null se o tipo nao e de um item C#.
    public static ModItem? GetModItem(int type) => ItemLoader.Get(type);

    internal static void Add(object instance)
    {
        lock (Instances) Instances[instance.GetType()] = instance;
    }
}
