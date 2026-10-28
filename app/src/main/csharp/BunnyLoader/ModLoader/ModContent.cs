using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;

namespace Terraria.ModLoader;

// O conteudo registrado pelo tipo da classe, como o ModContent do tModLoader.
public static class ModContent
{
    static readonly Dictionary<Type, object> Instances = new();

    // A instancia da classe (o molde de um ModType, o Mod ou outro ILoadable);
    // erro se ela nao foi registrada.
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

    // Todo o conteudo de mod no jogo, os SetStaticDefaults ja rodados e as
    // receitas do jogo montadas (content/common/ModContent.h). Uma vez, na
    // thread do jogo. Cada passo vai a todos os mods antes do proximo; o erro
    // de um mod nao para os outros.
    [UnmanagedCallersOnly]
    internal static void ContentReady()
    {
        try
        {
            Mod[] mods = Host.Mods;
            foreach (Mod mod in mods) Run(mod, "PostSetupContent", mod.PostSetupContent);
            foreach (Mod mod in mods) Run(mod, "AddRecipeGroups", mod.AddRecipeGroups);
            foreach (Mod mod in mods)
            {
                Run(mod, "AddRecipes", mod.AddRecipes);
                foreach (ModItem item in mod.GetContent<ModItem>()) Run(mod, $"{item.Name}.AddRecipes", item.AddRecipes);
            }
            foreach (Mod mod in mods) Run(mod, "PostAddRecipes", mod.PostAddRecipes);
        }
        catch (Exception e)
        {
            Log.Error($"content setup failed: {e}");
        }
    }

    static void Run(Mod mod, string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error($"mod {mod.Uid}: {step} threw: {e}");
        }
    }
}
