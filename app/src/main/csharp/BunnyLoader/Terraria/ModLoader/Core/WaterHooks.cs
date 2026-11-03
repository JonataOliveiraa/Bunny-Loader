using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Light;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class WaterHooks
{
    const float LightFactor = 0.91f;

    static readonly int[] VanillaBackgrounds = { 1, 2, 3, 4, 5, 6, 7, 8, 13, 14 };

    static bool counted;
    static Hook? hairDye;

    [ThreadStatic] static bool dripping;
    [ThreadStatic] static ModWaterStyle? raining;
    [ThreadStatic] static ModWaterfallStyle? tinting;
    [ThreadStatic] static float tintAlpha;

    static readonly Dictionary<(Type, string), bool> Overrides = new();

    internal static bool Has(Type type, Type baseType, string name)
    {
        lock (Overrides)
        {
            if (!Overrides.TryGetValue((type, name), out bool has)) Overrides[(type, name)] = has = ItemHooks.Overrides(type, baseType, name);
            return has;
        }
    }

    internal static void Install() => ItemHooks.Once("Water", () =>
    {
        On_Main.CalculateWaterStyle += CalculateWaterStyle;
        On_Main.DrawWaters += DrawWaters;
        On_WaterfallManager.Draw += DrawWaterfalls;
        On_Dust.dustWater += DustWater;
        On_TileDrawing.EmitLiquidDrops += EmitLiquidDrops;
        On_Gore.NewGore += NewGore;
        On_Rain.MakeRain += MakeRain;
        On_Rain.NewRainForced += NewRainForced;
    });

    internal static void InstallStyle(Type type)
    {
        if (Has(type, typeof(ModWaterStyle), nameof(ModWaterStyle.GetRainVariant))) ItemHooks.Once("WaterRainVariant", () => On_Rain.NewRain += NewRain);
        if (Has(type, typeof(ModWaterStyle), nameof(ModWaterStyle.GetRainTexture))) ItemHooks.Once("WaterRainTexture", () => On_Main.DrawRain += DrawRain);
        if (Has(type, typeof(ModWaterStyle), nameof(ModWaterStyle.LightColorMultiplier))) ItemHooks.Once("WaterLight", () =>
        {
            On_LightingEngine.UpdateLightDecay += UpdateLightDecay;
            On_LegacyLighting.UpdateLightDecay += UpdateLegacyLightDecay;
        });
        if (Has(type, typeof(ModWaterStyle), nameof(ModWaterStyle.BiomeHairColor))) ItemHooks.Once("WaterHairDye", () =>
        {
            try
            {
                hairDye = Hook.Install(new GameClass("Terraria.Initializers", "DyeInitializer", "<>c"),
                    "Color <LoadLegacyHairdyes>b__5_6(Player player, Color newColor, ref bool lighting)",
                    (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, uint, byte*, IntPtr, uint>)&BiomeHairDye);
            }
            catch (Exception e) { Log.Error($"mod water: the biome hair dye is not hooked: {e.Message}"); }
        });
    }

    internal static void InstallWaterfalls(Type type)
    {
        if (Has(type, typeof(ModWaterfallStyle), nameof(ModWaterfallStyle.ColorMultiplier)))
            ItemHooks.Once("WaterfallColor", () => On_WaterfallManager.DrawWaterfall_SpriteBatch_int_float += DrawWaterfall);
    }

    static ModWaterStyle? Current() => WaterStylesLoader.Get(Main.waterStyle);

    static int CalculateWaterStyle(On_Main.orig_CalculateWaterStyle orig, bool ignoreFountains)
    {
        int vanilla = orig(ignoreFountains);
        try { return Choose(vanilla, ignoreFountains); }
        catch (Exception e)
        {
            ItemHooks.Report("ModWaterStyle (choice)", e);
            return vanilla;
        }
    }

    static int Choose(int vanilla, bool ignoreFountains)
    {
        if (Main.gameMenu) return vanilla;
        var scene = SceneEffectLoader.Of(Main.LocalPlayer).waterStyle;
        if (scene.priority < SceneEffectPriority.BiomeLow || WaterStylesLoader.Get(scene.value) == null) return vanilla;
        if (!ignoreFountains && Main.SceneMetrics.ActiveFountainColor >= 0) return vanilla;
        if (Main.bloodMoon && !Main.dayTime) return vanilla;
        if (Array.IndexOf(VanillaBackgrounds, Main.bgStyle) >= 0) return vanilla;
        return scene.value;
    }

    static void DrawWaters(On_Main.orig_DrawWaters orig, Main self, bool isBackground)
    {
        try { Ensure(); }
        catch (Exception e) { ItemHooks.Report("ModWaterStyle (tables)", e); }
        orig(self, isBackground);
    }

    static void Ensure()
    {
        int total = WaterStylesLoader.TotalCount;
        GameArray<float> alpha = Main.liquidAlpha;
        if (!alpha.IsNull && alpha.Length < total) Main.liquidAlpha = alpha.Resized(total);
        GameArray<int> active = Main.activeLiquidAlpha;
        if (!active.IsNull && active.Length < total) Main.activeLiquidAlpha = active.Resized(total);
        if (counted) return;
        counted = true;
        if (Host.Native()->SetWaterStyleCount(total) == 0) Log.Error("mod water: Main.DrawWaters did not take the mod styles (see the native log)");
    }

    static void DrawWaterfalls(On_WaterfallManager.orig_Draw orig, WaterfallManager self, SpriteBatch spriteBatch)
    {
        orig(self, spriteBatch);
        if (Main.gameMenu) return;
        GameArray<float> alpha = Main.liquidAlpha;
        foreach (ModWaterStyle style in WaterStylesLoader.All)
        {
            if (style.Slot >= alpha.Length || !(alpha[style.Slot] > 0)) continue;
            int waterfall;
            try { waterfall = style.ChooseWaterfallStyle(); }
            catch (Exception e)
            {
                ItemHooks.Report($"{style.FullName}.ChooseWaterfallStyle", e);
                continue;
            }
            self.DrawWaterfall(spriteBatch, waterfall, alpha[style.Slot]);
            Light(self, waterfall);
        }
    }

    static void Light(WaterfallManager manager, int waterfall)
    {
        if (WaterFallStylesLoader.Get(waterfall) is not ModWaterfallStyle style || !Has(style.GetType(), typeof(ModWaterfallStyle), nameof(ModWaterfallStyle.AddLight))) return;
        var list = manager.waterfalls;
        int count = Math.Min(manager.currentMax, list.Length);
        try
        {
            for (int k = 0; k < count; k++)
            {
                ref WaterfallManager.WaterfallData w = ref list[k];
                if (w.type != 0) continue;
                int steps = Math.Min(w.stopAtStep, 24);
                for (int s = 0; s <= steps; s += 3) style.AddLight(w.x, w.y + s);
            }
        }
        catch (Exception e) { ItemHooks.Report($"{style.FullName}.AddLight", e); }
    }

    static void DrawWaterfall(On_WaterfallManager.orig_DrawWaterfall_SpriteBatch_int_float orig, WaterfallManager self, SpriteBatch spriteBatch, int Style, float Alpha)
    {
        ModWaterfallStyle? style = WaterFallStylesLoader.Get(Style);
        IntPtr texture = IntPtr.Zero;
        if (style != null && Has(style.GetType(), typeof(ModWaterfallStyle), nameof(ModWaterfallStyle.ColorMultiplier)))
        {
            GameArray<GameObject> textures = WaterfallManager.waterfallTexture;
            if (Style < textures.Length && textures[Style].Pointer != IntPtr.Zero) texture = Host.Native()->TextureAssetValue(textures[Style].Pointer);
        }
        if (texture == IntPtr.Zero || Host.Native()->BeginDrawTint(texture, &Tint) == 0)
        {
            orig(self, spriteBatch, Style, Alpha);
            return;
        }
        ModWaterfallStyle? outer = tinting;
        float outerAlpha = tintAlpha;
        tinting = style;
        tintAlpha = Alpha;
        try { orig(self, spriteBatch, Style, Alpha); }
        finally
        {
            Host.Native()->EndDrawTint();
            tinting = outer;
            tintAlpha = outerAlpha;
        }
    }

    [UnmanagedCallersOnly]
    static void Tint(uint* colors, int count)
    {
        ModWaterfallStyle? style = tinting;
        if (style == null) return;
        try
        {
            for (int k = 0; k < count; k++)
            {
                ref Color c = ref *(Color*)(colors + k);
                float r = c.R, g = c.G, b = c.B;
                style.ColorMultiplier(ref r, ref g, ref b, tintAlpha);
                c = new Color((int)r, (int)g, (int)b, c.A);
            }
        }
        catch (Exception e) { ItemHooks.Report($"{style.FullName}.ColorMultiplier", e); }
    }

    static int DustWater(On_Dust.orig_dustWater orig)
    {
        if (Current() is not ModWaterStyle style) return orig();
        try { return style.GetSplashDust(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{style.FullName}.GetSplashDust", e);
            return orig();
        }
    }

    static void EmitLiquidDrops(On_TileDrawing.orig_EmitLiquidDrops orig, TileDrawing self, int j, int i, byte liquid, ushort typeCache)
    {
        bool outer = dripping;
        dripping = true;
        try { orig(self, j, i, liquid, typeCache); }
        finally { dripping = outer; }
    }

    static int NewGore(On_Gore.orig_NewGore orig, Vector2 Position, Vector2 Velocity, int Type, float Scale)
    {
        if (!dripping) return orig(Position, Velocity, Type, Scale);
        int water = Main.waterStyle;
        if (Type != 705 + water || WaterStylesLoader.Get(water) is not ModWaterStyle style) return orig(Position, Velocity, Type, Scale);
        int drip;
        try { drip = style.GetDropletGore(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{style.FullName}.GetDropletGore", e);
            drip = GoreID.WaterDrip;
        }
        if (drip < GoreID.Count) return orig(Position, Velocity, drip, Scale);
        int index = orig(Position, Velocity, GoreID.WaterDrip, Scale);
        GameArray<Gore> gores = Main.gore;
        if ((uint)index < (uint)gores.Length) gores[index].type = drip;
        return index;
    }

    static void MakeRain(On_Rain.orig_MakeRain orig)
    {
        int water = Main.waterStyle;
        if (WaterStylesLoader.Get(water) is not ModWaterStyle style)
        {
            orig();
            return;
        }
        ModWaterStyle? outer = raining;
        Main.waterStyle = 0;
        raining = style;
        try { orig(); }
        finally
        {
            Main.waterStyle = water;
            raining = outer;
        }
    }

    static byte Variant(ModWaterStyle style)
    {
        try { return style.GetRainVariant(); }
        catch (Exception e)
        {
            ItemHooks.Report($"{style.FullName}.GetRainVariant", e);
            return 0;
        }
    }

    static void SetRainType(int index, ModWaterStyle style)
    {
        GameArray<Rain> rain = Main.rain;
        if ((uint)index < (uint)rain.Length) rain[index].type = Variant(style);
    }

    static int NewRain(On_Rain.orig_NewRain orig, Vector2 Position, Vector2 Velocity)
    {
        int index = orig(Position, Velocity);
        if (raining is ModWaterStyle style && Has(style.GetType(), typeof(ModWaterStyle), nameof(ModWaterStyle.GetRainVariant))) SetRainType(index, style);
        return index;
    }

    static int NewRainForced(On_Rain.orig_NewRainForced orig, Vector2 Position, Vector2 Velocity)
    {
        int index = orig(Position, Velocity);
        if (Current() is ModWaterStyle style) SetRainType(index, style);
        return index;
    }

    static void DrawRain(On_Main.orig_DrawRain orig, Main self)
    {
        IntPtr own = IntPtr.Zero;
        if (Current() is ModWaterStyle style)
        {
            try
            {
                var asset = style.GetRainTexture();
                if (asset != null && !asset.IsDirect)
                {
                    asset.Wait();
                    own = asset.GameAsset;
                }
            }
            catch (Exception e) { ItemHooks.Report($"{style.FullName}.GetRainTexture", e); }
        }
        Asset<Texture2D> vanilla = TextureAssets.Rain;
        if (own == IntPtr.Zero || own == vanilla?.Pointer)
        {
            orig(self);
            return;
        }
        TextureAssets.Rain = Asset<Texture2D>.FromGame(own);
        try { orig(self); }
        finally { TextureAssets.Rain = vanilla; }
    }

    static void UpdateLightDecay(On_LightingEngine.orig_UpdateLightDecay orig, LightingEngine self)
    {
        orig(self);
        if (Decay() is Vector3 v) self._workingLightMap.LightDecayThroughWater = v;
    }

    static void UpdateLegacyLightDecay(On_LegacyLighting.orig_UpdateLightDecay orig, LegacyLighting self)
    {
        orig(self);
        if (Decay() is Vector3 v) self._workingLightMap.LightDecayThroughWater = v;
    }

    static Vector3? Decay()
    {
        if (Current() is not ModWaterStyle style || !Has(style.GetType(), typeof(ModWaterStyle), nameof(ModWaterStyle.LightColorMultiplier))) return null;
        try
        {
            float r = 0.88f, g = 0.96f, b = 1.015f;
            WaterStylesLoader.LightColorMultiplier(style.Slot, LightFactor, ref r, ref g, ref b);
            return new Vector3(r, g, b);
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{style.FullName}.LightColorMultiplier", e);
            return null;
        }
    }

    [UnmanagedCallersOnly]
    static uint BiomeHairDye(IntPtr self, IntPtr playerPointer, uint newColor, byte* lighting, IntPtr method)
    {
        try
        {
            var player = new Player(playerPointer);
            if (Current() is ModWaterStyle style && !player.ZoneShimmer && Has(style.GetType(), typeof(ModWaterStyle), nameof(ModWaterStyle.BiomeHairColor)))
            {
                Color target = style.BiomeHairColor();
                Color from = player.hairDyeColor;
                if (from.A == 0) from = target;
                Color result = new(Step(from.R, target.R), Step(from.G, target.G), Step(from.B, target.B), from.A);
                return *(uint*)&result;
            }
        }
        catch (Exception e) { ItemHooks.Report("ModWaterStyle.BiomeHairColor", e); }
        Hook? hook = hairDye;
        if (hook == null) return newColor;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(playerPointer);
        call.Arg((long)newColor);
        call.Arg((IntPtr)lighting);
        call.Arg(method);
        return (uint)ItemHooks.Original(ref call, hook, "DyeInitializer biome hair dye");
    }

    static int Step(byte from, byte to) => from > to ? from - 1 : from < to ? from + 1 : from;
}
