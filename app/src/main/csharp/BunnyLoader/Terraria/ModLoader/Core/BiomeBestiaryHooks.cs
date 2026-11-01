using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent.Bestiary;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class BiomeBestiaryHooks
{
    sealed class Art
    {
        public Texture2D? Icon;
        public IntPtr Background;
    }

    const int ShadowFrame = 64;

    static readonly Dictionary<IntPtr, Art> Arts = new();
    static readonly List<GameHandle> Roots = new();
    static readonly List<ModBiome> Used = new();

    internal static ModBiomeBestiaryInfoElement Create(ModBiome biome)
    {
        Color? color = biome.BackgroundColor;
        GameNullable<Color> background = color.HasValue ? new GameNullable<Color>(color.Value) : default;
        SpawnConditionBestiaryInfoElement element = SpawnConditionBestiaryInfoElement.New(biome.DisplayName.Key, ShadowFrame, null, background);
        Roots.Add(new GameHandle(element.Pointer));
        var art = new Art
        {
            Icon = Load(biome, biome.BestiaryIcon, 30, 30)?.Value,
            Background = Load(biome, biome.BackgroundPath, 115, 65)?.GameAsset ?? IntPtr.Zero,
        };
        Arts[element.Pointer] = art;
        ItemHooks.Once("BiomeBestiary", () =>
        {
            On_FilterProviderInfoElement.GetDisplay += GetDisplay;
            On_SpawnConditionBestiaryInfoElement.GetBackgroundImage += GetBackgroundImage;
            On_GUIBestiary.SpawnDraw += SpawnDraw;
        });
        return new ModBiomeBestiaryInfoElement(element);
    }

    static Asset<Texture2D>? Load(ModBiome biome, string? path, int width, int height)
    {
        if (string.IsNullOrEmpty(path) || !ModContent.HasAsset(path)) return null;
        try
        {
            Asset<Texture2D> asset = ModContent.Request<Texture2D>(path);
            Texture2D texture = asset.Value;
            if (texture.Width == width && texture.Height == height) return asset;
            biome.Mod.Logger.Info($"{path} needs to be {width}x{height} pixels.");
        }
        catch (Exception e) { ItemHooks.Report($"{biome.FullName} bestiary art ({path})", e); }
        return null;
    }

    internal static void AddTo(BestiaryEntry entry, ModNPC npc)
    {
        foreach (int type in npc.SpawnModBiomes)
        {
            if (BiomeLoader.Get(type) is not ModBiome biome || biome.ModBiomeBestiaryInfoElement.IsNull) continue;
            entry.Info.Add(biome.ModBiomeBestiaryInfoElement);
            if (!Used.Contains(biome)) Used.Add(biome);
        }
    }

    internal static void RegisterFilters(BestiaryDatabase database)
    {
        foreach (ModBiome biome in Used)
            database.Register(Filters.ByInfoElement.New(biome.ModBiomeBestiaryInfoElement));
        if (Used.Count > 0) Log.Info($"bestiary: {Used.Count} mod biome filter(s)");
        Used.Clear();
    }

    static void GetDisplay(On_FilterProviderInfoElement.orig_GetDisplay orig, FilterProviderInfoElement self, out Texture2D texture, out Rectangle frame)
    {
        orig(self, out texture, out frame);
        if (!Arts.TryGetValue(self.Pointer, out Art? art) || art.Icon is not Texture2D icon) return;
        texture = icon;
        frame = new Rectangle(0, 0, 30, 30);
    }

    static GameObject GetBackgroundImage(On_SpawnConditionBestiaryInfoElement.orig_GetBackgroundImage orig, SpawnConditionBestiaryInfoElement self)
    {
        if (Arts.TryGetValue(self.Pointer, out Art? art) && art.Background != IntPtr.Zero) return new GameObject(art.Background);
        return orig(self);
    }

    static void SpawnDraw(On_GUIBestiary.orig_SpawnDraw orig, GUIBestiary self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        Art? art = null;
        string? key = null;
        try
        {
            var list = new GameList(self.FilterProviders);
            if (!list.IsNull && index >= 0 && index < list.Count && Arts.TryGetValue(list[index], out art) && art.Icon != null)
                key = new FilterProviderInfoElement(list[index])._key;
        }
        catch (Exception e) { ItemHooks.Report("ModBiome bestiary (spawn list)", e); }
        if (key == null || art?.Icon is not Texture2D icon)
        {
            orig(self, gridLayout, index, position, scale);
            return;
        }
        Bestiary_Layout layout = Bestiary_Layout.Instance;
        Panel_Layout backing = layout.EntrySpawnBacking;
        Vector2 size = backing.Size;
        ControlAnchor.SetGridItemRegion(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y));
        GUIPanel.DrawAtPosition(backing, position, size, false);
        Main.spriteBatch.Draw(icon, GUIPanel.Region(layout.EntrySpawnIcon), Color.White);
        GUIString.Draw(layout.EntrySpawnType, Language.GetTextValue(key));
    }
}
