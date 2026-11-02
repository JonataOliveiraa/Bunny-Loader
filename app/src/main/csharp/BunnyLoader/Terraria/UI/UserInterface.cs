using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Core;

namespace Terraria.UI;

public class UserInterface
{
    const long DoubleClickMs = 500;
    const long StateChangeClickDisableMs = 200;
    const int MaxHistorySize = 32;
    const int HistoryPruneSize = 4;

    sealed class PointerCache
    {
        public long LastTimeDown;
        public bool WasDown;
        public UIElement? LastDown;
        public UIElement? LastClicked;
        public required Action<UIElement, UIMouseEvent> MouseDown;
        public required Action<UIElement, UIMouseEvent> MouseUp;
        public required Action<UIElement, UIMouseEvent> Click;
        public required Action<UIElement, UIMouseEvent> DoubleClick;

        public void Clear()
        {
            LastClicked = null;
            LastDown = null;
            LastTimeDown = 0;
        }
    }

    public static UserInterface ActiveInstance = new();

    readonly List<UIState> history = new();
    readonly PointerCache leftMouse = new()
    {
        MouseDown = (e, evt) => e.LeftMouseDown(evt),
        MouseUp = (e, evt) => e.LeftMouseUp(evt),
        Click = (e, evt) => e.LeftClick(evt),
        DoubleClick = (e, evt) => e.LeftDoubleClick(evt),
    };
    readonly PointerCache rightMouse = new()
    {
        MouseDown = (e, evt) => e.RightMouseDown(evt),
        MouseUp = (e, evt) => e.RightMouseUp(evt),
        Click = (e, evt) => e.RightClick(evt),
        DoubleClick = (e, evt) => e.RightDoubleClick(evt),
    };

    public Vector2 MousePosition;
    public bool IsVisible;
    UIElement? lastElementHover;
    long clickDisabledUntil;
    bool isStateDirty;
    UIState? currentState;
    int seenInput = UIInput.Sequence;
    Vector2 sizeAtRecalculate;

    public UserInterface() => ActiveInstance = this;

    public UIState? CurrentState => currentState;

    public void ClearPointers()
    {
        leftMouse.Clear();
        rightMouse.Clear();
    }

    public bool MouseCaptured() => (leftMouse.WasDown && leftMouse.LastDown != null) || (rightMouse.WasDown && rightMouse.LastDown != null);

    public void ResetLasts()
    {
        lastElementHover?.MouseOut(new UIMouseEvent(lastElementHover, MousePosition));
        ClearPointers();
        lastElementHover = null;
    }

    public void EscapeElements() => ResetLasts();

    public void Use()
    {
        if (ActiveInstance == this) return;
        ActiveInstance = this;
        Recalculate();
    }

    void ResetState()
    {
        if (!Main.dedServ)
        {
            ReadMouse(UIInput.Current);
            leftMouse.WasDown = UIInput.Current.Left;
            rightMouse.WasDown = UIInput.Current.Right;
            lastElementHover?.MouseOut(new UIMouseEvent(lastElementHover, MousePosition));
        }
        ClearPointers();
        lastElementHover = null;
        clickDisabledUntil = Math.Max(clickDisabledUntil, Environment.TickCount64 + StateChangeClickDisableMs);
    }

    void ReadMouse(UIInput.Touch touch) => MousePosition = new Vector2(touch.X, touch.Y);

    public void Update(GameTime time)
    {
        if (currentState == null)
        {
            seenInput = UIInput.Sequence;
            return;
        }
        foreach (UIInput.Touch touch in UIInput.Since(seenInput)) UIInput.With(touch, () => Pointer(touch));
        seenInput = UIInput.Sequence;
        UIInput.With(UIInput.Current, () =>
        {
            Pointer(UIInput.Current);
            currentState?.Update(time);
        });
    }

    void Pointer(UIInput.Touch touch)
    {
        UIState? state = currentState;
        if (state == null) return;
        ReadMouse(touch);
        bool touching = touch.Left || leftMouse.WasDown;
        UIElement? hover = touching ? state.GetElementAt(MousePosition) : null;
        if (hover != lastElementHover)
        {
            lastElementHover?.MouseOut(new UIMouseEvent(lastElementHover, MousePosition));
            hover?.MouseOver(new UIMouseEvent(hover, MousePosition));
            lastElementHover = hover;
        }
        if (Environment.TickCount64 < clickDisabledUntil) return;
        HandleClick(leftMouse, touch.Left, hover);
        HandleClick(rightMouse, touch.Right, hover);
    }

    void HandleClick(PointerCache cache, bool isDown, UIElement? element)
    {
        long now = Environment.TickCount64;
        if (isDown && !cache.WasDown && element != null)
        {
            cache.LastDown = element;
            cache.MouseDown(element, new UIMouseEvent(element, MousePosition));
            if (cache.LastClicked == element && now - cache.LastTimeDown < DoubleClickMs)
            {
                cache.DoubleClick(element, new UIMouseEvent(element, MousePosition));
                cache.LastClicked = null;
            }
            cache.LastTimeDown = now;
        }
        else if (!isDown && cache.WasDown && cache.LastDown != null)
        {
            UIElement down = cache.LastDown;
            if (down.ContainsPoint(MousePosition) && !InDraggedList(down))
            {
                cache.Click(down, new UIMouseEvent(down, MousePosition));
                cache.LastClicked = down;
            }
            cache.MouseUp(down, new UIMouseEvent(down, MousePosition));
            cache.LastDown = null;
        }
        cache.WasDown = isDown;
    }

    static bool InDraggedList(UIElement element)
    {
        for (UIElement? e = element; e != null; e = e.Parent)
            if (e is UIList { IsDragging: true }) return true;
        return false;
    }

    public void Draw(SpriteBatch spriteBatch, GameTime time)
    {
        Use();
        if (currentState == null) return;
        UIInput.ScreenSize = new Vector2(Main.screenWidth, Main.screenHeight);
        if (isStateDirty || sizeAtRecalculate != UIInput.ScreenSize)
        {
            currentState.Recalculate();
            sizeAtRecalculate = UIInput.ScreenSize;
            isStateDirty = false;
        }
        UIInput.Drawn(this);
        currentState.Draw(spriteBatch);
    }

    internal void RegisterRegions()
    {
        if (currentState == null) return;
        GUIInputRegionManager manager = GUIInputRegionManager.Instance;
        if (manager.IsNull) return;
        foreach (UIElement element in currentState.Children)
        {
            if (element.IgnoresMouseInteraction) continue;
            CalculatedStyle d = element.GetDimensions();
            if (d.Width <= 0 || d.Height <= 0) continue;
            manager.RegisterInputRegion(new Rectangle((int)d.X, (int)d.Y, (int)Math.Ceiling(d.Width), (int)Math.Ceiling(d.Height)));
        }
    }

    public void SetState(UIState? state)
    {
        if (state == currentState) return;
        if (state != null) AddToHistory(state);
        if (currentState != null)
        {
            lastElementHover?.MouseOut(new UIMouseEvent(lastElementHover, MousePosition));
            currentState.Deactivate();
        }
        currentState = state;
        ResetState();
        if (state != null)
        {
            isStateDirty = true;
            state.Activate();
            state.Recalculate();
        }
        IsVisible = currentState != null;
    }

    public void GoBack()
    {
        if (history.Count < 2) return;
        UIState state = history[^2];
        history.RemoveRange(history.Count - 2, 2);
        SetState(state);
    }

    void AddToHistory(UIState state)
    {
        history.Add(state);
        if (history.Count > MaxHistorySize) history.RemoveRange(0, HistoryPruneSize);
    }

    public void Recalculate() => currentState?.Recalculate();

    public CalculatedStyle GetDimensions()
    {
        Vector2 size = UIInput.ScreenSize;
        if (size.X <= 0)
        {
            try { size = new Vector2(Main.screenWidth, Main.screenHeight); }
            catch (Exception) { size = Vector2.Zero; }
        }
        return new CalculatedStyle(0f, 0f, size.X, size.Y);
    }

    internal void RefreshState()
    {
        currentState?.Deactivate();
        ResetState();
        currentState?.Activate();
        currentState?.Recalculate();
    }

    public bool IsElementUnderMouse() => IsVisible && lastElementHover != null && lastElementHover is not UIState;
}
