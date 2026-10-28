using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using ReLogic.Content;

namespace BunnyLoader;

internal static class VanillaAsset<T> where T : struct, IGameObject
{
    static readonly Dictionary<string, Asset<T>> Requested = new(StringComparer.Ordinal);

    public static Asset<T> Get(string path)
    {
        string name = path.Replace('\\', '/').TrimStart('/');
        int dot = name.LastIndexOf('.');
        if (dot > name.LastIndexOf('/')) name = name[..dot];
        lock (Requested)
        {
            if (!Requested.TryGetValue(name, out Asset<T>? asset)) Requested[name] = asset = new Asset<T>(name, null);
            return asset;
        }
    }
}
