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

    public static TextureAssetArray Projectile { get; } = new(3, "Images/Projectile_", static () => Projectile_, static i => Main.instance.LoadProjectile(i));

    public static TextureAssetArray Npc { get; } = new(4, "Images/NPC_", static () => Npc_, static i => Main.instance.LoadNPC(i));

    public static TextureAssetArray Gore { get; } = new(5, "Images/Gore_", static () => Gore_, static i => Main.instance.LoadGore(i));

    public static TextureAssetArray NpcHead { get; } = new(6, "Images/NPC_Head_", static () => NpcHead_, static i => { });

    public static TextureAssetArray PlayerHair { get; } = new(1, "Images/Player_Hair_", static () => PlayerHair_, static i => Main.instance.LoadHair(i));

    public static TextureAssetArray PlayerHairAlt { get; } = new(2, "Images/Player_HairAlt_", static () => PlayerHairAlt_, static i => Main.instance.LoadHair(i));

    public static Asset<Texture2D> Heart => Wrap(Heart_, "Images/Heart");

    public static Asset<Texture2D> Heart2 => Wrap(Heart2_, "Images/Heart2");

    public static Asset<Texture2D> Mana => Wrap(Mana_, "Images/Mana");

    static Asset<Texture2D> Wrap(GameObject asset, string name) =>
        asset.IsNull ? throw new InvalidOperationException($"TextureAssets: {name} is not loaded yet") : Asset<Texture2D>.ForGameAsset(asset.Pointer, name);

    internal static void ResizeGore(int length)
    {
        if (Gore_.Length < length) Gore_ = Gore_.Resized(length);
    }

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
                Asset<Texture2D> wrapped = Asset<Texture2D>.ForGameAsset(asset, prefix + index);
                Cache[(id, index)] = wrapped;
                return wrapped;
            }
        }
        set
        {
            GameArray<GameObject> items = array();
            if ((uint)index >= (uint)items.Length) throw new IndexOutOfRangeException($"{prefix}{index}: there are {items.Length} textures");
            if (value.IsDirect) throw new InvalidOperationException($"{prefix}{index}: this asset is a bare texture, not a game asset");
            value.Wait();
            items[index] = new GameObject(value.GameAsset);
            lock (Cache) Cache[(id, index)] = value;
        }
    }
}
