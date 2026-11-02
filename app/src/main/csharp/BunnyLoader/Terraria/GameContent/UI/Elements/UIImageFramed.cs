using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UIImageFramed : UIElement
{
    Asset<Texture2D> texture;
    Rectangle frame;

    public UIImageFramed(Asset<Texture2D> texture, Rectangle frame)
    {
        this.texture = texture;
        SetFrame(frame);
        Color = new Color(255, 255, 255, 255);
    }

    public Color Color { get; set; }

    public void SetImage(Asset<Texture2D> texture, Rectangle frame)
    {
        this.texture = texture;
        SetFrame(frame);
    }

    public void SetFrame(Rectangle frame)
    {
        this.frame = frame;
        Width.Set(frame.Width, 0f);
        Height.Set(frame.Height, 0f);
    }

    public void SetFrame(int frameCountHorizontal, int frameCountVertical, int frameX, int frameY, int sizeOffsetX, int sizeOffsetY)
    {
        Rectangle r = texture.Frame(frameCountHorizontal, frameCountVertical, frameX, frameY);
        SetFrame(new Rectangle(r.X, r.Y, r.Width + sizeOffsetX, r.Height + sizeOffsetY));
    }

    protected override void DrawSelf(SpriteBatch spriteBatch) => spriteBatch.Draw(texture.Value, GetDimensions().Position(), frame, Color);
}
