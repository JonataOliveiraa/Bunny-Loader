using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UIScrollbar : UIElement
{
    float viewPosition;
    float viewSize = 1f;
    float maxViewSize = 20f;
    bool isDragging;
    bool isHoveringOverHandle;
    float dragYOffset;
    readonly Asset<Texture2D>? texture;
    readonly Asset<Texture2D>? innerTexture;
    public bool AutoHide;

    public UIScrollbar()
    {
        Width.Set(20f, 0f);
        MaxWidth.Set(20f, 0f);
        texture = Vanilla("Terraria/Images/UI/Scrollbar");
        innerTexture = Vanilla("Terraria/Images/UI/ScrollbarInner");
        PaddingTop = 5f;
        PaddingBottom = 5f;
    }

    static Asset<Texture2D>? Vanilla(string name) => ModContent.RequestIfExists(name, out Asset<Texture2D> asset) ? asset : null;

    public float ViewPosition
    {
        get => viewPosition;
        set => viewPosition = Math.Clamp(value, 0f, Math.Max(0f, maxViewSize - viewSize));
    }

    public bool CanScroll => maxViewSize != viewSize;

    public void GoToBottom() => ViewPosition = maxViewSize - viewSize;

    public void SetView(float viewSize, float maxViewSize)
    {
        viewSize = Math.Clamp(viewSize, 0f, maxViewSize);
        viewPosition = Math.Clamp(viewPosition, 0f, Math.Max(0f, maxViewSize - viewSize));
        this.viewSize = viewSize;
        this.maxViewSize = maxViewSize;
    }

    public float GetValue() => viewPosition;

    Rectangle GetHandleRectangle()
    {
        CalculatedStyle inner = GetInnerDimensions();
        if (maxViewSize == 0f && viewSize == 0f)
        {
            viewSize = 1f;
            maxViewSize = 1f;
        }
        return new Rectangle((int)inner.X, (int)(inner.Y + inner.Height * (viewPosition / maxViewSize)) - 3, 20, (int)(inner.Height * (viewSize / maxViewSize)) + 7);
    }

    static Texture2D Pixel() => Asset<Texture2D>.ForGameAsset(TextureAssets.MagicPixel.Pointer, "Images/MagicPixel").Value;

    void DrawBar(SpriteBatch spriteBatch, Asset<Texture2D>? asset, Rectangle r, Color color, bool handle)
    {
        if (asset == null)
        {
            Texture2D pixel = Pixel();
            if (handle)
            {
                spriteBatch.Draw(pixel, new Rectangle(r.X + 4, r.Y, r.Width - 8, r.Height), new Color(220, 225, 245) * (color.A / 255f));
                return;
            }
            spriteBatch.Draw(pixel, new Rectangle(r.X, r.Y - 4, r.Width, r.Height + 8), new Color(0, 0, 0) * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(r.X + 2, r.Y - 2, r.Width - 4, r.Height + 4), new Color(43, 56, 101) * 0.9f);
            return;
        }
        Texture2D t = asset.Value;
        spriteBatch.Draw(t, new Rectangle(r.X, r.Y - 6, r.Width, 6), new Rectangle(0, 0, t.Width, 6), color);
        spriteBatch.Draw(t, new Rectangle(r.X, r.Y, r.Width, r.Height), new Rectangle(0, 6, t.Width, 4), color);
        spriteBatch.Draw(t, new Rectangle(r.X, r.Y + r.Height, r.Width, 6), new Rectangle(0, t.Height - 6, t.Width, 6), color);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (AutoHide && !CanScroll) return;
        CalculatedStyle d = GetDimensions();
        CalculatedStyle inner = GetInnerDimensions();
        Vector2 mouse = UserInterface.ActiveInstance.MousePosition;
        if (isDragging)
            viewPosition = Math.Clamp((mouse.Y - inner.Y - dragYOffset) / inner.Height * maxViewSize, 0f, Math.Max(0f, maxViewSize - viewSize));
        Rectangle handle = GetHandleRectangle();
        isHoveringOverHandle = handle.Contains((int)mouse.X, (int)mouse.Y);
        DrawBar(spriteBatch, texture, new Rectangle((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height), new Color(255, 255, 255, 255), false);
        DrawBar(spriteBatch, innerTexture, handle, new Color(255, 255, 255, 255) * (isDragging || isHoveringOverHandle ? 1f : 0.85f), true);
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);
        if (evt.Target != this) return;
        Rectangle handle = GetHandleRectangle();
        if (handle.Contains((int)evt.MousePosition.X, (int)evt.MousePosition.Y))
        {
            isDragging = true;
            dragYOffset = evt.MousePosition.Y - handle.Y;
            return;
        }
        CalculatedStyle inner = GetInnerDimensions();
        viewPosition = Math.Clamp((evt.MousePosition.Y - inner.Y - (handle.Height >> 1)) / inner.Height * maxViewSize, 0f, Math.Max(0f, maxViewSize - viewSize));
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        isDragging = false;
    }
}
