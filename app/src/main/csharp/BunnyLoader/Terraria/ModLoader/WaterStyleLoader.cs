using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.GameContent.Liquid;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class WaterStylesLoader
{
    public const int VanillaCount = Main.maxLiquidTypes;

    static readonly List<ModWaterStyle> Styles = new();

    public static int TotalCount => VanillaCount + Styles.Count;

    internal static IReadOnlyList<ModWaterStyle> All => Styles;

    public static ModWaterStyle? Get(int type)
    {
        int index = type - VanillaCount;
        return (uint)index < (uint)Styles.Count ? Styles[index] : null;
    }

    internal static int Register(ModWaterStyle style)
    {
        Styles.Add(style);
        WaterHooks.Install();
        WaterHooks.InstallStyle(style.GetType());
        return VanillaCount + Styles.Count - 1;
    }

    internal static void ResizeArrays()
    {
        if (Styles.Count == 0) return;
        int total = TotalCount;
        Grow(ref LiquidRenderer._liquidTextures, total);
        Grow(ref TextureAssets.Liquid, total);
        Grow(ref TextureAssets.LiquidSlope, total);
        Log.Info($"mod water styles: {Styles.Count} (styles {VanillaCount}..{total - 1})");
    }

    internal static void SetupTextures(ModWaterStyle style)
    {
        int slot = style.Slot;
        IntPtr water = Load(style, style.Texture), block = Load(style, style.BlockTexture), slope = Load(style, style.SlopeTexture);
        LiquidRenderer._liquidTextures[slot] = new GameObject(water != IntPtr.Zero ? water : LiquidRenderer._liquidTextures[0].Pointer);
        TextureAssets.Liquid[slot] = new GameObject(block != IntPtr.Zero ? block : TextureAssets.Liquid[0].Pointer);
        TextureAssets.LiquidSlope[slot] = new GameObject(slope != IntPtr.Zero ? slope : TextureAssets.Liquid[slot].Pointer);
    }

    internal static IntPtr Load(ModType content, string texture)
    {
        try
        {
            var asset = new Asset<Texture2D>(texture, ItemLoader.ResolveTexture(content, texture));
            asset.Wait();
            return asset.GameAsset;
        }
        catch (Exception e)
        {
            Log.Error($"{content.FullName}: the texture {texture} could not be loaded: {e.Message}");
            return IntPtr.Zero;
        }
    }

    internal static void Grow(ref GameArray<GameObject> array, int total)
    {
        if (array.IsNull || array.Length >= total) return;
        int old = array.Length;
        array = array.Resized(total);
        for (int i = old; i < total; i++) array[i] = array[0];
    }

    public static void LightColorMultiplier(int style, float factor, ref float r, ref float g, ref float b)
    {
        if (Get(style) is not ModWaterStyle waterStyle) return;
        waterStyle.LightColorMultiplier(ref r, ref g, ref b);
        r *= factor;
        g *= factor;
        b *= factor;
    }
}

public static class WaterFallStylesLoader
{
    public const int VanillaCount = 28;

    static readonly List<ModWaterfallStyle> Styles = new();

    public static int TotalCount => VanillaCount + Styles.Count;

    internal static IReadOnlyList<ModWaterfallStyle> All => Styles;

    public static ModWaterfallStyle? Get(int type)
    {
        int index = type - VanillaCount;
        return (uint)index < (uint)Styles.Count ? Styles[index] : null;
    }

    internal static int Register(ModWaterfallStyle style)
    {
        Styles.Add(style);
        WaterHooks.InstallWaterfalls(style.GetType());
        return VanillaCount + Styles.Count - 1;
    }

    internal static void ResizeArrays()
    {
        if (Styles.Count == 0) return;
        WaterStylesLoader.Grow(ref WaterfallManager.waterfallTexture, TotalCount);
        Log.Info($"mod waterfall styles: {Styles.Count} (styles {VanillaCount}..{TotalCount - 1})");
    }

    internal static void SetupTexture(ModWaterfallStyle style)
    {
        IntPtr texture = WaterStylesLoader.Load(style, style.Texture);
        if (texture != IntPtr.Zero) WaterfallManager.waterfallTexture[style.Slot] = new GameObject(texture);
    }
}
