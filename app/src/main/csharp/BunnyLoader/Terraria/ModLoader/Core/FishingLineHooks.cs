using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader.Core;

internal static class FishingLineHooks
{
    [ThreadStatic] static Color? lineColor;

    internal static void Install(Func<string, bool> has)
    {
        if (!has("ModifyFishingLine")) return;
        ItemHooks.Once("FishingLine", () =>
        {
            On_Main.DrawProj_FishingLine += DrawFishingLine;
            On_Main.TryApplyingPlayerStringColor += StringColor;
        });
    }

    static bool ItemWants(Item item) => ItemHooks.ModItemOf(item) is ModItem m && ItemHooks.Overrides(m.GetType(), typeof(ModItem), nameof(ModItem.ModifyFishingLine));

    static void DrawFishingLine(On_Main.orig_DrawProj_FishingLine orig, Projectile proj, Player theOwner, ref float polePosX, ref float polePosY, Vector2 mountedCenter)
    {
        Item held = theOwner.inventory[theOwner.selectedItem];
        bool projectile = ProjectileHooks.Wants(proj, nameof(ModProjectile.ModifyFishingLine));
        if (!projectile && !ItemWants(held))
        {
            orig(proj, theOwner, ref polePosX, ref polePosY, mountedCenter);
            return;
        }
        float x = 0f, y = 0f;
        Color color = VanillaLineColor(held.type);
        if (projectile) ProjectileLoader.ModifyFishingLine(proj, ref x, ref y, ref color);
        ItemLoader.ModifyFishingLine(proj, theOwner, ref x, ref y, ref color);
        Color? outer = lineColor;
        lineColor = color;
        try { orig(proj, theOwner, ref polePosX, ref polePosY, new Vector2(mountedCenter.X + x, mountedCenter.Y + y)); }
        finally { lineColor = outer; }
    }

    static Color StringColor(On_Main.orig_TryApplyingPlayerStringColor orig, int playerStringColor, Color stringColor) =>
        orig(playerStringColor, lineColor ?? stringColor);

    static Color VanillaLineColor(int type) => type switch
    {
        2294 => new Color(100, 180, 230, 100),
        2295 => new Color(250, 90, 70, 100),
        2293 => new Color(203, 190, 210, 100),
        2421 => new Color(183, 77, 112, 100),
        2422 => new Color(255, 226, 116, 100),
        4325 => new Color(200, 100, 100, 100),
        4442 => new Color(100, 100, 200, 100),
        _ => new Color(200, 200, 200, 100),
    };
}
