using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.DataStructures;

public struct BuffDrawParams
{
    public Texture2D Texture;

    public Vector2 Position;

    public Vector2 TextPosition;

    public Rectangle SourceRectangle;

    public Rectangle MouseRectangle;

    public Color DrawColor;

    public BuffDrawParams(Texture2D texture, Vector2 position, Vector2 textPosition, Rectangle sourceRectangle, Rectangle mouseRectangle, Color drawColor)
    {
        Texture = texture;
        Position = position;
        TextPosition = textPosition;
        SourceRectangle = sourceRectangle;
        MouseRectangle = mouseRectangle;
        DrawColor = drawColor;
    }

    public void Deconstruct(out Texture2D texture, out Vector2 position, out Vector2 textPosition, out Rectangle sourceRectangle, out Rectangle mouseRectangle,
                            out Color drawColor)
    {
        texture = Texture;
        position = Position;
        textPosition = TextPosition;
        sourceRectangle = SourceRectangle;
        mouseRectangle = MouseRectangle;
        drawColor = DrawColor;
    }
}
