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

    static ModResourceOverlay[] preDraw = Array.Empty<ModResourceOverlay>(), postDraw = Array.Empty<ModResourceOverlay>();
    static ModResourceOverlay[] preDisplay = Array.Empty<ModResourceOverlay>(), postDisplay = Array.Empty<ModResourceOverlay>();

    public static int OverlayCount => overlays.Count;

    internal static int Add(ModResourceOverlay overlay)
    {
        lock (overlays)
        {
            overlays.Add(overlay);
            Type type = overlay.GetType();
            if (Overrides(type, nameof(ModResourceOverlay.PreDrawResource))) preDraw = With(preDraw, overlay);
            if (Overrides(type, nameof(ModResourceOverlay.PostDrawResource))) postDraw = With(postDraw, overlay);
            if (Overrides(type, nameof(ModResourceOverlay.PreDrawResourceDisplay))) preDisplay = With(preDisplay, overlay);
            if (Overrides(type, nameof(ModResourceOverlay.PostDrawResourceDisplay))) postDisplay = With(postDisplay, overlay);
            ResourceOverlayHooks.Install(type);
            return overlays.Count - 1;
        }
    }

    static bool Overrides(Type type, string name) => ItemHooks.Overrides(type, typeof(ModResourceOverlay), name);

    static ModResourceOverlay[] With(ModResourceOverlay[] list, ModResourceOverlay overlay)
    {
        var grown = new ModResourceOverlay[list.Length + 1];
        Array.Copy(list, grown, list.Length);
        grown[^1] = overlay;
        return grown;
    }

    public static ModResourceOverlay GetOverlay(int type) => type >= 0 && type < overlays.Count ? overlays[type] : null!;

    public static bool PreDrawResource(ResourceOverlayDrawContext context)
    {
        bool result = true;
        foreach (ModResourceOverlay overlay in preDraw)
        {
            try { result &= overlay.PreDrawResource(context); }
            catch (Exception e) { ItemHooks.Report($"{overlay.FullName}.PreDrawResource", e); }
        }
        return result;
    }

    public static void PostDrawResource(ResourceOverlayDrawContext context)
    {
        foreach (ModResourceOverlay overlay in postDraw)
        {
            try { overlay.PostDrawResource(context); }
            catch (Exception e) { ItemHooks.Report($"{overlay.FullName}.PostDrawResource", e); }
        }
    }

    public static bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText)
    {
        bool result = true;
        drawText = true;
        foreach (ModResourceOverlay overlay in preDisplay)
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
        foreach (ModResourceOverlay overlay in postDisplay)
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
