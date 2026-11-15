using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Core;

namespace Terraria.UI;

internal static class UIInput
{
    internal readonly record struct Touch(int Sequence, bool Left, bool Right, int X, int Y);

    internal readonly struct Saved
    {
        internal readonly bool Changed, Left, Right;
        internal readonly int X, Y;

        internal Saved(bool left, bool right, int x, int y)
        {
            Changed = true;
            Left = left;
            Right = right;
            X = x;
            Y = y;
        }
    }

    const int MaxEvents = 32;

    static readonly List<Touch> events = new();
    static readonly HashSet<UserInterface> drawn = new();
    static readonly List<UserInterface> registering = new();
    static bool hooked;

    internal static int Sequence { get; private set; }

    internal static Touch Current { get; private set; }

    internal static Vector2 ScreenSize;

    internal static List<Touch> Events => events;

    internal static void Drawn(UserInterface ui)
    {
        drawn.Add(ui);
        if (hooked) return;
        hooked = true;
        ItemHooks.Once("UIInput", () => On_Main.DrawInterface += DrawInterface);
    }

    internal static Saved Push(Touch touch)
    {
        bool left = Main.mouseLeft, right = Main.mouseRight;
        int x = Main.mouseX, y = Main.mouseY;
        if (left == touch.Left && right == touch.Right && x == touch.X && y == touch.Y) return default;
        Main.mouseLeft = touch.Left;
        Main.mouseRight = touch.Right;
        Main.mouseX = touch.X;
        Main.mouseY = touch.Y;
        return new Saved(left, right, x, y);
    }

    internal static void Pop(in Saved saved)
    {
        if (!saved.Changed) return;
        Main.mouseLeft = saved.Left;
        Main.mouseRight = saved.Right;
        Main.mouseX = saved.X;
        Main.mouseY = saved.Y;
    }

    static void DrawInterface(On_Main.orig_DrawInterface orig, Main self, GameTime gameTime)
    {
        try { Capture(); }
        catch (Exception e) { ItemHooks.Report("UserInterface (touch)", e); }
        orig(self, gameTime);
    }

    static void Capture()
    {
        bool left = Main.mouseLeft, right = Main.mouseRight;
        if (left != Current.Left || right != Current.Right)
        {
            events.Add(new Touch(++Sequence, left, right, Main.mouseX, Main.mouseY));
            if (events.Count > MaxEvents) events.RemoveAt(0);
        }
        Current = new Touch(Sequence, left, right, Main.mouseX, Main.mouseY);
        if (drawn.Count == 0) return;
        registering.AddRange(drawn);
        drawn.Clear();
        try
        {
            foreach (UserInterface ui in registering)
            {
                try { ui.RegisterRegions(); }
                catch (Exception e) { ItemHooks.Report("UserInterface (touch regions)", e); }
            }
        }
        finally { registering.Clear(); }
    }
}
