using System;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CsFont;

public sealed class FontSystem : ModSystem
{
    public static SpriteFont Font;
    public static int Draws;

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (Font.IsNull || Main.gameMenu) return;
        Utils.DrawBorderStringFourWay(spriteBatch, Font, "Bunny Loader font 123", Main.screenWidth / 2f - 200, Main.screenHeight * 0.3f, Color.Gold, Color.Black, Vector2.Zero, 1.2f);
        Draws++;
    }
}

public sealed class CsFontMod : Mod
{
    const int End = 400;
    static int frame, pass, fail;

    static ILog Log => ModContent.GetInstance<CsFontMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csfont {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
        frame++;
        try { Step(); }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == End) Log.Info($"csfont END pass={pass} fail={fail}");
    }

    static void Step()
    {
        switch (frame)
        {
            case 30:
            {
                Report("HasAsset sees the font file", ModContent.HasAsset("CsFont/Assets/Fonts/ExampleFont") && ModContent.GetInstance<CsFontMod>().Assets.HasAsset<SpriteFont>("Assets/Fonts/ExampleFont"), "");
                Asset<SpriteFont> asset = ModContent.Request<SpriteFont>("CsFont/Assets/Fonts/ExampleFont");
                SpriteFont font = asset.Value;
                Report("ModContent.Request<SpriteFont> loads the mod font", !font.IsNull && asset.IsLoaded, asset.Name);
                Asset<SpriteFont> again = ModContent.GetInstance<CsFontMod>().Assets.Request<SpriteFont>("Assets/Fonts/ExampleFont.json");
                Report("the same file gives the same asset", ReferenceEquals(asset, again) && again.Value == font, "");
                SpriteFont mouse = FontAssets.MouseText.Value;
                Vector2 own = font.MeasureString("Bunny Loader"), game = mouse.MeasureString("Bunny Loader");
                Report("the mod font measures text with its own glyphs", own.X > 10 && own.X != game.X, $"{own.X} vs {game.X}");
                Report("the game font is untouched", font != mouse, "");
                SpriteFont vanilla = ModContent.Request<SpriteFont>("Terraria/Fonts/Mouse_Text").Value;
                Report("Request<SpriteFont> finds the game fonts by name", vanilla == mouse, "");
                bool texture = true;
                try { ModContent.Request<SpriteFont>("CsFont/Assets/Fonts/Missing"); texture = false; }
                catch (System.IO.FileNotFoundException) { }
                Report("a missing font file throws", texture, "");
                FontSystem.Font = font;
                break;
            }
            case 90:
                Report("the mod font draws", FontSystem.Draws > 10, $"draws={FontSystem.Draws}");
                Log.Info("csfont SHOT font");
                break;
        }
    }
}
