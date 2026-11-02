using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UIImage : UIElement
{
    Asset<Texture2D>? texture;
    Texture2D nonReloadingTexture;
    bool measured;
    public float ImageScale = 1f;
    public float Rotation;
    public bool ScaleToFit;
    public bool AllowResizingDimensions = true;
    public Color Color = new(255, 255, 255, 255);
    public Vector2 NormalizedOrigin = Vector2.Zero;
    public Rectangle? Frame;
    public bool RemoveFloatingPointsFromDrawPosition;
    public bool UseTextureSizeForOrigin = true;

    protected UIImage() { }

    public UIImage(Asset<Texture2D> texture) => SetImage(texture);

    public UIImage(Texture2D nonReloadingTexture) => SetImage(nonReloadingTexture);

    public Asset<Texture2D>? Texture => texture;

    public void SetImage(Asset<Texture2D> value)
    {
        texture = value;
        nonReloadingTexture = default;
        measured = false;
        Measure();
    }

    public void SetImage(Texture2D value)
    {
        texture = null;
        nonReloadingTexture = value;
        measured = false;
        Measure();
    }

    bool Measure()
    {
        if (measured || !AllowResizingDimensions) return false;
        try
        {
            Texture2D tex = CurrentTexture();
            if (tex.IsNull) return false;
            Width.Set(tex.Width, 0f);
            Height.Set(tex.Height, 0f);
            measured = true;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    Texture2D CurrentTexture() => !nonReloadingTexture.IsNull ? nonReloadingTexture : texture != null ? texture.Value : default;

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (Measure()) Recalculate();
        Texture2D tex = CurrentTexture();
        if (tex.IsNull) return;
        CalculatedStyle d = GetDimensions();
        if (ScaleToFit)
        {
            spriteBatch.Draw(tex, d.ToRectangle(), Frame, Color);
            return;
        }
        Vector2 size = new(tex.Width, tex.Height);
        Vector2 box = UseTextureSizeForOrigin ? size : new Vector2(d.Width, d.Height);
        Vector2 position = d.Position() + box * (1f - ImageScale) / 2f + box * NormalizedOrigin;
        if (RemoveFloatingPointsFromDrawPosition) position = new Vector2((int)position.X, (int)position.Y);
        spriteBatch.Draw(tex, position, Frame, Color, Rotation, size * NormalizedOrigin, ImageScale, SpriteEffects.None, 0f);
    }
}
