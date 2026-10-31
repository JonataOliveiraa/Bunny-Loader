using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class ResourceOverlayLoader
{
    internal static readonly List<ModResourceOverlay> overlays = new();

    public static int OverlayCount => overlays.Count;

    internal static int Add(ModResourceOverlay overlay)
    {
        lock (overlays)
        {
            overlays.Add(overlay);
            ResourceOverlayHooks.Install(overlay.GetType());
            return overlays.Count - 1;
        }
    }

    public static ModResourceOverlay GetOverlay(int type) => type >= 0 && type < overlays.Count ? overlays[type] : null!;

    public static bool PreDrawResource(ResourceOverlayDrawContext context)
    {
        bool result = true;
        foreach (ModResourceOverlay overlay in overlays)
        {
            try { result &= overlay.PreDrawResource(context); }
            catch (Exception e) { ItemHooks.Report($"{overlay.FullName}.PreDrawResource", e); }
        }
        return result;
    }

    public static void PostDrawResource(ResourceOverlayDrawContext context)
    {
        foreach (ModResourceOverlay overlay in overlays)
        {
            try { overlay.PostDrawResource(context); }
            catch (Exception e) { ItemHooks.Report($"{overlay.FullName}.PostDrawResource", e); }
        }
    }

    public static bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText)
    {
        bool result = true;
        drawText = true;
        foreach (ModResourceOverlay overlay in overlays)
        {
            try
            {
                result &= overlay.PreDrawResourceDisplay(snapshot, displaySet, drawingLife, ref textColor, out bool draw);
                drawText &= draw;
            }
            catch (Exception e) { ItemHooks.Report($"{overlay.FullName}.PreDrawResourceDisplay", e); }
        }
        return result;
    }

    public static void PostDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, Color textColor, bool drawText)
    {
        foreach (ModResourceOverlay overlay in overlays)
        {
            try { overlay.PostDrawResourceDisplay(snapshot, displaySet, drawingLife, textColor, drawText); }
            catch (Exception e) { ItemHooks.Report($"{overlay.FullName}.PostDrawResourceDisplay", e); }
        }
    }

    public static bool DisplayHoverText(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife)
    {
        bool result = true;
        foreach (ModResourceOverlay overlay in overlays) result &= overlay.DisplayHoverText(snapshot, displaySet, drawingLife);
        return result;
    }
}
