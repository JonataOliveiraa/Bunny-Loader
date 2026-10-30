using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace Terraria.GameContent;

public static partial class TextureAssets
{
    public static ItemTextureAssets Item => default;
}

public readonly unsafe struct ItemTextureAssets
{
    static readonly Dictionary<int, Asset<Texture2D>> Cache = new();

    public int Length => TextureAssets.Item_.Length;

    public Asset<Texture2D> this[int type]
    {
        get
        {
            GameArray<GameObject> items = TextureAssets.Item_;
            if ((uint)type >= (uint)items.Length) throw new IndexOutOfRangeException($"TextureAssets.Item[{type}]: there are {items.Length} item textures");
            IntPtr asset = items[type].Pointer;
            if (asset == IntPtr.Zero || Host.Native()->TextureAssetValue(asset) == IntPtr.Zero)
            {
                Main.instance.LoadItem(type);
                asset = TextureAssets.Item_[type].Pointer;
            }
            lock (Cache)
            {
                if (Cache.TryGetValue(type, out Asset<Texture2D>? cached) && cached.GameAsset == asset) return cached;
                var wrapped = new Asset<Texture2D>($"Images/Item_{type}", asset);
                Cache[type] = wrapped;
                return wrapped;
            }
        }
    }
}
