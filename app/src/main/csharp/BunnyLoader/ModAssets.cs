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
