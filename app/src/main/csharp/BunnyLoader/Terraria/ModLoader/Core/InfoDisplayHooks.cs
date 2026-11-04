using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader.Core;

internal static class InfoDisplayHooks
{
    static readonly List<InfoDisplay> Displays = new();
    internal static void Register(InfoDisplay display)
    {
        Displays.Add(display);
        ItemHooks.Once("ModInfoDisplays", () => On_GUIAccessoryInfo.Draw += Draw);
    }

    static void Draw(On_GUIAccessoryInfo.orig_Draw orig, GUIAccessoryInfo self)
    {
        orig(self);
        if (Main.gameMenu || Main.LocalPlayer.IsNull) return;
        Rectangle region = self.lastDrawRegion;
        Vector2 position = region.Width > 0 ? new Vector2(region.X, region.Y + region.Height + 4) : new Vector2(Math.Max(20, Main.screenWidth - 240), 100);
        foreach (InfoDisplay display in Displays)
        {
            try
            {
                if (!display.Active()) continue;
                Color color = Color.White, shadow = Color.Black;
                string text = display.DisplayValue(ref color, ref shadow);
                var texture = ModContent.Request<Texture2D>(display.Texture).Value;
                Main.spriteBatch.Draw(texture, position, Color.White);
                Utils.DrawBorderString(Main.spriteBatch, text, position + new Vector2(texture.Width + 4, 0), color, 0.8f);
                position.Y += Math.Max(24, texture.Height + 4);
            }
            catch (Exception e) { ItemHooks.Report($"{display.FullName}.InfoDisplay", e); }
        }
    }
}
