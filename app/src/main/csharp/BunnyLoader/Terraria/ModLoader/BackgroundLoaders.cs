using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class BackgroundTextureLoader
{
    static readonly Dictionary<string, int> backgrounds = new(StringComparer.Ordinal);
    static readonly List<(string Key, string? File)> pending = new();
    static int vanillaCount = -1;

    public static int VanillaCount => vanillaCount >= 0 ? vanillaCount : vanillaCount = Main.maxBackgrounds;

    public static int TotalCount => VanillaCount + pending.Count;

    public static int GetBackgroundSlot(string texture) =>
        TryGetBackgroundSlot(texture, out int slot) ? slot : throw new KeyNotFoundException($"BackgroundTextureLoader: no background '{texture}'");

    public static int GetBackgroundSlot(Mod mod, string texture) => GetBackgroundSlot($"{mod.Name}/{texture}");

    public static bool TryGetBackgroundSlot(string texture, out int slot)
    {
        lock (backgrounds)
        {
            if (backgrounds.TryGetValue(texture, out int index))
            {
                slot = VanillaCount + index;
                return true;
            }
        }
        slot = 0;
        return false;
    }

    public static bool TryGetBackgroundSlot(Mod mod, string texture, out int slot) => TryGetBackgroundSlot($"{mod.Name}/{texture}", out slot);

    public static void AddBackgroundTexture(Mod mod, string texture)
    {
        if (mod == null) throw new ArgumentNullException(nameof(mod));
        if (texture == null) throw new ArgumentNullException(nameof(texture));
        if (!mod.loading) throw new InvalidOperationException($"{mod.Name}: AddBackgroundTexture can only be called while the mod loads");
        (string modName, string path) = ModContent.SplitName(texture);
        Mod owner = modName == mod.Name ? mod : Host.FindMod(modName) ?? throw new KeyNotFoundException($"'{texture}': no C# mod named '{modName}' is loaded");
        string? file = owner.ResolveFile(path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? path : path + ".png");
        if (file == null || !File.Exists(file)) throw new FileNotFoundException($"{mod.Name}: background '{texture}' not found", texture);
        string key = texture.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? texture[..^4] : texture;
        lock (backgrounds)
        {
            if (backgrounds.ContainsKey(key)) return;
            backgrounds[key] = pending.Count;
            pending.Add((key, file));
        }
    }

    internal static void AutoloadBackgrounds(Mod mod)
    {
        foreach (string file in mod.GetFileNames())
        {
            if (!file.Contains("Backgrounds/") || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            AddBackgroundTexture(mod, $"{mod.Name}/{Path.ChangeExtension(file, null)}");
        }
    }

    internal static void ResizeArrays()
    {
        int surfaceTotal = SurfaceBackgroundStylesLoader.TotalCount;
        GrowBools(ref SurfaceBackgroundID.Sets.IsForest, surfaceTotal);
        GrowBools(ref SurfaceBackgroundID.Sets.IsDesertVariant, surfaceTotal);
        if (pending.Count == 0) return;
        int first = VanillaCount, total = TotalCount;
        WaterStylesLoader.Grow(ref TextureAssets.Background_, total);
        if (Main.backgroundWidth.Length < total) Main.backgroundWidth = Main.backgroundWidth.Resized(total);
        if (Main.backgroundHeight.Length < total) Main.backgroundHeight = Main.backgroundHeight.Resized(total);
        for (int i = 0; i < pending.Count; i++)
        {
            (string key, string? file) = pending[i];
            int slot = first + i;
            try
            {
                var asset = new Asset<Texture2D>(key, file);
                asset.Wait();
                TextureAssets.Background_[slot] = new GameObject(asset.GameAsset);
                Texture2D texture = asset.Value;
                Main.backgroundWidth[slot] = texture.Width;
                Main.backgroundHeight[slot] = texture.Height;
            }
            catch (Exception e) { Log.Error($"background {key}: the texture could not be loaded: {e.Message}"); }
        }
        Log.Info($"mod backgrounds: {pending.Count} (slots {first}..{total - 1})");
    }

    static void GrowBools(ref GameArray<bool> set, int total)
    {
        if (!set.IsNull && set.Length < total) set = set.Resized(total);
    }

    internal static Texture2D? Texture(int slot)
    {
        GameArray<GameObject> textures = TextureAssets.Background_;
        if (slot < 0 || slot >= textures.Length || textures[slot].Pointer == IntPtr.Zero) return null;
        IntPtr value = Host.Native()->TextureAssetValue(textures[slot].Pointer);
        if (value == IntPtr.Zero)
        {
            Main.instance.LoadBackground(slot);
            value = Host.Native()->TextureAssetValue(textures[slot].Pointer);
        }
        return value == IntPtr.Zero ? null : new Texture2D(value);
    }
}

public static class UndergroundBackgroundStylesLoader
{
    public const int VanillaUndergroundBackgroundStylesCount = 22;

    public const int VanillaCount = VanillaUndergroundBackgroundStylesCount;

    static readonly List<ModUndergroundBackgroundStyle> Styles = new();

    public static int TotalCount => VanillaCount + Styles.Count;

    public static ModUndergroundBackgroundStyle? Get(int style)
    {
        int index = style - VanillaCount;
        return (uint)index < (uint)Styles.Count ? Styles[index] : null;
    }

    internal static int Register(ModUndergroundBackgroundStyle style)
    {
        Styles.Add(style);
        BackgroundHooks.InstallUnderground();
        return VanillaCount + Styles.Count - 1;
    }

    public static void FillTextureArray(int style, int[] textureSlots)
    {
        Get(style)?.FillTextureArray(textureSlots);
        foreach (GlobalBackgroundStyle global in GlobalBackgroundStyleLoader.All) global.FillUndergroundTextureArray(style, textureSlots);
    }
}

public static class SurfaceBackgroundStylesLoader
{
    public const int VanillaCount = SurfaceBackgroundID.Count;

    static readonly List<ModSurfaceBackgroundStyle> Styles = new();

    public static int TotalCount => VanillaCount + Styles.Count;

    internal static IReadOnlyList<ModSurfaceBackgroundStyle> All => Styles;

    public static ModSurfaceBackgroundStyle? Get(int style)
    {
        int index = style - VanillaCount;
        return (uint)index < (uint)Styles.Count ? Styles[index] : null;
    }

    internal static int Register(ModSurfaceBackgroundStyle style)
    {
        Styles.Add(style);
        BackgroundHooks.InstallSurface();
        CaptureBiomeHooks.Install();
        return VanillaCount + Styles.Count - 1;
    }

    public static void ModifyFarFades(int style, float[] fades, float transitionSpeed)
    {
        Get(style)?.ModifyFarFades(fades, transitionSpeed);
        foreach (GlobalBackgroundStyle global in GlobalBackgroundStyleLoader.All) global.ModifyFarSurfaceFades(style, fades, transitionSpeed);
    }
}

internal static class GlobalBackgroundStyleLoader
{
    static readonly List<GlobalBackgroundStyle> Globals = new();

    internal static IReadOnlyList<GlobalBackgroundStyle> All => Globals;

    internal static void Register(GlobalBackgroundStyle global)
    {
        Globals.Add(global);
        BackgroundHooks.InstallSurface();
        BackgroundHooks.InstallUnderground();
    }
}
