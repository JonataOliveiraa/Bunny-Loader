using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader;

internal static unsafe class ItemTooltipDraw
{
    const string DrawStringSignature =
        "void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)";

    static readonly Regex Tag = new(@"\[c/([0-9a-fA-F]{6}):([^\]]*)\]", RegexOptions.Compiled);

    static Hook? drawString, measure, tooltip;

    static Drawing? drawing;

    struct Pass
    {
        public IntPtr Self, Font;
        public float X, Y, Rotation, OriginX, OriginY, Scale, Depth;
        public uint Color;
        public int Effects;
        public IntPtr Method;
    }

    sealed class Drawing
    {
        public Item Item;
        public List<TooltipLine> Lines = null!;
        public bool CanDraw = true;
        public int Dx, Dy;
        public int Next;
        public int Shift;
        public int YOffset;
        public int Index = -1;
        public string Text = "";
        public readonly List<Pass> Shadows = new();
        public readonly List<DrawableTooltipLine> Drawn = new();
    }

    public static void Install() =>
        tooltip = ItemHooks.Raw("Terraria", "Main", "void MouseText_DrawItemTooltip(Main.MouseTextCache info, int rare, byte diff, int X, int Y)",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int, byte, int, int, IntPtr, void>)&DrawItemTooltip);

    [UnmanagedCallersOnly]
    static void DrawItemTooltip(IntPtr self, IntPtr info, int rare, byte diff, int x, int y, IntPtr method)
    {
        Hook? hook = tooltip;
        if (hook == null) return;
        void Original()
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(self);
            call.Arg(info);
            call.Arg(rare);
            call.Arg(diff);
            call.Arg(x);
            call.Arg(y);
            call.Arg(method);
            ItemHooks.Original(ref call, hook, "Main.MouseText_DrawItemTooltip");
        }
        try { Draw(Original, x, y); }
        catch (Exception e) { ItemHooks.Report("tooltip drawing", e); }
    }

    static void EnsureStringHooks() => ItemHooks.Once("TooltipStrings", () =>
    {
        measure = ItemHooks.Raw("Microsoft.Xna.Framework.Graphics", "SpriteFont", "Vector2 MeasureString(string text)",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, Vector2>)&MeasureString);
        drawString = ItemHooks.Raw("Microsoft.Xna.Framework.Graphics", "SpriteBatch", DrawStringSignature,
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, float, float, uint, float, float, float, float, int, float, IntPtr, void>)&DrawString);
    });

    static bool HasTags(string? text) => text != null && text.Contains("[c/", StringComparison.Ordinal);

    static string Strip(string text) => Tag.Replace(text, "$2");

    static bool DrawHooks(Item item)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            for (int i = 1; i < ItemTooltipHooks.HookNames.Length; i++)
                if (ItemTooltipHooks.Has(m, ItemTooltipHooks.HookNames[i])) return true;
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            for (int i = 1; i < ItemTooltipHooks.HookNames.Length; i++)
                if (ItemHooks.Overrides(g.GetType(), typeof(GlobalItem), ItemTooltipHooks.HookNames[i])) return true;
        }
        return false;
    }

    static List<TooltipLine>? Prepare(Item item)
    {
        var texts = GameArray<GameString>.New(new GameClass("System", "String"), 64);
        var prefix = GameArray<bool>.New(new GameClass("System", "Boolean"), 64);
        var bad = GameArray<bool>.New(new GameClass("System", "Boolean"), 64);
        int yoyo = -1, research = -1, materials = -1, count = 0, setBonus = -1, shared = -1;
        Color setColor = new(255, 255, 255, 255);
        ItemTooltipHooks.Last = null;
        Main.MouseText_DrawItemTooltip_GetLinesInfo(item, ref yoyo, ref research, ref materials, item.knockBack, ref count, texts, prefix, bad,
            ref setBonus, ref setColor, ref shared);
        return ItemTooltipHooks.Last;
    }

    static void Draw(Action orig, int x, int y)
    {
        Item item = default;
        Drawing? state = null;
        try
        {
            item = Main.HoverItem;
            if (drawing == null && ItemTooltipHooks.Wanted(item) && Prepare(item) is List<TooltipLine> lines)
            {
                state = new Drawing { Item = item, Lines = lines };
                int px = x, py = y;
                state.CanDraw = PreDrawTooltip(item, lines.AsReadOnly(), ref px, ref py);
                state.Dx = px - x;
                state.Dy = py - y;
                bool needed = DrawHooks(item) || lines.Exists(l => l.OverrideColor != null || HasTags(l.Text));
                if (needed) EnsureStringHooks();
                else state = null;
                ItemTooltipHooks.PreparedItem = item.Pointer;
                ItemTooltipHooks.Prepared = lines;
            }
        }
        catch (Exception e)
        {
            ItemHooks.Report("PreDrawTooltip", e);
            state = null;
        }
        drawing = state;
        try { orig(); }
        finally
        {
            drawing = null;
            ItemTooltipHooks.Prepared = null;
            ItemTooltipHooks.PreparedItem = IntPtr.Zero;
        }
        if (state == null) return;
        try
        {
            Flush(state);
            PostDrawTooltip(state.Item, state.Drawn.AsReadOnly());
        }
        catch (Exception e) { ItemHooks.Report("PostDrawTooltip", e); }
    }

    [UnmanagedCallersOnly]
    static void DrawString(IntPtr self, IntPtr font, IntPtr text, float x, float y, uint color, float rotation, float originX, float originY,
                           float scale, int effects, float depth, IntPtr method)
    {
        Hook? hook = drawString;
        if (hook == null) return;
        var pass = new Pass
        {
            Self = self, Font = font, X = x, Y = y, Color = color, Rotation = rotation, OriginX = originX, OriginY = originY,
            Scale = scale, Effects = effects, Depth = depth, Method = method,
        };
        Drawing? state = drawing;
        if (state != null && text != IntPtr.Zero)
        {
            try
            {
                if (Take(state, GameString.Read(text) ?? "", pass)) return;
            }
            catch (Exception e) { ItemHooks.Report("PreDrawTooltipLine", e); }
        }
        Original(hook, pass, text);
    }

    static void Original(Hook hook, in Pass p, IntPtr text)
    {
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(p.Self);
        call.Arg(p.Font);
        call.Arg(text);
        call.Arg(p.X);
        call.Arg(p.Y);
        call.Arg((long)p.Color);
        call.Arg(p.Rotation);
        call.Arg(p.OriginX);
        call.Arg(p.OriginY);
        call.Arg(p.Scale);
        call.Arg(p.Effects);
        call.Arg(p.Depth);
        call.Arg(p.Method);
        ItemHooks.Original(ref call, hook, "SpriteBatch.DrawString");
    }

    static bool Take(Drawing state, string text, Pass pass)
    {
        bool shadow = (pass.Color & 0x00FFFFFF) == 0;
        if (state.Index < 0 || text != state.Text)
        {
            int at = state.Lines.FindIndex(state.Next, l => l.Text == text);
            if (at < 0) return false;
            Flush(state);
            state.Index = at;
            state.Text = text;
            state.Next = at + 1;
        }
        if (shadow)
        {
            state.Shadows.Add(pass);
            return true;
        }
        Line(state, pass);
        return true;
    }

    static void Line(Drawing state, Pass main)
    {
        TooltipLine line = state.Lines[state.Index];
        Color color = *(Color*)&main.Color;
        if (line.OverrideColor is Color over)
        {
            float alpha = color.A / 255f;
            color = new Color((int)(over.R * alpha), (int)(over.G * alpha), (int)(over.B * alpha), color.A);
        }
        var dl = new DrawableTooltipLine(line, state.Index, (int)main.X, (int)main.Y, color)
        {
            Font = new GameObject(main.Font),
            Rotation = main.Rotation,
            Origin = new Vector2(main.OriginX, main.OriginY),
            BaseScale = new Vector2(main.Scale),
        };
        bool go = PreDrawTooltipLine(state.Item, dl, ref state.YOffset) && state.CanDraw;
        if (go)
        {
            float dx = dl.X - dl.OriginalX + state.Dx, dy = dl.Y - dl.OriginalY + state.Dy + state.Shift;
            float spread = dl.Spread / 2f;
            foreach (Pass s in state.Shadows)
            {
                Pass shadow = s;
                shadow.X = main.X + dx + (s.X - main.X) * spread;
                shadow.Y = main.Y + dy + (s.Y - main.Y) * spread;
                Apply(ref shadow, dl);
                Write(shadow, Strip(line.Text), shadowColor: true, dl);
            }
            Pass text = main;
            text.X += dx;
            text.Y += dy;
            Color drawn = dl.Color;
            text.Color = *(uint*)&drawn;
            Apply(ref text, dl);
            Write(text, line.Text, shadowColor: false, dl);
        }
        state.Shadows.Clear();
        PostDrawTooltipLine(state.Item, dl);
        state.Drawn.Add(dl);
        state.Shift += state.YOffset;
        state.Index = -1;
        state.Text = "";
    }

    static void Apply(ref Pass p, DrawableTooltipLine dl)
    {
        p.Font = dl.Font.Pointer;
        p.Rotation = dl.Rotation;
        p.OriginX = dl.Origin.X;
        p.OriginY = dl.Origin.Y;
        p.Scale = dl.BaseScale.X;
    }

    static void Write(Pass p, string text, bool shadowColor, DrawableTooltipLine dl)
    {
        Hook? hook = drawString;
        if (hook == null) return;
        if (shadowColor || !HasTags(text))
        {
            Original(hook, p, GameString.New(Strip(text)));
            return;
        }
        Color baseColor = *(Color*)&p.Color;
        float alpha = baseColor.A / 255f;
        float x = p.X;
        int at = 0;
        foreach (Match m in Tag.Matches(text))
        {
            if (m.Index > at) x = Segment(hook, p, text[at..m.Index], baseColor, x);
            int rgb = Convert.ToInt32(m.Groups[1].Value, 16);
            var c = new Color((int)(((rgb >> 16) & 255) * alpha), (int)(((rgb >> 8) & 255) * alpha), (int)((rgb & 255) * alpha), baseColor.A);
            x = Segment(hook, p, m.Groups[2].Value, c, x);
            at = m.Index + m.Length;
        }
        if (at < text.Length) Segment(hook, p, text[at..], baseColor, x);
    }

    static float Segment(Hook hook, Pass p, string text, Color color, float x)
    {
        if (text.Length == 0) return x;
        IntPtr s = GameString.New(text);
        p.X = x;
        p.Color = *(uint*)&color;
        Original(hook, p, s);
        return x + Measure(p.Font, s).X * p.Scale;
    }

    static void Flush(Drawing state)
    {
        Hook? hook = drawString;
        if (state.Index >= 0 && hook != null)
        {
            IntPtr text = GameString.New(Strip(state.Text));
            foreach (Pass s in state.Shadows) Original(hook, s, text);
        }
        state.Shadows.Clear();
        state.Index = -1;
        state.Text = "";
    }

    static Vector2 Measure(IntPtr font, IntPtr text)
    {
        Hook? hook = measure;
        if (hook == null || font == IntPtr.Zero) return default;
        var call = GameCall.Returning(RetKind.H2F);
        call.Arg(font);
        call.Arg(text);
        call.Arg(IntPtr.Zero);
        long raw = ItemHooks.Original(ref call, hook, "SpriteFont.MeasureString");
        float* f = (float*)&raw;
        return new Vector2(f[0], f[1]);
    }

    [UnmanagedCallersOnly]
    static Vector2 MeasureString(IntPtr self, IntPtr text, IntPtr method)
    {
        Hook? hook = measure;
        if (hook == null) return default;
        IntPtr measured = text;
        if (drawing != null && text != IntPtr.Zero)
        {
            try
            {
                string? s = GameString.Read(text);
                if (HasTags(s)) measured = GameString.New(Strip(s!));
            }
            catch (Exception e) { ItemHooks.Report("tooltip color tags", e); }
        }
        var call = GameCall.Returning(RetKind.H2F);
        call.Arg(self);
        call.Arg(measured);
        call.Arg(method);
        long raw = ItemHooks.Original(ref call, hook, "SpriteFont.MeasureString");
        float* f = (float*)&raw;
        return new Vector2(f[0], f[1]);
    }

    static bool PreDrawTooltip(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)
    {
        bool go = true;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { go &= m.PreDrawTooltip(lines, ref x, ref y); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PreDrawTooltip", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { go &= g.PreDrawTooltip(item, lines, ref x, ref y); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PreDrawTooltip", e); }
        }
        return go;
    }

    static void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.PostDrawTooltip(lines); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PostDrawTooltip", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.PostDrawTooltip(item, lines); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PostDrawTooltip", e); }
        }
    }

    static bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
    {
        bool go = true;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { go &= m.PreDrawTooltipLine(line, ref yOffset); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PreDrawTooltipLine", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { go &= g.PreDrawTooltipLine(item, line, ref yOffset); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PreDrawTooltipLine", e); }
        }
        return go;
    }

    static void PostDrawTooltipLine(Item item, DrawableTooltipLine line)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.PostDrawTooltipLine(line); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PostDrawTooltipLine", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.PostDrawTooltipLine(item, line); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PostDrawTooltipLine", e); }
        }
    }
}
