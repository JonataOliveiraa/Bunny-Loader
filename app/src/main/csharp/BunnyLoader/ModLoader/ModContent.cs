using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using ReLogic.Content;

namespace Terraria.ModLoader;

// O conteudo registrado, como o ModContent do tModLoader: pela classe
// (GetInstance), pelo nome ("Mod/Name", Find) e os arquivos e texturas pelo
// caminho com o nome do mod na frente.
public static class ModContent
{
    static readonly Dictionary<Type, object> Instances = new();

    // FullName -> o conteudo com esse nome (um item e um NPC podem dividir um).
    static readonly Dictionary<string, List<IModType>> ByName = new(StringComparer.Ordinal);

    // A instancia da classe (o molde de um ModType, o Mod ou outro ILoadable);
    // null se ela nao foi carregada, como no tModLoader.
    public static T GetInstance<T>() where T : class
    {
        lock (Instances)
        {
            return Instances.TryGetValue(typeof(T), out object? instance) ? (T)instance : null!;
        }
    }

    // 0 se o item nao foi carregado.
    public static int ItemType<T>() where T : ModItem => GetInstance<T>()?.Type ?? 0;

    // O ModItem do tipo, ou null se o tipo nao e de um item C#.
    public static ModItem? GetModItem(int type) => ItemLoader.Get(type);

    // "ExampleMod/ExampleItem". Erro se nao ha.
    public static T Find<T>(string fullname) where T : IModType =>
        TryFind(fullname, out T value) ? value : throw new KeyNotFoundException($"{typeof(T).Name} '{fullname}' not found");

    public static T Find<T>(string modName, string name) where T : IModType => Find<T>(modName + "/" + name);

    public static bool TryFind<T>(string fullname, out T value) where T : IModType
    {
        lock (Instances)
        {
            if (ByName.TryGetValue(fullname, out List<IModType>? found))
            {
                foreach (IModType content in found)
                {
                    if (content is T match)
                    {
                        value = match;
                        return true;
                    }
                }
            }
        }
        value = default!;
        return false;
    }

    public static bool TryFind<T>(string modName, string name, out T value) where T : IModType =>
        TryFind(modName + "/" + name, out value);

    // "ExampleMod/Content/Items/ExampleItem" (o Mod.Assets daquele mod) ou
    // "Terraria/Images/Item_1" (uma imagem do jogo). Por enquanto so Texture2D,
    // carregada na primeira leitura do Value, na thread do jogo.
    public static Asset<T> Request<T>(string name, AssetRequestMode mode = AssetRequestMode.AsyncLoad) where T : struct, IGameObject
    {
        Asset<T>.CheckSupported();
        (string modName, string path) = SplitName(name);
        if (modName == "Terraria") return VanillaAsset<T>.Get(path);
        return ModOf(modName, name).Assets.Request<T>(path, mode);
    }

    // Mod: o arquivo existe. Jogo: so na thread do jogo, que e quando da para
    // perguntar (e pergunta carregando a imagem).
    public static bool HasAsset(string assetName)
    {
        (string modName, string path) = SplitName(assetName);
        if (modName != "Terraria") return Host.FindMod(modName) is Mod mod && mod.Assets.HasAsset<Microsoft.Xna.Framework.Graphics.Texture2D>(path);
        try
        {
            VanillaAsset<Microsoft.Xna.Framework.Graphics.Texture2D>.Get(path).Wait();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // "ExampleMod/Localization/en-US.json": um arquivo daquele mod.
    public static bool FileExists(string name)
    {
        (string modName, string path) = SplitName(name);
        return Host.FindMod(modName) is Mod mod && mod.FileExists(path);
    }

    public static byte[] GetFileBytes(string name)
    {
        (string modName, string path) = SplitName(name);
        return ModOf(modName, name).GetFileBytes(path);
    }

    // "Mod/resto" -> ("Mod", "resto").
    public static (string modName, string subName) SplitName(string name)
    {
        string clean = name.Replace('\\', '/');
        int slash = clean.IndexOf('/');
        if (slash <= 0 || slash == clean.Length - 1) throw new ArgumentException($"'{name}' is not 'ModName/path'", nameof(name));
        return (clean[..slash], clean[(slash + 1)..]);
    }

    static Mod ModOf(string modName, string name) =>
        Host.FindMod(modName) ?? throw new KeyNotFoundException($"'{name}': no C# mod named '{modName}' is loaded");

    internal static void Add(object instance)
    {
        lock (Instances)
        {
            Instances[instance.GetType()] = instance;
            if (instance is IModType content)
            {
                if (!ByName.TryGetValue(content.FullName, out List<IModType>? list)) ByName[content.FullName] = list = new List<IModType>();
                list.Add(content);
            }
        }
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
