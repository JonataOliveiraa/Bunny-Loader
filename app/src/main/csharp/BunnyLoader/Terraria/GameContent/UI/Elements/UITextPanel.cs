using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UITextPanel<T> : UIPanel
{
    protected T _text = default!;
    protected float _textScale = 1f;
    protected bool _isLarge;
    protected bool _drawPanel = true;
    public float TextHAlign = 0.5f;
    public bool HideContents;
    readonly UIText label;

    public UITextPanel(T text, float textScale = 1f, bool large = false)
    {
        label = new UIText("", textScale, large) { IgnoresMouseInteraction = true };
        label.Width.Set(0f, 1f);
        label.Height.Set(0f, 1f);
        Append(label);
        SetText(text, textScale, large);
    }

    public bool IsLarge => _isLarge;

    public bool DrawPanel
    {
        get => _drawPanel;
        set => _drawPanel = value;
    }

    public float TextScale
    {
        get => _textScale;
        set => _textScale = value;
    }

    public string Text => _text is LocalizedText localized ? localized.Value ?? "" : _text?.ToString() ?? "";

    public Color TextColor
    {
        get => label.TextColor;
        set => label.TextColor = value;
    }

    public override void Recalculate()
    {
        SetText(_text, _textScale, _isLarge);
        base.Recalculate();
    }

    public void SetText(T text) => SetText(text, _textScale, _isLarge);

    public virtual void SetText(T text, float textScale, bool large)
    {
        _text = text;
        _textScale = textScale;
        _isLarge = large;
        label.TextOriginX = TextHAlign;
        label.TextOriginY = 0.5f;
        label.SetText(HideContents ? "" : Text, textScale, large);
        MinWidth.Set(label.MinWidth.Pixels + PaddingLeft + PaddingRight, 0f);
        MinHeight.Set(label.MinHeight.Pixels + PaddingTop + PaddingBottom, 0f);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (_drawPanel) base.DrawSelf(spriteBatch);
    }
}
