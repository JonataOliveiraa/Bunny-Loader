using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBackground;

public sealed class TestWaterfall : ModWaterfallStyle
{
    public static int Tints;

    public override string Texture => "Water/TestWaterfall";

    public override void ColorMultiplier(ref float r, ref float g, ref float b, float a)
    {
        Tints++;
        g = 0;
        b = 0;
    }
}

public sealed class TestWater : ModWaterStyle
{
    public const int Splash = DustID.GoldFlame;

    Asset<Texture2D> rain = null!;

    public override string Texture => "Water/TestWater";

    public override void Load() => rain = Mod.Assets.Request<Texture2D>("Water/TestRain");

    public override int ChooseWaterfallStyle() => ModContent.GetInstance<TestWaterfall>().Slot;

    public override int GetSplashDust() => Splash;

    public override int GetDropletGore() => GoreID.WaterDrip;

    public override void LightColorMultiplier(ref float r, ref float g, ref float b)
    {
        r = 1;
        g = 1;
        b = 1;
    }

    public override byte GetRainVariant() => 1;

    public override Asset<Texture2D> GetRainTexture() => rain;

    public override Color BiomeHairColor() => new(255, 0, 0, 255);
}

public sealed class TestSurface : ModSurfaceBackgroundStyle
{
    public static int Far, Middle, Close, Fades;

    public override void ModifyFarFades(float[] fades, float transitionSpeed)
    {
        Fades++;
        for (int i = 0; i < fades.Length; i++)
        {
            if (i == Slot) fades[i] = System.Math.Min(1f, fades[i] + transitionSpeed);
            else fades[i] = System.Math.Max(0f, fades[i] - transitionSpeed);
        }
    }

    public override int ChooseFarTexture()
    {
        Far++;
        return BackgroundTextureLoader.GetBackgroundSlot(Mod, "Backgrounds/ExampleBiomeSurfaceFar");
    }

    public override int ChooseMiddleTexture()
    {
        Middle++;
        return BackgroundTextureLoader.GetBackgroundSlot(Mod, "Backgrounds/ExampleBiomeSurfaceMid0");
    }

    public override int ChooseCloseTexture(ref float scale, ref double parallax, ref float a, ref float b)
    {
        Close++;
        return BackgroundTextureLoader.GetBackgroundSlot(Mod, "Backgrounds/ExampleBiomeSurfaceClose");
    }
}

public sealed class TestUnderground : ModUndergroundBackgroundStyle
{
    public static int Fills;

    public override void FillTextureArray(int[] textureSlots)
    {
        Fills++;
        for (int i = 0; i < 4; i++) textureSlots[i] = BackgroundTextureLoader.GetBackgroundSlot(Mod, "Backgrounds/ExampleBiomeUnderground" + i);
    }
}

public sealed class TestScene : ModSceneEffect
{
    public static bool On;
    public static SceneEffectPriority Level = SceneEffectPriority.None;

    public override SceneEffectPriority Priority => Level;

    public override ModWaterStyle WaterStyle => ModContent.GetInstance<TestWater>();

    public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle => ModContent.GetInstance<TestSurface>();

    public override ModUndergroundBackgroundStyle UndergroundBackgroundStyle => ModContent.GetInstance<TestUnderground>();

    public override bool IsSceneEffectActive(Player player) => On;
}

public sealed class MapScene : ModSceneEffect
{
    public static bool On, Fullbright, Tint;
    public static readonly Color TintColor = new(10, 20, 30, 255);

    public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

    public override string? MapBackground => "CsBackground/Map/TestMapBg";

    public override bool MapBackgroundFullbright => Fullbright;

    public override void MapBackgroundColor(ref Color color)
    {
        if (Tint) color = TintColor;
    }

    public override bool IsSceneEffectActive(Player player) => On;
}
