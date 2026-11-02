using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader.Interop;
using ReLogic.Content;
using Terraria.ModLoader;

namespace BunnyLoader;

public sealed class ModAssets
{
    readonly Mod mod;

    readonly Dictionary<string, object> requested = new();

    internal ModAssets(Mod mod) => this.mod = mod;

    public Asset<T> Request<T>(string assetName, AssetRequestMode mode = AssetRequestMode.AsyncLoad) where T : struct, IGameObject
    {
        Asset<T>.CheckSupported();
        string relative = Relative(assetName);
        string extension = Extension<T>();
        lock (requested)
        {
            if (requested.TryGetValue(relative + extension, out object? have)) return (Asset<T>)have;
            string? file = Find(relative, extension);
            if (file == null) throw new FileNotFoundException($"{mod.Name}: asset '{assetName}' not found in {mod.Folder}");
            var asset = new Asset<T>($"{mod.Name}/{relative}", file);
            requested[relative + extension] = asset;
            return asset;
        }
    }

    public bool HasAsset<T>(string assetName) where T : struct, IGameObject =>
        (typeof(T) == typeof(Microsoft.Xna.Framework.Graphics.Texture2D) || typeof(T) == typeof(Microsoft.Xna.Framework.Graphics.SpriteFont)) &&
        Find(Relative(assetName), Extension<T>()) != null;

    static string Extension<T>() => typeof(T) == typeof(Microsoft.Xna.Framework.Graphics.SpriteFont) ? ".json" : ".png";

    static string Relative(string assetName)
    {
        string name = assetName.Replace('\\', '/').TrimStart('/');
        foreach (string extension in new[] { ".png", ".json" })
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return name[..^extension.Length];
        return name;
    }

    string? Find(string relative, string extension)
    {
        string? path = mod.ResolveFile(relative + extension);
        return path != null && File.Exists(path) ? path : null;
    }
}
