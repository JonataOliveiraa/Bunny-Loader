using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static unsafe class ServerSyncMenu
{
    sealed class Line
    {
        internal required string Kind, Text;

        internal string Tag = "";

        internal Color Color;

        internal float Height;
    }

    const float Gap = 4, TextPad = 16, TagPad = 18, FooterGap = 6, DragPx = 8, Friction = 0.92f;
    const int PollFrames = 6, ScreenAnchor = 0, TopLeft = 9, Middle = 16, CenterX = 2, Right = 4, Top = 8, Escape = 27, MenuOpenSound = 10, MenuCloseSound = 11, TickSound = 12;

    static readonly List<GameHandle> kept = new();
    static readonly GameClass ObjectClass = new("System", "Object");
    static MethodRef cloneMethod;
    static readonly int[] cloneLayout = { -1 };
    static readonly Dictionary<string, string> names = new();
    static readonly List<float> rowScales = new();

    static bool active, dragged, pressIn, guard;
    static JsonObject state = new() { ["phase"] = "checking" };
    static int poll, guardIdle;
    static float scroll, momentum, titleScale = 1, leftScale = 1, rightScale = 1;
    static float? pressY, lastY;
    static String_Layout textPart, tagPart;

    internal static void Install() => ItemHooks.Once("ServerSyncMenu", () => On_GUINetPlayStatusMenu.Draw += Draw);

    internal static void Open()
    {
        active = true;
        state = new JsonObject { ["phase"] = "checking" };
        poll = 0;
        scroll = 0;
        momentum = 0;
        guard = true;
        guardIdle = 0;
        SoundEngine.PlaySound(MenuOpenSound);
    }

    static void Report(string what, Exception e) => ItemHooks.Report("server sync menu: " + what, e);

    static nint Clone(nint obj)
    {
        ref readonly var m = ref GameInterop.Method(ref cloneMethod, ObjectClass, "object MemberwiseClone()", cloneLayout);
        CallFrame f;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out f);
        f.X[0] = obj;
        f.X[1] = (long)m.Info;
        f.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &f);
        nint copy = (nint)(long)f.Out[0];
        if (copy != 0)
            kept.Add(new GameHandle(copy));
        return copy;
    }

    static string Text(string key) => Language.GetTextValue("BunnyLoader." + key);

    static Color TagColor(string kind) => kind switch
    {
        "download" => new Color(255, 210, 90, 255),
        "enable" or "done" => new Color(120, 225, 120, 255),
        "disable" => new Color(235, 130, 130, 255),
        "missing" => new Color(255, 95, 95, 255),
        "waiting" => new Color(165, 165, 180, 255),
        _ => new Color(255, 255, 255, 255)
    };

    static void Close(bool sound = true)
    {
        if (!active)
            return;
        active = false;
        state = new JsonObject { ["phase"] = "idle" };
        ServerSyncHooks.Call("cancel");
        if (sound)
            SoundEngine.PlaySound(MenuCloseSound);
    }

    static void Draw(On_GUINetPlayStatusMenu.orig_Draw orig, GUINetPlayStatusMenu self)
    {
        if (!active)
        {
            orig(self);
            return;
        }

        try { DrawScreen(); }
        catch (Exception e)
        {
            Report("draw", e);
            Close(false);
        }
    }

    static string Phase => state["phase"]?.ToString() ?? "checking";

    static List<string> Names(string key) => (state[key] as JsonArray)?.Select(n => n?.ToString() ?? "").ToList() ?? new List<string>();

    static void DrawScreen()
    {
        SettingsOverlay_Layout s = SettingsOverlay_Layout.Instance;
        if (--poll <= 0)
        {
            poll = PollFrames;
            string? text = ServerSyncHooks.Call("state");
            if (!string.IsNullOrEmpty(text) && JsonNode.Parse(text) is JsonObject parsed)
                state = parsed;
        }

        if (guard)
        {
            guardIdle = Main.mouseLeft ? 0 : guardIdle + 1;
            if (guardIdle >= 2)
                guard = false;
        }

        GUIPanel.Draw(s.Backing, false, default, default, default);
        GUIPanel.Draw(s.MenuDivider2, false, default, default, default);
        GUIStringButton.DrawButton(s.Title, Text("ServerModsTitle"), ref titleScale, false, true);
        (List<Line> lines, string? footer) = Lines();
        DrawList(s, lines, footer);
        string? action = DrawButtons();
        if (action != null && !guard && !dragged)
        {
            XNAUIInputLayer.Instance.CaptureUICrusorDrag(-1);
            if (action == "sync")
            {
                SoundEngine.PlaySound(TickSound);
                ServerSyncHooks.Call("apply");
                poll = 0;
            }
            else
            {
                Close();
                return;
            }
        }

        if (KeyboardInput.GetKeyUp(Escape) && Phase != "applying")
        {
            GUIInstance gi = GUIInstance.Active;
            if (!gi.IsNull && !gi.GUIKeyboardMappings.IsNull)
                gi.GUIKeyboardMappings.DisableEscapeKeyUsage();
            Close();
        }
    }

    static bool Focus()
    {
        GUIInstance gi = GUIInstance.Active;
        if (gi.IsNull)
            return false;
        var pad = gi.GUIVirtualInputController;
        return !pad.IsNull && pad.ControllerActive;
    }

    static string? DrawButtons()
    {
        KeyboardMappings_Layout k = KeyboardMappings_Layout.Instance;
        string phase = Phase;
        bool applying = phase == "applying", canSync = phase == "ready";
        string left = Text(canSync || phase == "checking" || applying ? "Cancel" : "Back");
        string? clicked = null;
        if (GUITransactionButton.Draw(k.ResetSettings, default(Item), left, applying, ref leftScale, false, Focus(), false, false) == GUITransactionButton.InputState.Clicked && !applying)
            clicked = "cancel";
        if (canSync || phase == "checking" || applying)
            if (GUITransactionButton.Draw(k.Close, default(Item), Text("ServerModsSync"), !canSync, ref rightScale, false, Focus(), false, false) == GUITransactionButton.InputState.Clicked && canSync)
                clicked = "sync";
        return clicked;
    }

    static (List<Line> Lines, string? Footer) Lines()
    {
        Line Para(string key) => new() { Kind = "text", Text = Text(key) };
        IEnumerable<Line> Items(string key, string tag, string color) =>
            Names(key).Select(name => new Line { Kind = "item", Text = name, Tag = Text(tag), Color = TagColor(color) });
        switch (Phase)
        {
            case "ready":
            {
                var changes = Items("download", "ServerModsDownload", "download").Concat(Items("enable", "ServerModsEnable", "enable")).Concat(Items("disable", "ServerModsDisable", "disable")).ToList();
                var list = new List<Line> { Para(changes.Count > 0 ? "ServerModsIntro" : "ServerModsReorder") };
                list.AddRange(changes);
                return (list, Text("ServerModsAfter"));
            }
            case "missing":
            {
                var list = new List<Line> { Para("ServerModsMissing") };
                list.AddRange(Items("missing", "ServerModsMissingTag", "missing"));
                return (list, null);
            }
            case "applying":
            {
                List<string> downloads = Names("download");
                int step = state["step"]?.GetValue<int>() ?? 0, steps = state["steps"]?.GetValue<int>() ?? 0;
                float progress = state["progress"]?.GetValue<float>() ?? 0;
                bool restarting = steps == 0 || step > steps;
                var list = new List<Line> { Para(restarting ? "ServerModsRestarting" : "ServerModsDownloading") };
                for (int i = 0; i < downloads.Count; i++)
                {
                    bool done = restarting || i + 1 < step, busy = i + 1 == step;
                    list.Add(new Line
                    {
                        Kind = "item",
                        Text = downloads[i],
                        Tag = done ? Text("ServerModsDone") : busy ? $"{MathF.Round(progress * 100)}%" : Text("ServerModsQueued"),
                        Color = TagColor(done ? "done" : busy ? "busy" : "waiting")
                    });
                }

                return (list, null);
            }
            case "error":
                return (new List<Line> { new() { Kind = "text", Text = state["message"]?.ToString() ?? "" } }, null);
            default:
                return (new List<Line> { Para("ServerModsChecking") }, null);
        }
    }

    static void Parts(SettingsOverlay_Layout s)
    {
        if (!textPart.IsNull)
            return;
        textPart = new String_Layout(Clone(s.ToggleTemplate.Option1Label.Pointer));
        textPart.AnchorControl = (ControlAnchor.ControlId)ScreenAnchor;
        textPart.Anchor = (LayoutCalculator.AnchorType)TopLeft;
        textPart.Alignment = (LayoutCalculator.AnchorType)(Top | CenterX);
        tagPart = new String_Layout(Clone(s.SliderTemplate.Value.Pointer));
        tagPart.AnchorControl = (ControlAnchor.ControlId)ScreenAnchor;
        tagPart.Anchor = (LayoutCalculator.AnchorType)TopLeft;
        tagPart.Alignment = (LayoutCalculator.AnchorType)(Middle | Right);
    }

    static Rectangle Rect(float x, float y, float width, float height) =>
        new((int)MathF.Round(x), (int)MathF.Round(y), (int)MathF.Round(width), (int)MathF.Round(height));

    static Vector2 At(TransactionButton_Layout l) => LayoutCalculator.GetAnchoredPosition(l.AnchorControl, l.Anchor, l.Location);

    static List<string> Wrap(String_Layout label, string text, float width)
    {
        SpriteFont font = label.GetFont();
        if (font.IsNull)
            return new List<string> { text };
        float Measure(string value) => font.MeasureString(value).X * label.Scale;
        var lines = new List<string>();
        string line = "";
        foreach (string word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string next = line.Length > 0 ? line + " " + word : word;
            if (line.Length > 0 && Measure(next) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = next;
            }
        }

        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    static void DrawList(SettingsOverlay_Layout s, List<Line> rows, string? footer)
    {
        Parts(s);
        Vector2 title = LayoutCalculator.GetAnchoredPosition(s.Title.AnchorControl, s.Title.Anchor, s.Title.Location);
        float top = title.Y + s.Title.Size.Y / 2 + 14;
        float width = Achievements_Layout.Instance.ItemBacking.Size.X;
        float left = LayoutCalculator.GetAnchoredPosition(s.Backing.AnchorControl, s.Backing.Anchor, Vector2.Zero).X - width / 2;
        float itemHeight = s.ToggleTemplate.ToggleButton.overloadedSize.Y;
        SpriteFont font = textPart.GetFont();
        float lineHeight = (font.IsNull ? 24 : font.LineSpacing) * textPart.Scale;
        float textWidth = width - TextPad * 2;
        float center = left + width / 2;
        float bottom = LayoutCalculator.GetAnchoredPosition(s.MenuDivider2.AnchorControl, s.MenuDivider2.Anchor, s.MenuDivider2.Location).Y - 8;
        if (footer != null)
        {
            List<string> footerLines = Wrap(textPart, footer, textWidth);
            bottom -= footerLines.Count * lineHeight + FooterGap;
            for (int i = 0; i < footerLines.Count; i++)
            {
                textPart.Location = new Vector2(center, bottom + FooterGap + i * lineHeight);
                GUIString.Draw(textPart, footerLines[i], new Color(175, 175, 195, 255), false);
            }
        }

        var laid = new List<Line>();
        foreach (Line row in rows)
        {
            if (row.Kind == "text")
            {
                List<string> wrapped = Wrap(textPart, row.Text, textWidth);
                for (int i = 0; i < wrapped.Count; i++)
                    laid.Add(new Line { Kind = "text", Text = wrapped[i], Height = lineHeight + (i == wrapped.Count - 1 ? Gap * 3 : 0) });
            }
            else
            {
                row.Height = itemHeight + Gap;
                laid.Add(row);
            }
        }

        float total = laid.Sum(r => r.Height);
        Rectangle area = Rect(left, top, width, bottom - top);
        Scroll(area, total, bottom - top);
        Rectangle saved = ControlAnchor._gridItemRegion;
        SpriteBatchItem.EnableClipping(area, area, Main.spriteBatch, true);
        try
        {
            float y = top - scroll;
            for (int i = 0; i < laid.Count; i++)
            {
                Line row = laid[i];
                if (y + row.Height > top && y < bottom)
                {
                    if (row.Kind == "text")
                    {
                        textPart.Location = new Vector2(center, y);
                        GUIString.Draw(textPart, row.Text, textPart.Color, false);
                    }
                    else
                    {
                        ControlAnchor._gridItemRegion = Rect(left, y, width, itemHeight);
                        DrawItem(s, row, i, left, y, width, itemHeight);
                    }
                }

                y += row.Height;
            }
        }
        finally
        {
            SpriteBatchItem.DisabledClipping();
            ControlAnchor._gridItemRegion = saved;
        }
    }

    static void DrawItem(SettingsOverlay_Layout s, Line row, int index, float x, float y, float width, float height)
    {
        while (rowScales.Count <= index)
            rowScales.Add(1);
        float scale = rowScales[index];
        TransactionButton_Layout button = s.ToggleTemplate.ToggleButton;
        SpriteFont tagFont = tagPart.GetFont();
        float tagWidth = tagFont.IsNull ? 80 : tagFont.MeasureString(row.Tag).X * tagPart.Scale;
        string name = Fit(button.Label, row.Text, width - tagWidth - TagPad * 3);
        GUITransactionButton.Draw(button, default(Item), name, false, ref scale, false, Focus(), false, true);
        rowScales[index] = scale;
        tagPart.Location = new Vector2(x + width - TagPad, y + height / 2);
        GUIString.Draw(tagPart, row.Tag, row.Color, false);
    }

    static string Fit(String_Layout label, string text, float width)
    {
        string key = text + "|" + MathF.Round(width);
        if (names.TryGetValue(key, out string? fit))
            return fit;
        fit = text;
        SpriteFont font = label.IsNull ? default : label.GetFont();
        if (!font.IsNull)
        {
            float Measure(string value) => font.MeasureString(value).X * label.Scale;
            if (Measure(text) > width)
            {
                while (fit.Length > 1 && Measure(fit + "...") > width)
                    fit = fit[..^1];
                fit += "...";
            }
        }

        names[key] = fit;
        return fit;
    }

    static void Scroll(Rectangle area, float total, float visible)
    {
        GUIInputRegionManager.Instance.RegisterInputRegion(area);
        float max = Math.Max(0, total - visible);
        bool inside = Main.mouseX >= area.X && Main.mouseX <= area.X + area.Width && Main.mouseY >= area.Y && Main.mouseY <= area.Y + area.Height;
        if (Main.mouseLeft)
        {
            if (pressY == null)
            {
                pressY = Main.mouseY;
                lastY = Main.mouseY;
                dragged = false;
                pressIn = inside && !guard;
                momentum = 0;
            }
            else if (pressIn && Math.Abs(Main.mouseY - pressY.Value) > DragPx)
            {
                dragged = true;
            }

            if (dragged)
            {
                float delta = lastY!.Value - Main.mouseY;
                scroll += delta;
                momentum = delta;
                XNAUIInputLayer.Instance.CaptureUICrusorDrag(1);
            }

            lastY = Main.mouseY;
        }
        else
        {
            pressY = null;
            lastY = null;
            dragged = false;
            if (Math.Abs(momentum) > 0.5f)
            {
                scroll += momentum;
                momentum *= Friction;
            }
            else
            {
                momentum = 0;
            }
        }

        if (scroll < 0 || scroll > max)
        {
            scroll = Math.Clamp(scroll, 0, max);
            momentum = 0;
        }
    }
}
