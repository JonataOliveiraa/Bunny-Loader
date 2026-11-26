using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsTexData;

public sealed class DrawSystem : ModSystem
{
    public const int Scale = 40;

    static readonly Point[] Places = { new(100, 100), new(300, 100), new(500, 100) };

    static bool logged;

    static ILog Log => ModContent.GetInstance<CsTexDataMod>().Logger;

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (!CsTexDataMod.Ran) return;

        Texture2D[] textures = { Checks.Created, Checks.FromStream, Checks.ModImage };
        string[] names = { "created", "from_stream", "mod_image" };
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i].IsNull) continue;
            var area = new Rectangle(Places[i].X, Places[i].Y, Pattern.Width * Scale, Pattern.Height * Scale);
            spriteBatch.Draw(textures[i], area, Color.White);
            if (!logged) Log.Info($"cstexdata draw {names[i]} {area.X},{area.Y},{area.Width},{area.Height} screen {Main.screenWidth}x{Main.screenHeight}");
        }
        logged = true;
    }
}
