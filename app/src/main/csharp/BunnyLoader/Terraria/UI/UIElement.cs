using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Core;

namespace Terraria.UI;

public class UIElement : IComparable
{
    public delegate void MouseEvent(UIMouseEvent evt, UIElement listeningElement);

    public delegate void ScrollWheelEvent(UIScrollWheelEvent evt, UIElement listeningElement);

    public delegate void ElementEvent(UIElement affectedElement);

    public delegate void DrawEvent(UIElement affectedElement, SpriteBatch sb);

    public delegate void UIElementAction(UIElement element);

    static readonly List<Rectangle> Clips = new();
    static int idCounter;

    protected readonly List<UIElement> Elements = new();
    public StyleDimension Top;
    public StyleDimension Left;
    public StyleDimension Width;
    public StyleDimension Height;
    public StyleDimension MaxWidth = StyleDimension.Fill;
    public StyleDimension MaxHeight = StyleDimension.Fill;
    public StyleDimension MinWidth = StyleDimension.Empty;
    public StyleDimension MinHeight = StyleDimension.Empty;
    public bool IgnoresMouseInteraction;
    public bool PassThroughMouseInteraction;
    public bool OverflowHidden;
    public float PaddingTop;
    public float PaddingLeft;
    public float PaddingRight;
    public float PaddingBottom;
    public float MarginTop;
    public float MarginLeft;
    public float MarginRight;
    public float MarginBottom;
    public float HAlign;
    public float VAlign;
    public bool UseImmediateMode;

    bool isInitialized;
    CalculatedStyle innerDimensions;
    CalculatedStyle dimensions;
    CalculatedStyle outerDimensions;

    public UIElement() => UniqueId = idCounter++;

    public UIElement? Parent { get; private set; }

    public int UniqueId { get; private set; }

    public IEnumerable<UIElement> Children => Elements;

    public bool IsMouseHovering { get; private set; }

    public event MouseEvent? OnLeftMouseDown;
    public event MouseEvent? OnLeftMouseUp;
    public event MouseEvent? OnLeftClick;
    public event MouseEvent? OnLeftDoubleClick;
    public event MouseEvent? OnRightMouseDown;
    public event MouseEvent? OnRightMouseUp;
    public event MouseEvent? OnRightClick;
    public event MouseEvent? OnRightDoubleClick;
    public event MouseEvent? OnMouseOver;
    public event MouseEvent? OnMouseOut;
    public event ScrollWheelEvent? OnScrollWheel;
    public event ElementEvent? OnUpdate;
    public event DrawEvent? OnDraw;

    public virtual void ExecuteRecursively(UIElementAction action)
    {
        action(this);
        foreach (UIElement element in Elements) element.ExecuteRecursively(action);
    }

    protected virtual void DrawSelf(SpriteBatch spriteBatch) { }

    protected virtual void DrawChildren(SpriteBatch spriteBatch)
    {
        foreach (UIElement element in Elements) element.Draw(spriteBatch);
    }

    public void Append(UIElement element)
    {
        element.Remove();
        element.Parent = this;
        Elements.Add(element);
        element.Recalculate();
    }

    public void Remove() => Parent?.RemoveChild(this);

    public void RemoveChild(UIElement child)
    {
        Elements.Remove(child);
        child.Parent = null;
    }

    public void RemoveAllChildren()
    {
        foreach (UIElement element in Elements) element.Parent = null;
        Elements.Clear();
    }

    public bool HasChild(UIElement child) => Elements.Contains(child);

    public void AddOrRemoveChild(UIElement child, bool add)
    {
        if (add && !HasChild(child)) Append(child);
        else if (!add && HasChild(child)) RemoveChild(child);
    }

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        OnDraw?.Invoke(this, spriteBatch);
        DrawSelf(spriteBatch);
        if (!OverflowHidden)
        {
            DrawChildren(spriteBatch);
            return;
        }
        PushClip(GetClippingRectangle(spriteBatch));
        try { DrawChildren(spriteBatch); }
        finally { PopClip(spriteBatch); }
    }

    void PushClip(Rectangle rect)
    {
        Clips.Add(rect);
        ApplyClip(Main.spriteBatch);
    }

    static void PopClip(SpriteBatch spriteBatch)
    {
        if (Clips.Count > 0) Clips.RemoveAt(Clips.Count - 1);
        ApplyClip(spriteBatch);
    }

    static void ApplyClip(SpriteBatch spriteBatch)
    {
        if (Clips.Count == 0)
        {
            SpriteBatchItem.DisabledClipping();
            return;
        }
        Rectangle top = Clips[^1];
        SpriteBatchItem.EnableClipping(top, top, spriteBatch, true);
    }

    public virtual void Update(GameTime gameTime)
    {
        OnUpdate?.Invoke(this);
        foreach (UIElement element in Elements.ToArray()) element.Update(gameTime);
    }

    public Rectangle GetClippingRectangle(SpriteBatch spriteBatch)
    {
        float x0 = innerDimensions.X, y0 = innerDimensions.Y;
        float x1 = x0 + innerDimensions.Width, y1 = y0 + innerDimensions.Height;
        if (Clips.Count > 0)
        {
            Rectangle outer = Clips[^1];
            x0 = Math.Max(x0, outer.X);
            y0 = Math.Max(y0, outer.Y);
            x1 = Math.Min(x1, outer.X + outer.Width);
            y1 = Math.Min(y1, outer.Y + outer.Height);
        }
        return new Rectangle((int)x0, (int)y0, Math.Max(0, (int)(x1 - x0)), Math.Max(0, (int)(y1 - y0)));
    }

    public virtual void Recalculate()
    {
        CalculatedStyle parent = Parent == null ? UserInterface.ActiveInstance.GetDimensions() : Parent.GetInnerDimensions();
        if (Parent is UIList) parent.Height = float.MaxValue;
        CalculatedStyle d = GetDimensionsBasedOnParentDimensions(parent);
        outerDimensions = d;
        d.X += MarginLeft;
        d.Y += MarginTop;
        d.Width -= MarginLeft + MarginRight;
        d.Height -= MarginTop + MarginBottom;
        dimensions = d;
        d.X += PaddingLeft;
        d.Y += PaddingTop;
        d.Width -= PaddingLeft + PaddingRight;
        d.Height -= PaddingTop + PaddingBottom;
        innerDimensions = d;
        RecalculateChildren();
    }

    CalculatedStyle GetDimensionsBasedOnParentDimensions(CalculatedStyle parent)
    {
        CalculatedStyle d;
        d.X = Left.GetValue(parent.Width) + parent.X;
        d.Y = Top.GetValue(parent.Height) + parent.Y;
        float minWidth = MinWidth.GetValue(parent.Width), maxWidth = MaxWidth.GetValue(parent.Width);
        float minHeight = MinHeight.GetValue(parent.Height), maxHeight = MaxHeight.GetValue(parent.Height);
        d.Width = Math.Clamp(Width.GetValue(parent.Width), minWidth, Math.Max(minWidth, maxWidth));
        d.Height = Math.Clamp(Height.GetValue(parent.Height), minHeight, Math.Max(minHeight, maxHeight));
        d.Width += MarginLeft + MarginRight;
        d.Height += MarginTop + MarginBottom;
        d.X += parent.Width * HAlign - d.Width * HAlign;
        d.Y += parent.Height * VAlign - d.Height * VAlign;
        return d;
    }

    public UIElement? GetElementAt(Vector2 point)
    {
        UIElement? found = null;
        for (int i = Elements.Count - 1; i >= 0; i--)
        {
            UIElement element = Elements[i];
            if (element.IgnoresMouseInteraction || !element.ContainsPoint(point)) continue;
            found = element;
            if (!element.PassThroughMouseInteraction) break;
        }
        if (found != null) return found.GetElementAt(point);
        if (IgnoresMouseInteraction) return null;
        return ContainsPoint(point) ? this : null;
    }

    public virtual bool ContainsPoint(Vector2 point) => dimensions.Contains(point);

    public virtual Rectangle GetViewCullingArea() => dimensions.ToRectangle();

    public void SetPadding(float pixels)
    {
        PaddingBottom = pixels;
        PaddingLeft = pixels;
        PaddingRight = pixels;
        PaddingTop = pixels;
    }

    public virtual void RecalculateChildren()
    {
        foreach (UIElement element in Elements) element.Recalculate();
    }

    public CalculatedStyle GetInnerDimensions() => innerDimensions;

    public CalculatedStyle GetDimensions() => dimensions;

    public CalculatedStyle GetOuterDimensions() => outerDimensions;

    public void CopyStyle(UIElement element)
    {
        Top = element.Top;
        Left = element.Left;
        Width = element.Width;
        Height = element.Height;
        PaddingBottom = element.PaddingBottom;
        PaddingLeft = element.PaddingLeft;
        PaddingRight = element.PaddingRight;
        PaddingTop = element.PaddingTop;
        HAlign = element.HAlign;
        VAlign = element.VAlign;
        MinWidth = element.MinWidth;
        MaxWidth = element.MaxWidth;
        MinHeight = element.MinHeight;
        MaxHeight = element.MaxHeight;
        Recalculate();
    }

    static void Raise(MouseEvent? handler, UIMouseEvent evt, UIElement element, string what)
    {
        if (handler == null) return;
        try { handler(evt, element); }
        catch (Exception e) { ItemHooks.Report($"{element.GetType().Name}.{what}", e); }
    }

    public virtual void LeftMouseDown(UIMouseEvent evt)
    {
        Raise(OnLeftMouseDown, evt, this, nameof(OnLeftMouseDown));
        Parent?.LeftMouseDown(evt);
    }

    public virtual void LeftMouseUp(UIMouseEvent evt)
    {
        Raise(OnLeftMouseUp, evt, this, nameof(OnLeftMouseUp));
        Parent?.LeftMouseUp(evt);
    }

    public virtual void LeftClick(UIMouseEvent evt)
    {
        Raise(OnLeftClick, evt, this, nameof(OnLeftClick));
        Parent?.LeftClick(evt);
    }

    public virtual void LeftDoubleClick(UIMouseEvent evt)
    {
        Raise(OnLeftDoubleClick, evt, this, nameof(OnLeftDoubleClick));
        Parent?.LeftDoubleClick(evt);
    }

    public virtual void RightMouseDown(UIMouseEvent evt)
    {
        Raise(OnRightMouseDown, evt, this, nameof(OnRightMouseDown));
        Parent?.RightMouseDown(evt);
    }

    public virtual void RightMouseUp(UIMouseEvent evt)
    {
        Raise(OnRightMouseUp, evt, this, nameof(OnRightMouseUp));
        Parent?.RightMouseUp(evt);
    }

    public virtual void RightClick(UIMouseEvent evt)
    {
        Raise(OnRightClick, evt, this, nameof(OnRightClick));
        Parent?.RightClick(evt);
    }

    public virtual void RightDoubleClick(UIMouseEvent evt)
    {
        Raise(OnRightDoubleClick, evt, this, nameof(OnRightDoubleClick));
        Parent?.RightDoubleClick(evt);
    }

    public virtual void MouseOver(UIMouseEvent evt)
    {
        IsMouseHovering = true;
        Raise(OnMouseOver, evt, this, nameof(OnMouseOver));
        Parent?.MouseOver(evt);
    }

    public virtual void MouseOut(UIMouseEvent evt)
    {
        IsMouseHovering = false;
        Raise(OnMouseOut, evt, this, nameof(OnMouseOut));
        Parent?.MouseOut(evt);
    }

    public virtual void ScrollWheel(UIScrollWheelEvent evt)
    {
        OnScrollWheel?.Invoke(evt, this);
        Parent?.ScrollWheel(evt);
    }

    public void Activate()
    {
        if (!isInitialized) Initialize();
        OnActivate();
        foreach (UIElement element in Elements) element.Activate();
    }

    public virtual void OnActivate() { }

    public void Deactivate()
    {
        OnDeactivate();
        foreach (UIElement element in Elements) element.Deactivate();
    }

    public virtual void OnDeactivate() { }

    public void Initialize()
    {
        OnInitialize();
        isInitialized = true;
    }

    public virtual void OnInitialize() { }

    public virtual int CompareTo(object? obj) => 0;
}
