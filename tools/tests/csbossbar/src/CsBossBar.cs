using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsBossBar;

public sealed class CsBossBarMod : Mod
{
    public override void Load()
    {
        On_Player.Update += Update;
        On_GUIPanel.DrawBacking += DrawBacking;
    }

    static void DrawBacking(On_GUIPanel.orig_DrawBacking orig, Vector2 position, Vector2 size, Texture2D texture, Color color, GameNullable<Rectangle> sourceFrame)
    {
        if (Checks.InBar) Checks.Backings.Add(color);
        orig(position, size, texture, color, sourceFrame);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            Checks.Reset();
            return;
        }
        if (i != Main.myPlayer) return;
        Checks.Frame(self);
    }
}
