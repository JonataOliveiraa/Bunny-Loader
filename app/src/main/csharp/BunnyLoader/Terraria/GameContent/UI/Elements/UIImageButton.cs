using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UIImageButton : UIElement
{
    Asset<Texture2D> texture;
    protected float _visibilityActive = 1f;
    protected float _visibilityInactive = 0.4f;
    Asset<Texture2D>? borderTexture;
    readonly Rectangle? frame;
    Rectangle? borderFrame;
    bool measured;
    public Color Color = new(255, 255, 255, 255);
    public Color BorderColor = new(255, 255, 255, 255);

    public UIImageButton(Asset<Texture2D> texture, Rectangle? frame = null)
    {
        this.texture = texture;
        this.frame = frame;
        Measure();
    }

    bool Measure()
    {
        if (measured) return false;
        if (frame is Rectangle f)
        {
            Width.Set(f.Width, 0f);
            Height.Set(f.Height, 0f);
            measured = true;
            return true;
        }
        try
        {
            Width.Set(texture.Width(), 0f);
            Height.Set(texture.Height(), 0f);
            measured = true;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void SetHoverImage(Asset<Texture2D> texture, Rectangle? frame = null)
    {
        borderTexture = texture;
        borderFrame = frame;
    }

    public void SetImage(Asset<Texture2D> texture, Rectangle? frame = null)
    {
        this.texture = texture;
        measured = false;
        Measure();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (Measure()) Recalculate();
        CalculatedStyle d = GetDimensions();
        spriteBatch.Draw(texture.Value, d.Position(), frame, Color * (IsMouseHovering ? _visibilityActive : _visibilityInactive));
        if (borderTexture != null && IsMouseHovering) spriteBatch.Draw(borderTexture.Value, d.Position(), borderFrame, BorderColor);
    }

    public override void MouseOver(UIMouseEvent evt)
    {
        base.MouseOver(evt);
        SoundEngine.PlaySound(12);
    }

    public void SetVisibility(float whenActive, float whenInactive)
    {
        _visibilityActive = Math.Clamp(whenActive, 0f, 1f);
        _visibilityInactive = Math.Clamp(whenInactive, 0f, 1f);
    }
}
