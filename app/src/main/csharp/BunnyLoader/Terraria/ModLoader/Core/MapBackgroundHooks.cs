using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace Terraria.ModLoader.Core;

internal static class MapBackgroundHooks
{
    static readonly Dictionary<string, Asset<Texture2D>?> Textures = new(StringComparer.Ordinal);

    internal static void Install() => ItemHooks.Once("MapBackground", () =>
    {
        On_GUIMap.DrawMapFullscreenBackground += DrawGuiMap;
        On_Main.DrawMapFullscreenBackground += DrawMain;
    });

    static void DrawGuiMap(On_GUIMap.orig_DrawMapFullscreenBackground orig, GUIMap self, Vector2 screenPosition, int screenWidth, int screenHeight)
    {
        if (!Draw(screenPosition, screenWidth, screenHeight)) orig(self, screenPosition, screenWidth, screenHeight);
    }

    static void DrawMain(On_Main.orig_DrawMapFullscreenBackground orig, Vector2 screenPosition, int screenWidth, int screenHeight)
    {
        if (!Draw(screenPosition, screenWidth, screenHeight)) orig(screenPosition, screenWidth, screenHeight);
    }

    static Asset<Texture2D>? TextureOf(ModSceneEffect effect, string path)
    {
        lock (Textures)
        {
            if (Textures.TryGetValue(path, out Asset<Texture2D>? known)) return known;
        }
        Asset<Texture2D>? asset = null;
        try
        {
            asset = ModContent.Request<Texture2D>(path);
            asset.Wait();
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{effect.FullName}.MapBackground ({path})", e);
            asset = null;
        }
        lock (Textures) Textures[path] = asset;
        return asset;
    }

    static bool Draw(Vector2 screenPosition, int screenWidth, int screenHeight)
    {
        try
        {
            var scene = SceneEffectLoader.Of(Main.LocalPlayer);
            if (scene.mapBackground is not { Length: > 0 } path || scene.mapBackgroundSceneEffect is not ModSceneEffect effect) return false;
            if (TextureOf(effect, path) is not Asset<Texture2D> asset) return false;
            Color color = !effect.MapBackgroundFullbright && screenPosition.Y <= Main.worldSurface * 16.0 ? Main.ColorOfTheSkies : Color.White;
            effect.MapBackgroundColor(ref color);
            Main.spriteBatch.Draw(asset.Value, new Rectangle(0, 0, screenWidth, screenHeight), color);
            return true;
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModSceneEffect (map background)", e);
            return false;
        }
    }
}
