using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Core;

namespace Terraria.UI;

internal static class UIInput
{
    internal readonly record struct Touch(int Sequence, bool Left, bool Right, int X, int Y);

    const int MaxEvents = 32;

    static readonly List<Touch> events = new();
    static readonly HashSet<UserInterface> drawn = new();

    internal static int Sequence { get; private set; }

    internal static Touch Current { get; private set; }

    internal static Vector2 ScreenSize;

    internal static void Drawn(UserInterface ui)
    {
        drawn.Add(ui);
        ItemHooks.Once("UIInput", () => On_Main.DrawInterface += DrawInterface);
    }

    internal static List<Touch> Since(int seen)
    {
        var result = new List<Touch>();
        foreach (Touch touch in events)
            if (touch.Sequence > seen) result.Add(touch);
        return result;
    }

    internal static void With(Touch touch, Action action)
    {
        bool left = Main.mouseLeft, right = Main.mouseRight;
        int x = Main.mouseX, y = Main.mouseY;
        if (left == touch.Left && right == touch.Right && x == touch.X && y == touch.Y)
        {
            action();
            return;
        }
        Main.mouseLeft = touch.Left;
        Main.mouseRight = touch.Right;
        Main.mouseX = touch.X;
        Main.mouseY = touch.Y;
        try { action(); }
        finally
        {
            Main.mouseLeft = left;
            Main.mouseRight = right;
            Main.mouseX = x;
            Main.mouseY = y;
        }
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
        var uis = new List<UserInterface>(drawn);
        drawn.Clear();
        foreach (UserInterface ui in uis)
        {
            try { ui.RegisterRegions(); }
            catch (Exception e) { ItemHooks.Report("UserInterface (touch regions)", e); }
        }
    }
}
