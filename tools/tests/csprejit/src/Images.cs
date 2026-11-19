using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace CsPreJit;

public sealed class Images : ModSystem
{
    public static readonly (string Name, int Width, int Height)[] All =
    {
        ("Rgba8", 37, 23), ("Rgb8Key", 20, 15), ("Palette", 16, 16), ("Pal2", 13, 7), ("Gray4", 19, 11), ("Gray1", 9, 9),
        ("GrayAlpha16", 10, 6), ("Rgba16", 8, 8), ("Interlaced", 33, 17), ("InterlacedPal", 11, 5), ("Asym", 32, 32),
    };

    public static Asset<Texture2D> Async = null!;
    public static Asset<Texture2D> Lazy = null!;
    public static Asset<Texture2D> Touched = null!;

    public override void Load()
    {
        Async = Mod.Assets.Request<Texture2D>("Images/Rgba8");
        Lazy = Mod.Assets.Request<Texture2D>("Images/Gray4", AssetRequestMode.DoNotLoad);
        Touched = Mod.Assets.Request<Texture2D>("Images/Interlaced");
    }

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (!Checks.Drawing) return;
        float x = 120, y = 120;
        foreach ((string name, int width, int height) in All)
        {
            Texture2D texture = Mod.Assets.Request<Texture2D>("Images/" + name).Value;
            float scale = name == "Asym" ? 6f : 4f;
            spriteBatch.Draw(texture, new Vector2(x, y), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            x += width * scale + 24;
            if (x > 900)
            {
                x = 120;
                y += 200;
            }
        }
    }
}
