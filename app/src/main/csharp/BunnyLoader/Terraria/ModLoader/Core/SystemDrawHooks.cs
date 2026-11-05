using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics;
using Terraria.Map;

namespace Terraria.ModLoader.Core;

internal static class SystemDrawHooks
{
    static bool preMapIcons, postFullscreenMap;
    static readonly HashSet<nint> HiddenLayers = new();
    static readonly List<IMapLayer> Layers = new();

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModSystem.ModifyScreenPosition))) PlayerDrawHooks.InstallCamera(false);
        if (has(nameof(ModSystem.ModifySunLightColor)))
            ItemHooks.Once("SystemSunLight", () => On_Main.ApplyColorOfTheSkiesToTiles += ApplyColorOfTheSkiesToTiles);
        if (has(nameof(ModSystem.ModifyLightingBrightness)))
            ItemHooks.Once("SystemBrightness", () => On_Lighting.UpdateGlobalBrightness += UpdateGlobalBrightness);
        if (has(nameof(ModSystem.PostDrawTiles)))
            ItemHooks.Once("SystemPostDrawTiles", () => On_Main.DrawSuperSpecialProjectiles += DrawSuperSpecialProjectiles);
        if (has(nameof(ModSystem.PostDrawInterface)))
            ItemHooks.Once("SystemPostDrawInterface", () => On_Main.DrawInterface_33_MouseText += DrawMouseText);
        if (has(nameof(ModSystem.ModifyInterfaceLayers))) InterfaceLayerHooks.Install();
        if (has(nameof(ModSystem.PreDrawMapIconOverlay))) preMapIcons = true;
        if (has(nameof(ModSystem.PostDrawFullscreenMap))) postFullscreenMap = true;
        if (preMapIcons || postFullscreenMap) WantMapIcons();
    }

    internal static void WantMapIcons() => ItemHooks.Once("SystemMapIcons", () => On_MapIconOverlay.Draw += DrawMapIcons);

    internal static void ModifyTransformMatrix()
    {
        if (Main.gameMenu) return;
        SpriteViewMatrix transform = Main.GameViewMatrix;
        SystemLoader.ModifyTransformMatrix(ref transform);
        if (!transform.IsNull) Main.GameViewMatrix = transform;
    }

    internal static bool IsHidden(nint layer) => HiddenLayers.Contains(layer);

    internal static void SetHidden(nint layer, bool hidden)
    {
        if (hidden) HiddenLayers.Add(layer);
        else HiddenLayers.Remove(layer);
    }

    static void ApplyColorOfTheSkiesToTiles(On_Main.orig_ApplyColorOfTheSkiesToTiles orig)
    {
        orig();
        if (Main.gameMenu) return;
        Color tile = Main.tileColor, background = Main.ColorOfTheSkies;
        SystemLoader.ModifySunLightColor(ref tile, ref background);
        Main.tileColor = tile;
        Main.ColorOfTheSkies = background;
    }

    static void UpdateGlobalBrightness(On_Lighting.orig_UpdateGlobalBrightness orig)
    {
        orig();
        if (Main.gameMenu) return;
        float scale = 1f;
        SystemLoader.ModifyLightingBrightness(ref scale);
        if (float.IsFinite(scale) && scale >= 0f && scale != 1f) Lighting.GlobalBrightness *= scale;
    }

    static void DrawSuperSpecialProjectiles(On_Main.orig_DrawSuperSpecialProjectiles orig, Main self, GameObject projCache, bool startSpriteBatch)
    {
        if (!Main.gameMenu) SystemLoader.PostDrawTiles();
        orig(self, projCache, startSpriteBatch);
    }

    static void DrawMouseText(On_Main.orig_DrawInterface_33_MouseText orig, Main self)
    {
        if (!Main.gameMenu) SystemLoader.PostDrawInterface(Main.spriteBatch);
        orig(self);
    }

    static void DrawMapIcons(On_MapIconOverlay.orig_Draw orig, MapIconOverlay self, Vector2 mapPosition, Vector2 mapOffset,
                             GameNullable<Rectangle> clippingRect, float mapScale, float drawScale, int alpha, ref GameString text)
    {
        if (Main.gameMenu)
        {
            orig(self, mapPosition, mapOffset, clippingRect, mapScale, drawScale, alpha, ref text);
            return;
        }
        var list = new GameList(self._layers);
        int firstHidden = -1;
        if (preMapIcons && !list.IsNull)
        {
            HiddenLayers.Clear();
            Layers.Clear();
            for (int i = 0; i < list.Count; i++) Layers.Add(new IMapLayer(list[i]));
            var context = MapOverlayDrawContext.New(mapPosition, mapOffset, clippingRect, mapScale, drawScale, alpha / 255f);
            SystemLoader.PreDrawMapIconOverlay(Layers, context);
            if (HiddenLayers.Count > 0)
                for (int i = 0; i < Layers.Count && firstHidden < 0; i++)
                    if (HiddenLayers.Contains(Layers[i].Pointer)) firstHidden = i;
        }
        if (MapLayerLoader.Any && !list.IsNull)
        {
            var context = MapOverlayDrawContext.New(mapPosition, mapOffset, clippingRect, mapScale, drawScale, alpha / 255f);
            MapLayerLoader.Draw(list, ref context, ref text, static layer => HiddenLayers.Contains(layer.Pointer));
        }
        else if (firstHidden < 0)
        {
            orig(self, mapPosition, mapOffset, clippingRect, mapScale, drawScale, alpha, ref text);
        }
        else
        {
            for (int i = list.Count - 1; i >= firstHidden; i--) list.RemoveAt(i);
            for (int i = firstHidden; i < Layers.Count; i++)
            {
                if (HiddenLayers.Contains(Layers[i].Pointer)) continue;
                list.Add(Layers[i].Pointer);
            }
            try { orig(self, mapPosition, mapOffset, clippingRect, mapScale, drawScale, alpha, ref text); }
            finally
            {
                for (int i = list.Count - 1; i >= firstHidden; i--) list.RemoveAt(i);
                for (int i = firstHidden; i < Layers.Count; i++) list.Add(Layers[i].Pointer);
            }
        }
        if (!postFullscreenMap || !Main.mapFullscreen) return;
        string mouseText = text.ToString() ?? "";
        string before = mouseText;
        SystemLoader.PostDrawFullscreenMap(ref mouseText);
        if (mouseText != before) text = mouseText;
    }
}
