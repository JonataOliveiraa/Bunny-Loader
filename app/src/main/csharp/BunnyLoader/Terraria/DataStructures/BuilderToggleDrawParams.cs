using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.DataStructures;

public struct BuilderToggleDrawParams
{
    public Texture2D Texture;
    public Vector2 Position;
    public Rectangle Frame;
    public Color Color = Color.White;
    public float Scale = 1f;
    public SpriteEffects SpriteEffects;
    public BuilderToggleDrawParams() { }
}
