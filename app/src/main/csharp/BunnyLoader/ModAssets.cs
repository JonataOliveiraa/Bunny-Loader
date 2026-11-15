using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader.Interop;
using ReLogic.Content;
using Terraria.ModLoader;

namespace BunnyLoader;

public sealed class ModAssets
{
    static readonly string[] Extensions = { ".png", ".json" };

    readonly Mod mod;

    readonly Dictionary<string, object> requested = new();

    readonly Dictionary<(string Name, string Extension), object> byName = new();

    internal ModAssets(Mod mod) => this.mod = mod;

    public Asset<T> Request<T>(string assetName, AssetRequestMode mode = AssetRequestMode.AsyncLoad) where T : struct, IGameObject
    {
        Asset<T>.CheckSupported();
        string extension = Extension<T>();
        lock (requested)
        {
            if (byName.TryGetValue((assetName, extension), out object? seen)) return (Asset<T>)seen;
        }
        string relative = Relative(assetName);
        string key = relative + extension;
        lock (requested)
        {
            if (!requested.TryGetValue(key, out object? have))
            {
                string? file = Find(relative, extension);
                if (file == null) throw new FileNotFoundException($"{mod.Name}: asset '{assetName}' not found in {mod.Folder}");
                have = new Asset<T>($"{mod.Name}/{relative}", file);
                requested[key] = have;
            }
            byName[(assetName, extension)] = have;
            return (Asset<T>)have;
        }
    }

    public bool HasAsset<T>(string assetName) where T : struct, IGameObject =>
        (typeof(T) == typeof(Microsoft.Xna.Framework.Graphics.Texture2D) || typeof(T) == typeof(Microsoft.Xna.Framework.Graphics.SpriteFont)) &&
        Find(Relative(assetName), Extension<T>()) != null;

    static string Extension<T>() => typeof(T) == typeof(Microsoft.Xna.Framework.Graphics.SpriteFont) ? ".json" : ".png";

    static string Relative(string assetName)
    {
        string name = assetName.Replace('\\', '/').TrimStart('/');
        foreach (string extension in Extensions)
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return name[..^extension.Length];
        return name;
    }

    string? Find(string relative, string extension)
    {
        string? path = mod.ResolveFile(relative + extension);
        return path != null && File.Exists(path) ? path : null;
    }
}
