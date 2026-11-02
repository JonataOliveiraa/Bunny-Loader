using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UIList : UIElement, IEnumerable<UIElement>
{
    public delegate bool ElementSearchMethod(UIElement element);

    sealed class UIInnerList : UIElement
    {
        public override bool ContainsPoint(Vector2 point) => true;

        protected override void DrawChildren(SpriteBatch spriteBatch)
        {
            Vector2 position = Parent!.GetDimensions().Position();
            Vector2 size = new(Parent.GetDimensions().Width, Parent.GetDimensions().Height);
            foreach (UIElement element in Elements)
            {
                CalculatedStyle d = element.GetDimensions();
                if (d.Y + d.Height >= position.Y && d.Y <= position.Y + size.Y) element.Draw(spriteBatch);
            }
        }

        public override Rectangle GetViewCullingArea() => Parent!.GetDimensions().ToRectangle();
    }

    const float DragThreshold = 6f;

    protected List<UIElement> _items = new();
    protected UIScrollbar? _scrollbar;
    internal UIElement _innerList = new UIInnerList();
    float innerListHeight;
    float ownView;
    public float ListPadding = 5f;
    public Action<List<UIElement>>? ManualSortMethod;
    float? dragStartY;
    float dragStartView;

    public UIList()
    {
        _innerList.OverflowHidden = false;
        _innerList.Width.Set(0f, 1f);
        _innerList.Height.Set(0f, 1f);
        OverflowHidden = true;
        Append(_innerList);
    }

    public int Count => _items.Count;

    public bool IsDragging { get; private set; }

    public float ViewPosition
    {
        get => _scrollbar?.ViewPosition ?? ownView;
        set
        {
            if (_scrollbar != null) _scrollbar.ViewPosition = value;
            else ownView = Math.Clamp(value, 0f, Math.Max(0f, innerListHeight - GetInnerDimensions().Height));
        }
    }

    public float GetTotalHeight() => innerListHeight;

    public void Goto(ElementSearchMethod searchMethod)
    {
        foreach (UIElement item in _items)
        {
            if (!searchMethod(item)) continue;
            ViewPosition = item.Top.Pixels;
            return;
        }
    }

    public virtual void Add(UIElement item)
    {
        _items.Add(item);
        _innerList.Append(item);
        UpdateOrder();
        _innerList.Recalculate();
    }

    public virtual void AddRange(IEnumerable<UIElement> items)
    {
        foreach (UIElement item in items)
        {
            _items.Add(item);
            _innerList.Append(item);
        }
        UpdateOrder();
        _innerList.Recalculate();
    }

    public virtual bool Remove(UIElement item)
    {
        _innerList.RemoveChild(item);
        UpdateOrder();
        return _items.Remove(item);
    }

    public virtual void Clear()
    {
        _innerList.RemoveAllChildren();
        _items.Clear();
    }

    public override void Recalculate()
    {
        base.Recalculate();
        UpdateScrollbar();
    }

    public override void ScrollWheel(UIScrollWheelEvent evt)
    {
        base.ScrollWheel(evt);
        ViewPosition -= evt.ScrollWheelValue;
    }

    public override void RecalculateChildren()
    {
        base.RecalculateChildren();
        float pixels = 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            float padding = _items.Count == 1 ? 0f : ListPadding;
            _items[i].Top.Set(pixels, 0f);
            _items[i].Recalculate();
            pixels += _items[i].GetOuterDimensions().Height + padding;
        }
        innerListHeight = pixels;
    }

    void UpdateScrollbar() => _scrollbar?.SetView(GetInnerDimensions().Height, innerListHeight);

    public void SetScrollbar(UIScrollbar scrollbar)
    {
        _scrollbar = scrollbar;
        UpdateScrollbar();
    }

    public void UpdateOrder()
    {
        if (ManualSortMethod != null)
        {
            ManualSortMethod(_items);
        }
        else
        {
            List<UIElement> sorted = _items.OrderBy(item => item, Comparer<UIElement>.Create(SortMethod)).ToList();
            _items.Clear();
            _items.AddRange(sorted);
        }
        UpdateScrollbar();
    }

    public int SortMethod(UIElement item1, UIElement item2) => item1.CompareTo(item2);

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        float y = Main.mouseY;
        if (!Main.mouseLeft)
        {
            dragStartY = null;
            IsDragging = false;
            return;
        }
        if (dragStartY == null)
        {
            if (!ContainsPoint(new Vector2(Main.mouseX, y))) return;
            dragStartY = y;
            dragStartView = ViewPosition;
            return;
        }
        if (!IsDragging && Math.Abs(y - dragStartY.Value) > DragThreshold) IsDragging = true;
        if (IsDragging) ViewPosition = dragStartView - (y - dragStartY.Value);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        _innerList.Top.Set(-ViewPosition, 0f);
        Recalculate();
    }

    public IEnumerator<UIElement> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
