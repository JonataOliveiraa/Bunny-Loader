using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace Terraria.GameContent;

public static partial class TextureAssets
{
    public static TextureAssetArray Item { get; } = new(0, "Images/Item_", static () => Item_, static i => Main.instance.LoadItem(i));

    public static TextureAssetArray PlayerHair { get; } = new(1, "Images/Player_Hair_", static () => PlayerHair_, static i => Main.instance.LoadHair(i));

    public static TextureAssetArray PlayerHairAlt { get; } = new(2, "Images/Player_HairAlt_", static () => PlayerHairAlt_, static i => Main.instance.LoadHair(i));

    internal static void ResizePlayerHair(int length)
    {
        if (PlayerHair_.Length < length) PlayerHair_ = PlayerHair_.Resized(length);
        if (PlayerHairAlt_.Length < length) PlayerHairAlt_ = PlayerHairAlt_.Resized(length);
    }
}

public sealed unsafe class TextureAssetArray
{
    static readonly Dictionary<(int, int), Asset<Texture2D>> Cache = new();

    readonly int id;
    readonly string prefix;
    readonly Func<GameArray<GameObject>> array;
    readonly Action<int> load;

    internal TextureAssetArray(int id, string prefix, Func<GameArray<GameObject>> array, Action<int> load)
    {
        this.id = id;
        this.prefix = prefix;
        this.array = array;
        this.load = load;
    }

    public int Length => array().Length;

    public Asset<Texture2D> this[int index]
    {
        get
        {
            GameArray<GameObject> items = array();
            if ((uint)index >= (uint)items.Length) throw new IndexOutOfRangeException($"{prefix}{index}: there are {items.Length} textures");
            IntPtr asset = items[index].Pointer;
            if (asset == IntPtr.Zero || Host.Native()->TextureAssetValue(asset) == IntPtr.Zero)
            {
                load(index);
                asset = array()[index].Pointer;
            }
            lock (Cache)
            {
                if (Cache.TryGetValue((id, index), out Asset<Texture2D>? cached) && cached.GameAsset == asset) return cached;
                var wrapped = new Asset<Texture2D>(prefix + index, asset);
                Cache[(id, index)] = wrapped;
                return wrapped;
            }
        }
        set
        {
            GameArray<GameObject> items = array();
            if ((uint)index >= (uint)items.Length) throw new IndexOutOfRangeException($"{prefix}{index}: there are {items.Length} textures");
            value.Wait();
            items[index] = new GameObject(value.GameAsset);
            lock (Cache) Cache[(id, index)] = value;
        }
    }
}
