using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UIPanel : UIElement
{
    readonly int cornerSize = 12;
    readonly int barSize = 4;
    readonly Asset<Texture2D>? borderTexture;
    readonly Asset<Texture2D>? backgroundTexture;
    public Color BorderColor = new(0, 0, 0, 255);
    public Color BackgroundColor = new Color(63, 82, 151) * 0.7f;

    public UIPanel()
    {
        borderTexture = ModContent.Request<Texture2D>("Terraria/Images/UI/PanelBorder");
        backgroundTexture = ModContent.Request<Texture2D>("Terraria/Images/UI/PanelBackground");
        SetPadding(cornerSize);
    }

    public UIPanel(Asset<Texture2D> customBackground, Asset<Texture2D> customBorder, int customCornerSize = 12, int customBarSize = 4)
    {
        borderTexture = customBorder;
        backgroundTexture = customBackground;
        cornerSize = customCornerSize;
        barSize = customBarSize;
        SetPadding(cornerSize);
    }

    void DrawPanel(SpriteBatch spriteBatch, Texture2D texture, Color color)
    {
        CalculatedStyle d = GetDimensions();
        var p1 = new Point((int)d.X, (int)d.Y);
        var p2 = new Point(p1.X + (int)d.Width - cornerSize, p1.Y + (int)d.Height - cornerSize);
        int width = p2.X - p1.X - cornerSize;
        int height = p2.Y - p1.Y - cornerSize;
        int c = cornerSize, b = barSize;
        spriteBatch.Draw(texture, new Rectangle(p1.X, p1.Y, c, c), new Rectangle(0, 0, c, c), color);
        spriteBatch.Draw(texture, new Rectangle(p2.X, p1.Y, c, c), new Rectangle(c + b, 0, c, c), color);
        spriteBatch.Draw(texture, new Rectangle(p1.X, p2.Y, c, c), new Rectangle(0, c + b, c, c), color);
        spriteBatch.Draw(texture, new Rectangle(p2.X, p2.Y, c, c), new Rectangle(c + b, c + b, c, c), color);
        spriteBatch.Draw(texture, new Rectangle(p1.X + c, p1.Y, width, c), new Rectangle(c, 0, b, c), color);
        spriteBatch.Draw(texture, new Rectangle(p1.X + c, p2.Y, width, c), new Rectangle(c, c + b, b, c), color);
        spriteBatch.Draw(texture, new Rectangle(p1.X, p1.Y + c, c, height), new Rectangle(0, c, c, b), color);
        spriteBatch.Draw(texture, new Rectangle(p2.X, p1.Y + c, c, height), new Rectangle(c + b, c, c, b), color);
        spriteBatch.Draw(texture, new Rectangle(p1.X + c, p1.Y + c, width, height), new Rectangle(c, c, b, b), color);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (backgroundTexture != null) DrawPanel(spriteBatch, backgroundTexture.Value, BackgroundColor);
        if (borderTexture != null) DrawPanel(spriteBatch, borderTexture.Value, BorderColor);
    }
}
