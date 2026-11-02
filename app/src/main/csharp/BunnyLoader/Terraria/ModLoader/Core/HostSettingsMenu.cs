using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static unsafe class HostSettingsMenu
{
    sealed class Row
    {
        internal required string Key, Description;

        internal required Func<bool> Get;

        internal required Action<bool> Set;

        internal SettingsOverlayToggle_Layout Layout;

        internal float Scale = 1;
    }

    const float DragPx = 8, Friction = 0.92f;
    const int TickSound = 12;

    static readonly List<GameHandle> kept = new();
    static readonly GameClass ObjectClass = new("System", "Object");
    static MethodRef cloneMethod;
    static readonly int[] cloneLayout = { -1 };

    static Row[]? rows;
    static float scroll, momentum;
    static float? pressY, lastY;
    static bool dragged, pressIn;

    internal static void Install() => ItemHooks.Once("HostSettingsMenu", () =>
    {
        On_GUIMultiplayerHost.Draw += Draw;
        On_GUIMultiplayerHost.DrawMaxPlayers += (orig, self) => GameRow(self, () => orig(self), MultiplayerHost_Layout.Instance.MaxPlayers.Title);
        On_GUIMultiplayerHost.DrawOpenInviteOnly += (orig, self) => GameRow(self, () => orig(self), MultiplayerHost_Layout.Instance.InviteOpenToggle.ToggleButton);
        On_GUIMultiplayerHost.DrawCrossplay += (orig, self) => GameRow(self, () => orig(self), MultiplayerHost_Layout.Instance.Crossplay.ToggleButton);
        On_GUIMultiplayerHost.DrawPassword += DrawPassword;
        On_GUIMultiplayerHost.Back += (orig, self) =>
        {
            scroll = 0;
            orig(self);
        };
        On_GUIMultiplayerHost.HostServer += (orig, self) =>
        {
            scroll = 0;
            orig(self);
        };
    });

    static void Report(string what, Exception e) => ItemHooks.Report("host settings: " + what, e);

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

    static Row[] Rows()
    {
        if (rows != null)
            return rows;
        SettingsOverlayToggle_Layout template = MultiplayerHost_Layout.Instance.Crossplay;
        Row Make(string key, string description, Func<bool> get, Action<bool> set)
        {
            var layout = new SettingsOverlayToggle_Layout(Clone(template.Pointer));
            layout.ToggleButton = new TransactionButton_Layout(Clone(template.ToggleButton.Pointer));
            layout.Option1Label = new String_Layout(Clone(template.Option1Label.Pointer));
            layout.Option2Label = new String_Layout(Clone(template.Option2Label.Pointer));
            return new Row { Key = key, Description = description, Get = get, Set = set, Layout = layout };
        }

        return rows = new[]
        {
            Make("HostSyncMods", "HostSyncModsDescription", () => ServerSyncHooks.SyncMods, v => ServerSyncHooks.SyncMods = v),
            Make("HostAllowTextures", "HostAllowTexturesDescription", () => ServerSyncHooks.AllowTextures, v => ServerSyncHooks.AllowTextures = v),
            Make("HostAllowCheats", "HostAllowCheatsDescription", () => ServerSyncHooks.AllowCheats, v => ServerSyncHooks.AllowCheats = v),
            Make("HostAllowJs", "HostAllowJsDescription", () => ServerSyncHooks.AllowJs, v => ServerSyncHooks.AllowJs = v)
        };
    }

    static float Step(MultiplayerHost_Layout l) => l.Password.Location.Y - l.Crossplay.ToggleButton.Location.Y;

    static Rectangle Rect(float x, float y, float width, float height) =>
        new((int)MathF.Round(x), (int)MathF.Round(y), (int)MathF.Round(width), (int)MathF.Round(height));

    static Rectangle Area()
    {
        MultiplayerHost_Layout l = MultiplayerHost_Layout.Instance;
        Rectangle back = GUIPanel.Region(l.Backing);
        Rectangle top = GUIPanel.Region(l.MenuDivider2);
        Rectangle bottom = GUIPanel.Region(l.MenuDivider);
        int y = top.Y + top.Height;
        return Rect(back.X, y, back.Width, bottom.Y - y);
    }

    static void Draw(On_GUIMultiplayerHost.orig_Draw orig, GUIMultiplayerHost self)
    {
        try { Scroll(); }
        catch (Exception e) { Report("scroll", e); }
        orig(self);
    }

    static void Scroll()
    {
        Rectangle area = Area();
        GUIInputRegionManager.Instance.RegisterInputRegion(area);
        float max = Rows().Length * Step(MultiplayerHost_Layout.Instance);
        bool inside = Main.mouseX >= area.X && Main.mouseX <= area.X + area.Width && Main.mouseY >= area.Y && Main.mouseY <= area.Y + area.Height;
        if (Main.mouseLeft)
        {
            if (pressY == null)
            {
                pressY = Main.mouseY;
                lastY = Main.mouseY;
                dragged = false;
                pressIn = inside;
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
            if (dragged)
                XNAUIInputLayer.Instance.CaptureUICrusorDrag(1);
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

    static void Clipped(TransactionButton_Layout layout, float y, Action draw)
    {
        Rectangle area = Area();
        float x = layout.Location.X, savedY = layout.Location.Y;
        float top = LayoutCalculator.GetAnchoredPosition(layout.AnchorControl, layout.Anchor, new Vector2(x, y)).Y;
        float height = layout.overloadedSize.Y;
        if (top + height <= area.Y || top >= area.Y + area.Height)
            return;
        bool cut = top < area.Y || top + height > area.Y + area.Height;
        if (cut && pressY != null && !pressIn)
            return;
        layout.Location = new Vector2(x, y);
        SpriteBatchItem.EnableClipping(area, area, Main.spriteBatch, true);
        try { draw(); }
        finally
        {
            SpriteBatchItem.DisabledClipping();
            layout.Location = new Vector2(x, savedY);
        }
    }

    static void GameRow(GUIMultiplayerHost self, Action orig, TransactionButton_Layout layout)
    {
        if (scroll == 0)
        {
            orig();
            return;
        }

        bool drawn = false;
        try
        {
            Clipped(layout, layout.Location.Y - scroll, () =>
            {
                drawn = true;
                orig();
            });
            drawn = true;
        }
        catch (Exception e)
        {
            Report("game row", e);
            if (!drawn)
                orig();
        }
    }

    static void DrawPassword(On_GUIMultiplayerHost.orig_DrawPassword orig, GUIMultiplayerHost self)
    {
        GameRow(self, () => orig(self), MultiplayerHost_Layout.Instance.Password);
        try { DrawRows(self); }
        catch (Exception e) { Report("rows", e); }
    }

    static void DrawRows(GUIMultiplayerHost self)
    {
        MultiplayerHost_Layout l = MultiplayerHost_Layout.Instance;
        float step = Step(l);
        float baseX = l.Password.Location.X, baseY = l.Password.Location.Y;
        Row[] list = Rows();
        for (int i = 0; i < list.Length; i++)
        {
            Row row = list[i];
            TransactionButton_Layout button = row.Layout.ToggleButton;
            button.Location = new Vector2(baseX, baseY);
            Clipped(button, baseY + step * (i + 1) - scroll, () => DrawToggle(self, row));
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

    static void DrawToggle(GUIMultiplayerHost self, Row row)
    {
        SettingsOverlayToggle_Layout t = row.Layout;
        SettingsOverlayToggle_Layout template = MultiplayerHost_Layout.Instance.Crossplay;
        var state = GUITransactionButton.Draw(t.ToggleButton, default(Item), Text(row.Key), false, ref row.Scale, false, Focus(), false, false);
        if (state == GUITransactionButton.InputState.Clicked && pressIn && !dragged)
        {
            row.Set(!row.Get());
            SoundEngine.PlaySound(TickSound);
        }

        if (state is GUITransactionButton.InputState.Clicked or GUITransactionButton.InputState.Over or GUITransactionButton.InputState.Down)
            self.description = Text(row.Description);
        bool on = row.Get();
        if (on)
        {
            GUIPanel.Draw(t.Option2Disabled, false, default, default, default);
            GUIPanel.Draw(t.Option1Enabled, false, default, default, default);
        }
        else
        {
            GUIPanel.Draw(t.Option1Disabled, false, default, default, default);
            GUIPanel.Draw(t.Option2Enabled, false, default, default, default);
        }

        Label(t.Option1Label, template.Option1Label, t.Option1Enabled.Size.X, Language.GetTextValue("Mobile.ToggleSettingOn"), on ? t.Option1Label.Color : t.Option1DisabledLabelColour);
        Label(t.Option2Label, template.Option2Label, t.Option2Enabled.Size.X, Language.GetTextValue("Mobile.ToggleSettingOff"), on ? t.Option2DisabledLabelColour : t.Option2Label.Color);
    }

    static void Label(String_Layout label, String_Layout template, float width, string text, Color color)
    {
        SpriteFont font = template.GetFont();
        float measured = font.IsNull ? 0 : font.MeasureString(text).X;
        float room = Math.Max(8, width - 8);
        label.Scale = measured > 0 ? Math.Min(template.Scale, room / measured) : template.Scale;
        GUIString.Draw(label, text, color, false);
    }
}
