using System;
using Terraria.Graphics.Capture;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class CaptureBiomeHooks
{
    internal static void Install() => ItemHooks.Once("CaptureBiome", () =>
    {
        On_CaptureBiome.GetBiomeByWater += GetBiomeByWater;
        On_CaptureBiome.GetBiomeByLocation += GetBiomeByLocation;
    });

    static CaptureBiome GetBiomeByWater(On_CaptureBiome.orig_GetBiomeByWater orig) =>
        FromScene(orig(), scene => scene.waterStyle.value >= Main.maxLiquidTypes);

    static CaptureBiome GetBiomeByLocation(On_CaptureBiome.orig_GetBiomeByLocation orig) =>
        FromScene(orig(), scene => scene.surfaceBackground.value >= SurfaceBackgroundID.Count);

    static CaptureBiome FromScene(CaptureBiome vanilla, Func<SceneEffectLoader.SceneEffectInstance, bool> modded)
    {
        if (!vanilla.IsNull || Main.gameMenu) return vanilla;
        try
        {
            var scene = SceneEffectLoader.Of(Main.LocalPlayer);
            if (modded(scene)) return CaptureBiome.New(scene.surfaceBackground.value, scene.waterStyle.value, scene.tileColorStyle);
        }
        catch (Exception e) { ItemHooks.Report("ModSceneEffect (capture biome)", e); }
        return vanilla;
    }
}
