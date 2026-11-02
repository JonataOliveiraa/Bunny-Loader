using System;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.ModLoader.Core;
using Terraria.UI;
using NativeText = BunnyLoader.Interop.UI.Elements.UIText;
using NativeStyle = BunnyLoader.Interop.UI.CalculatedStyle;

namespace Terraria.GameContent.UI.Elements;

public class UIText : UIElement
{
    object text = "";
    float textScale = 1f;
    bool isLarge;
    Color color = new(255, 255, 255, 255);
    Color shadowColor = new(0, 0, 0, 255);
    float textOriginX = 0.5f;
    float textOriginY;
    float wrappedTextBottomPadding = 20f;
    bool isWrapped;
    bool dynamicallyScaleDownToWidth;
    GameHandle? handle;
    NativeText native;
    bool dirty = true;
    string? lastText;

    public UIText(string text, float textScale = 1f, bool large = false) => InternalSetText(text, textScale, large);

    public UIText(LocalizedText text, float textScale = 1f, bool large = false) => InternalSetText(text, textScale, large);

    public string Text => text is LocalizedText localized ? localized.Value ?? "" : text.ToString() ?? "";

    public float TextOriginX
    {
        get => textOriginX;
        set => Set(ref textOriginX, value);
    }

    public float TextOriginY
    {
        get => textOriginY;
        set => Set(ref textOriginY, value);
    }

    public float WrappedTextBottomPadding
    {
        get => wrappedTextBottomPadding;
        set => Set(ref wrappedTextBottomPadding, value);
    }

    public bool IsWrapped
    {
        get => isWrapped;
        set
        {
            Set(ref isWrapped, value);
            InternalSetText(text, textScale, isLarge);
        }
    }

    public bool DynamicallyScaleDownToWidth
    {
        get => dynamicallyScaleDownToWidth;
        set => Set(ref dynamicallyScaleDownToWidth, value);
    }

    public Color TextColor
    {
        get => color;
        set => Set(ref color, value);
    }

    public Color ShadowColor
    {
        get => shadowColor;
        set => Set(ref shadowColor, value);
    }

    public event Action? OnInternalTextChange;

    void Set<T>(ref T field, T value)
    {
        field = value;
        dirty = true;
    }

    public override void Recalculate()
    {
        InternalSetText(text, textScale, isLarge);
        base.Recalculate();
    }

    public void SetText(string text) => InternalSetText(text, textScale, isLarge);

    public void SetText(LocalizedText text) => InternalSetText(text, textScale, isLarge);

    public void SetText(string text, float textScale, bool large) => InternalSetText(text, textScale, large);

    public void SetText(LocalizedText text, float textScale, bool large) => InternalSetText(text, textScale, large);

    static unsafe bool OnGameThread() => Host.Native()->IsGameThread() != 0;

    bool Native()
    {
        if (!native.IsNull) return true;
        if (!OnGameThread()) return false;
        native = NativeText.New("", textScale, isLarge);
        handle = new GameHandle(native.Pointer);
        dirty = true;
        return true;
    }

    void Sync()
    {
        if (!dirty) return;
        native.TextOriginX = textOriginX;
        native.TextOriginY = textOriginY;
        native.WrappedTextBottomPadding = wrappedTextBottomPadding;
        native.IsWrapped = isWrapped;
        native.DynamicallyScaleDownToWidth = dynamicallyScaleDownToWidth;
        native.TextColor = color;
        native.ShadowColor = shadowColor;
        dirty = false;
    }

    static void Copy(ref NativeStyle to, CalculatedStyle from)
    {
        to.X = from.X;
        to.Y = from.Y;
        to.Width = from.Width;
        to.Height = from.Height;
    }

    void Place()
    {
        Copy(ref native._dimensions, GetDimensions());
        Copy(ref native._innerDimensions, GetInnerDimensions());
        Copy(ref native._outerDimensions, GetOuterDimensions());
    }

    void InternalSetText(object value, float scale, bool large)
    {
        text = value;
        textScale = scale;
        isLarge = large;
        try
        {
            if (!Native()) return;
            Sync();
            Place();
            lastText = Text;
            native.SetText(lastText, scale, large);
            MinWidth.Set(native.MinWidth.Pixels - native.PaddingLeft - native.PaddingRight + PaddingLeft + PaddingRight, 0f);
            MinHeight.Set(native.MinHeight.Pixels - native.PaddingTop - native.PaddingBottom + PaddingTop + PaddingBottom, 0f);
        }
        catch (Exception e)
        {
            ItemHooks.Report("UIText.SetText", e);
            return;
        }
        OnInternalTextChange?.Invoke();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (!Native()) return;
        Sync();
        if (Text != lastText) InternalSetText(text, textScale, isLarge);
        Place();
        native.DrawSelf(spriteBatch);
    }
}
