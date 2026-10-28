using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader.Interop;
using ReLogic.Content;
using Terraria.ModLoader;

namespace BunnyLoader;

// O Mod.Assets: as texturas do mod pelo caminho dentro dele. No tModLoader
// ele e um AssetRepository, mas esse nome ja e o do Main.Assets do jogo.
public sealed class ModAssets
{
    readonly Mod mod;

    // Os pedidos ja feitos, pelo caminho: o mesmo pedido devolve o mesmo Asset.
    readonly Dictionary<string, object> requested = new();

    internal ModAssets(Mod mod) => this.mod = mod;

    // Sem extensao (".png" tambem vale), relativo a pasta da entrada do mod:
    // "Content/Items/ExampleItem". O mesmo caminho devolve o mesmo Asset.
    public Asset<T> Request<T>(string assetName, AssetRequestMode mode = AssetRequestMode.AsyncLoad) where T : struct, IGameObject
    {
        Asset<T>.CheckSupported();
        string relative = Relative(assetName);
        lock (requested)
        {
            if (requested.TryGetValue(relative, out object? have)) return (Asset<T>)have;
            string? file = Find(relative);
            if (file == null) throw new FileNotFoundException($"{mod.Name}: asset '{assetName}' not found in {mod.Folder}");
            var asset = new Asset<T>($"{mod.Name}/{relative}", file);
            requested[relative] = asset;
            return asset;
        }
    }

    public bool HasAsset<T>(string assetName) where T : struct, IGameObject =>
        typeof(T) == typeof(Microsoft.Xna.Framework.Graphics.Texture2D) && Find(Relative(assetName)) != null;

    static string Relative(string assetName)
    {
        string name = assetName.Replace('\\', '/').TrimStart('/');
        return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    string? Find(string relative)
    {
        string? path = mod.ResolveFile(relative + ".png");
        return path != null && File.Exists(path) ? path : null;
    }
}
