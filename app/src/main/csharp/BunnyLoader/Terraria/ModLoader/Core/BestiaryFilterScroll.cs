using System;
using System.Runtime.InteropServices;
using BunnyLoader.Hooks;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader.Core;

internal static unsafe class BestiaryFilterScroll
{
    const float DragThreshold = 10f;
    const float Dampen = 0.92f;
    const float Elastic = 0.25f;
    const float Overscroll = 0.4f;

    static readonly int* Drawing = Awake();

    static GUIBestiary bestiary;
    static float scroll, glide;
    static bool wasDown, dragging, moved, tapping;
    static float dragStart, scrollAtStart, lastPointer, velocity;
    static int pressedFilter = -1;

    static int* Awake()
    {
        int* flag = (int*)NativeMemory.Alloc((nuint)sizeof(int));
        *flag = 0;
        return flag;
    }

    internal static void Install() => ItemHooks.Once("BestiaryFilterScroll", () =>
    {
        HookGates.Define<On_GUIItemGrid.hook_Draw_ItemGrid_Layout_GetItemScaleHandler_CursorOver_DrawItemHandler_Vector2_int>(() =>
            new GateSpec { Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = 0, FieldSize = 4, Address = (IntPtr)Drawing });
        HookGates.Want<On_GUIItemGrid.hook_Draw_ItemGrid_Layout_GetItemScaleHandler_CursorOver_DrawItemHandler_Vector2_int>();
        On_GUIItemGrid.Draw_ItemGrid_Layout_GetItemScaleHandler_CursorOver_DrawItemHandler_Vector2_int += GridDraw;
        On_GUIBestiary.DrawFilters += DrawFilters;
        On_GUIBestiary.Open += Open;
    });

    static void DrawFilters(On_GUIBestiary.orig_DrawFilters orig, GUIBestiary self)
    {
        bestiary = self;
        *Drawing = 1;
        try { orig(self); }
        finally { *Drawing = 0; }
    }

    static void Open(On_GUIBestiary.orig_Open orig, GUIBestiary self)
    {
        scroll = 0f;
        glide = 0f;
        dragging = false;
        pressedFilter = -1;
        orig(self);
    }

    static void GridDraw(On_GUIItemGrid.orig_Draw_ItemGrid_Layout_GetItemScaleHandler_CursorOver_DrawItemHandler_Vector2_int orig,
        ItemGrid_Layout gridLayout, GUIItemGrid.GetItemScaleHandler scaleHandler, GUIItemGrid.CursorOver cursorOverHandler,
        GUIItemGrid.DrawItemHandler drawhandler, Vector2 itemSize, int overloadedItemCount)
    {
        bool handled = false;
        try
        {
            if (*Drawing != 0 && !gridLayout.IsNull && !bestiary.IsNull && gridLayout.Pointer == Bestiary_Layout.Instance.FiltersGrid.Pointer)
            {
                if (gridLayout.Horizontal && overloadedItemCount > gridLayout.ItemCount && gridLayout.ItemLineCount > 0 &&
                    gridLayout.ItemCount >= gridLayout.ItemLineCount)
                {
                    DrawList(gridLayout, itemSize, overloadedItemCount);
                    handled = true;
                }
                else
                {
                    scroll = 0f;
                    glide = 0f;
                }
            }
        }
        catch (Exception e) { ItemHooks.Report("bestiary filter scroll", e); }
        if (!handled) orig(gridLayout, scaleHandler, cursorOverHandler, drawhandler, itemSize, overloadedItemCount);
    }

    static void DrawList(ItemGrid_Layout grid, Vector2 itemSize, int total)
    {
        int columns = grid.ItemLineCount;
        int rows = grid.ItemCount / columns;

        Vector2 origin = LayoutCalculator.GetAnchoredPosition(grid.FirstAnchorControl, grid.FirstItemAnchor, grid.FirstItemLocation);
        Vector2 spacing = grid.ExtraElementSpacing;
        float stepX = itemSize.X + spacing.X;
        float stepY = itemSize.Y + spacing.Y;
        float columnStep = Math.Abs(stepX), rowStep = Math.Abs(stepY);
        if (rowStep < 1f) rowStep = itemSize.Y;
        if (columnStep < 1f) columnStep = itemSize.X;
        float left = stepX < 0f ? origin.X + (columns - 1) * stepX : origin.X;
        float top = stepY < 0f ? origin.Y + (rows - 1) * stepY : origin.Y;
        Rectangle window = new((int)left, (int)top, (int)((columns - 1) * columnStep + itemSize.X), (int)((rows - 1) * rowStep + itemSize.Y));

        int lines = (total + columns - 1) / columns;
        float max = Math.Max(0, lines - rows) * rowStep;

        bool down = Main.mouseLeft;
        bool pressed = down && !wasDown;
        UpdateTouch(window, max, down, pressed);

        Vector2 border = grid.PickingBorder, offset = grid.PickingOffset;
        GUIInputRegionManager regions = GUIInputRegionManager.Instance;
        SpriteBatch batch = Main.spriteBatch;
        int first = Math.Max(0, (int)Math.Floor(scroll / rowStep));
        int last = Math.Min(lines - 1, (int)Math.Ceiling((scroll + window.Height) / rowStep));
        for (int line = first; line <= last; line++)
        {
            float y = top + line * rowStep - scroll;
            for (int column = 0; column < columns; column++)
            {
                int index = line * columns + column;
                if (index >= total) break;
                float x = left + column * columnStep;
                Rectangle cell = new((int)x, (int)y, (int)itemSize.X, (int)itemSize.Y);
                if (!cell.Intersects(window)) continue;

                Rectangle pick = Rectangle.Intersect(new Rectangle((int)(x - border.X + offset.X), (int)(y - border.Y + offset.Y),
                    (int)(itemSize.X + border.X * 2f), (int)(itemSize.Y + border.Y * 2f)), window);
                if (pick.Width > 0 && pick.Height > 0 && regions.RegisterInputRegion(pick))
                {
                    bestiary.filterOver = index;
                    if (pressed && dragging && pressedFilter < 0) pressedFilter = index;
                }

                bool inside = window.Contains(cell);
                if (!inside) SpriteBatchItem.EnableClipping(window, window, batch, true);
                bestiary.FilterDraw(grid, index, new Vector2(x, y), 1f);
                if (!inside) SpriteBatchItem.DisabledClipping();
            }
        }
    }

    static void UpdateTouch(Rectangle window, float max, bool down, bool pressed)
    {
        float pointer = Main.mouseY;
        if (pressed)
        {
            dragging = window.Contains(Main.mouseX, Main.mouseY);
            moved = false;
            dragStart = pointer;
            lastPointer = pointer;
            scrollAtStart = scroll;
            velocity = 0f;
            pressedFilter = -1;
            if (dragging) glide = 0f;
        }
        else if (down && dragging)
        {
            float delta = pointer - dragStart;
            if (!moved && Math.Abs(delta) > DragThreshold)
            {
                moved = true;
                dragStart = pointer;
                scrollAtStart = scroll;
                delta = 0f;
            }
            if (moved)
            {
                float target = scrollAtStart - delta;
                if (target < 0f) target *= Overscroll;
                else if (target > max) target = max + (target - max) * Overscroll;
                scroll = target;
                velocity = velocity * 0.5f + (lastPointer - pointer) * 0.5f;
            }
            lastPointer = pointer;
        }
        else if (!down && wasDown)
        {
            if (dragging && moved) glide = velocity;
            else if (dragging && pressedFilter >= 0) Tap(pressedFilter);
            dragging = false;
            pressedFilter = -1;
        }

        if (!down || !dragging) Glide(max);
        wasDown = down;
    }

    static void Glide(float max)
    {
        scroll += glide;
        glide *= Dampen;
        float edge = Math.Clamp(scroll, 0f, max);
        if (scroll != edge)
        {
            glide *= 0.5f;
            scroll += (edge - scroll) * Elastic;
            if (Math.Abs(scroll - edge) < 0.5f) scroll = edge;
        }
        if (Math.Abs(glide) < 0.05f) glide = 0f;
    }

    static void Tap(int filter)
    {
        if (bestiary.IsNull || tapping) return;
        bool left = Main.mouseLeft, release = Main.mouseLeftRelease;
        Main.mouseLeft = true;
        Main.mouseLeftRelease = true;
        tapping = true;
        try { bestiary.FilterOver(filter); }
        finally
        {
            tapping = false;
            Main.mouseLeft = left;
            Main.mouseLeftRelease = release;
        }
    }
}
