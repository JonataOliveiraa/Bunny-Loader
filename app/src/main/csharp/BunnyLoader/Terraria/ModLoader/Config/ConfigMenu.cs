using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader.Config;

internal static unsafe class ConfigMenu
{
    sealed class Entry
    {
        internal required ModConfig Active;

        internal ModConfig? Draft;

        internal ModConfig View => Draft ?? Active;

        internal required ConfigOption[] Options;
    }

    sealed class Row
    {
        internal required string Kind;

        internal required Entry Entry;

        internal ConfigOption? Option;

        internal string? Header;

        internal required string Key;

        internal float Height;
    }

    sealed class RowState
    {
        internal float Scale = 1, Scale2 = 1, Value;

        internal GUISlider.DragState Drag;
    }

    sealed class Popup
    {
        internal required Row Row;

        internal Rectangle Rect;

        internal bool Down, DownInside, Armed;

        internal required float[] Scales;
    }

    sealed class Tab
    {
        internal TransactionButton_Layout Layout;

        internal float Scale = 1;
    }

    sealed class TabShape
    {
        internal float IconPx, ToMiddle, MiddleStep, ToEnd;

        internal TransactionButton_Layout Start, Middle, End;
    }

    sealed class Parts
    {
        internal String_Layout Empty, Header, Label;

        internal Panel_Layout Divider, ChoiceOn, ChoiceOff, Swatch, Greyout, PopBacking;

        internal String_Layout Cycle;

        internal TransactionButton_Layout Option;
    }

    const float Gap = 4, HeaderExtra = 8, SwatchInset = 4, CycleX = 24, PopupMargin = 8, DragPx = 8, Friction = 0.92f, IconScale = 1.1f;
    const int ScreenAnchor = 0, TopLeft = 9, Center = 18, Middle = 16, MenuOpenSound = 10, MenuCloseSound = 11, TickSound = 12, SettingNone = 192, General = 0;
    const string IconId = "bunny/coelho_cabeca_cs", JsIconId = "bunny/coelho_cabeca";

    static readonly List<GameHandle> kept = new();
    static readonly Dictionary<string, RowState> rowState = new();
    static readonly Dictionary<string, (Color Color, float[] Hsl)> colorState = new();
    static readonly Dictionary<string, Tab> tabs = new();
    static readonly Dictionary<Mod, Texture2D> icons = new();
    static readonly List<Entry> entries = new();
    static readonly GameClass ObjectClass = new("System", "Object");
    static MethodRef cloneMethod;
    static readonly int[] cloneLayout = { -1 };

    static bool open, installed, guard, dragged, pressInList, tabDragged, tabPressIn;
    static string? where;
    static GameHandle? menuEntry;
    static TransactionButton_Layout button;
    static Parts? parts;
    static TabShape? tabShape;
    static Mod? selected;
    static float scroll, momentum, tabScroll, tabMomentum, buttonScale = 1, titleScale = 1, discardScale = 1, defaultsScale = 1, applyScale = 1;
    static float? lastY, pressY, tabPressX, tabLastX;
    static string? slider;
    static int guardIdle, pauseDrawing;
    static Popup? popup;
    static (string Culture, string Text)? back;

    static nint Keep(nint pointer)
    {
        if (pointer != 0)
            kept.Add(new GameHandle(pointer));
        return pointer;
    }

    static nint Clone(nint obj)
    {
        ref readonly var m = ref GameInterop.Method(ref cloneMethod, ObjectClass, "object MemberwiseClone()", cloneLayout);
        CallFrame f;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out f);
        f.X[0] = obj;
        f.X[1] = (long)m.Info;
        f.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &f);
        return Keep((nint)(long)f.Out[0]);
    }

    internal static void Install()
    {
        if (installed)
            return;
        installed = true;
        On_GUISettingsPauseMenu.SetupOffsets += SetupOffsets;
        On_GUISettingsPauseMenu.Draw += PauseDraw;
        On_GUIPanel.Draw += PanelDraw;
        On_WorldGen.clearWorld += ClearWorld;
        On_GUISettingsOverlay.SetCategory += SetCategory;
        On_GUISettingsOverlay.Draw += OverlayDraw;
        On_GUISettingsOverlay.Close += OverlayClose;
        On_GUIOpenUIButtonSetting.GetOption += GetOption;
        On_GUIOpenUIButtonSetting.GetButtonCaption += GetButtonCaption;
        On_GUIOpenUIButtonSetting.OpenUI += OpenUI;
    }

    static void Report(string what, Exception e) => ItemHooks.Report("ModConfig menu: " + what, e);

    static void SetupOffsets(On_GUISettingsPauseMenu.orig_SetupOffsets orig, GUISettingsPauseMenu self, bool setup)
    {
        orig(self, setup);
        if (!setup)
            return;
        try { Layout(self); }
        catch (Exception e) { Report("layout", e); }
    }

    static void PauseDraw(On_GUISettingsPauseMenu.orig_Draw orig, GUISettingsPauseMenu self)
    {
        if (where != "pause")
        {
            pauseDrawing++;
            try { orig(self); }
            finally { pauseDrawing--; }
            return;
        }

        try { DrawScreen(); }
        catch (Exception e)
        {
            Report("draw", e);
            Close(false);
        }
    }

    static void PanelDraw(On_GUIPanel.orig_Draw orig, Panel_Layout layout, bool cursorOver, GameNullable<Color> backing, GameNullable<Color> border, GameNullable<Color> highlight)
    {
        orig(layout, cursorOver, backing, border, highlight);
        if (pauseDrawing == 0)
            return;
        SettingsPauseMenu_Layout l = SettingsPauseMenu_Layout.Instance;
        if (l.IsNull || layout != l.Backing)
            return;
        try { DrawButton(l); }
        catch (Exception e) { Report("button", e); }
    }

    static void ClearWorld(On_WorldGen.orig_clearWorld orig)
    {
        orig();
        Close(false);
    }

    static void SetCategory(On_GUISettingsOverlay.orig_SetCategory orig, GUISettingsOverlay self, SettingsOverlayOptionCategories_Layout.Category category)
    {
        orig(self, category);
        try { AddMenuEntry(self); }
        catch (Exception e) { Report("main menu row", e); }
    }

    static void OverlayDraw(On_GUISettingsOverlay.orig_Draw orig, GUISettingsOverlay self)
    {
        if (where != "menu")
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

    static void OverlayClose(On_GUISettingsOverlay.orig_Close orig, GUISettingsOverlay self)
    {
        if (where == "menu")
            Close(false);
        orig(self);
    }

    static bool Ours(GUIOpenUIButtonSetting self) => menuEntry != null && self.Pointer == menuEntry.Target;

    static string? GetOption(On_GUIOpenUIButtonSetting.orig_GetOption orig, GUIOpenUIButtonSetting self) => Ours(self) ? Text("row") : orig(self);

    static string? GetButtonCaption(On_GUIOpenUIButtonSetting.orig_GetButtonCaption orig, GUIOpenUIButtonSetting self) => Ours(self) ? Text("open") : orig(self);

    static void OpenUI(On_GUIOpenUIButtonSetting.orig_OpenUI orig, GUIOpenUIButtonSetting self)
    {
        if (!Ours(self))
        {
            orig(self);
            return;
        }

        XNAUIInputLayer.Instance.CaptureUICrusorDrag(-1);
        Open("menu");
    }

    static void AddMenuEntry(GUISettingsOverlay overlay)
    {
        if (!Main.gameMenu || (int)overlay.Selected != General)
            return;
        var list = new GameList(overlay._categorySettings);
        if (list.IsNull)
            return;
        if (menuEntry == null)
        {
            GUIOpenUIButtonSetting entry = GUIOpenUIButtonSetting.New((GUISettingID)SettingNone);
            entry._category = (SettingsOverlayOptionCategories_Layout.Category)General;
            menuEntry = new GameHandle(entry.Pointer);
        }

        if (list.IndexOf(menuEntry.Target) < 0)
            list.Insert(0, menuEntry.Target);
    }

    internal static bool IsOpen => open;

    internal static void Open(string from)
    {
        SoundEngine.PlaySound(MenuOpenSound);
        open = true;
        where = from;
        Begin();
        ResetScroll();
        Guard();
    }

    static void Begin()
    {
        if (Main.gameMenu)
            ConfigNetHooks.RestoreLocal();
        entries.Clear();
        bool client = !Main.gameMenu && ModNet.NetMode == 1;
        foreach (ModConfig config in ConfigManager.All)
            if (!client || config.Mode != ConfigScope.ServerSide)
                entries.Add(new Entry { Active = config, Draft = config.Clone(), Options = ConfigManager.OptionsOf(config.GetType()) });
    }

    static IEnumerable<Entry> EntriesOf(Mod mod) => entries.Where(e => e.Active.Mod == mod);

    static bool JsPresent()
    {
        GUISettingsPauseMenu menu = GUIInstance.Active.IsNull ? default : GUIInstance.Active.GUISettingsPauseMenu;
        if (menu.IsNull || menu._controllerList.IsNull)
            return false;
        var items = menu._controllerList._items;
        for (int i = 0; i < items.Length; i++)
            if (IsJsButton(items[i]))
                return true;
        return false;
    }

    static bool IsJsButton(TransactionButton_Layout layout) => !layout.IsNull && layout.Pointer != button.Pointer && !layout.IconTexture.IsNull && layout.IconTexture.TextureId == JsIconId;

    static string Text(string key)
    {
        bool pt = (Language.ActiveCulture.Name ?? "").StartsWith("pt", StringComparison.Ordinal);
        string text = key switch
        {
            "button" => pt ? "Config. dos Mods" : "Mod Configuration",
            "row" => pt ? "Config. dos Mods:" : "Mod Configuration:",
            "open" => pt ? "Abrir" : "Open",
            "empty" => pt ? "Nenhum mod tem opções." : "No mod has options.",
            _ => key
        };
        return (key == "button" || key == "row") && JsPresent() ? text.TrimEnd(':') + " (C#)" + (key == "row" ? ":" : "") : text;
    }

    static string BackText()
    {
        string culture = Language.ActiveCulture.Name ?? "";
        if (back is { } cached && cached.Culture == culture)
            return cached.Text;
        string? text = null;
        foreach (string key in new[] { "UI.Back", "Mobile.Back", "LegacyMenu.15" })
        {
            string value = Language.GetTextValue(key);
            if (!string.IsNullOrEmpty(value) && value != key)
            {
                text = value;
                break;
            }
        }

        text ??= culture.StartsWith("pt", StringComparison.Ordinal) ? "Voltar" : "Back";
        back = (culture, text);
        return text;
    }

    static float Step(SettingsPauseMenu_Layout l) => l.Close.overloadedSize.Y + l.ButtonSpacing;

    static TransactionButton_Layout ButtonLayout(SettingsPauseMenu_Layout l)
    {
        if (!button.IsNull)
            return button;
        button = new TransactionButton_Layout(Clone(l.Settings.Pointer));
        try
        {
            Texture_Layout icon = new(Keep(Texture_Layout.New().Pointer));
            icon.TextureId = IconId;
            icon._lastTextureId = IconId;
            icon._texture = BunnyHead();
            button.IconTexture = icon;
            button.ForceIconScale = true;
            button.ForcedIconScale = IconScale;
        }
        catch (Exception e) { Report("icon", e); }
        return button;
    }

    static Texture2D BunnyHead() => Asset<Texture2D>.ForGameAsset(Host.Native()->BunnyHeadTexture(), "bunny:bunnyHead").Value;

    static void Move(TransactionButton_Layout b, float dy)
    {
        if (b.IsNull)
            return;
        Vector2 p = b.Location;
        b.Location = new Vector2(p.X, p.Y + dy);
    }

    static void Layout(GUISettingsPauseMenu self)
    {
        SettingsPauseMenu_Layout l = SettingsPauseMenu_Layout.Instance;
        float step = Step(l);
        Panel_Layout backing = l.Backing;
        backing.Size = new Vector2(backing.Size.X, backing.Size.Y + step);
        var list = self._controllerList;
        TransactionButton_Layout js = default;
        if (!list.IsNull)
        {
            var current = list._items;
            for (int i = 0; i < current.Length; i++)
                if (IsJsButton(current[i]))
                    js = current[i];
        }

        var above = new List<TransactionButton_Layout> { l.Close, l.Controls, l.Settings };
        if (self.includeMPInviteActive)
            above.Add(l.Invite);
        if (!js.IsNull)
            above.Add(js);
        var below = new List<TransactionButton_Layout> { l.Achievements, l.Bestiary, l.Home };
        if (self.creativeModeActive)
            below.Add(l.JourneySettings);
        foreach (TransactionButton_Layout b in above)
            Move(b, -step / 2);
        foreach (TransactionButton_Layout b in below)
            Move(b, step / 2);
        TransactionButton_Layout ours = ButtonLayout(l);
        Vector2 anchor = js.IsNull ? l.Settings.Location : js.Location;
        ours.Location = new Vector2(l.Settings.Location.X, anchor.Y + step);
        if (list.IsNull)
            return;
        var items = list._items;
        for (int i = 0; i < items.Length; i++)
            if (items[i].Pointer == ours.Pointer)
                return;
        var grown = GameArray<TransactionButton_Layout>.New(TransactionButton_Layout.Class, items.Length + 1);
        for (int i = 0; i < items.Length; i++)
            grown[i] = items[i];
        grown[items.Length] = ours;
        Keep(grown.Pointer);
        list._items = grown;
        list.Activate();
    }

    static void DrawButton(SettingsPauseMenu_Layout l)
    {
        if (open)
            return;
        TransactionButton_Layout ours = ButtonLayout(l);
        ours.overloadedSize = l.Settings.overloadedSize;
        ours.Location = new Vector2(l.Settings.Location.X, ours.Location.Y);
        var state = GUITransactionButton.Draw(ours, default(Item), Text("button"), false, ref buttonScale, false, Focus(), false, false);
        if (state != GUITransactionButton.InputState.Clicked)
            return;
        XNAUIInputLayer.Instance.CaptureUICrusorDrag(-1);
        Open("pause");
    }

    static bool Focus()
    {
        GUIInstance gi = GUIInstance.Active;
        if (gi.IsNull)
            return false;
        var pad = gi.GUIVirtualInputController;
        return !pad.IsNull && pad.ControllerActive;
    }

    static void Guard()
    {
        guard = true;
        guardIdle = 0;
    }

    static void Close(bool sound = true, bool apply = false)
    {
        if (!open)
            return;
        open = false;
        where = null;
        popup = null;
        ReleaseSliders();
        if (apply)
            Apply();
        entries.Clear();
        if (sound)
            SoundEngine.PlaySound(MenuCloseSound);
    }

    static bool Pending() => entries.Any(e => e.Draft != null && !ConfigManager.SameValues(e.Draft, e.Active));

    static bool AtDefaults(IEnumerable<Entry> list)
    {
        foreach (Entry entry in list)
        {
            ModConfig defaults = entry.Active.Clone();
            ConfigManager.ApplyDefaults(defaults);
            if (!ConfigManager.SameValues(entry.View, defaults))
                return false;
        }

        return true;
    }

    static void Defaults(IEnumerable<Entry> list)
    {
        foreach (Entry entry in list)
        {
            ModConfig defaults = entry.Active.Clone();
            ConfigManager.ApplyDefaults(defaults);
            foreach (ConfigOption option in entry.Options)
                if (option.Kind != ConfigOptionKind.Data && !Locked(entry, option))
                    option.Set(entry.View, option.Get(defaults));
        }
    }

    static void Apply()
    {
        foreach (Entry entry in entries)
        {
            if (entry.Draft == null || ConfigManager.SameValues(entry.Draft, entry.Active))
                continue;
            try { entry.Active.SaveChanges(entry.Draft); }
            catch (Exception e) { Report(entry.Active.FullName + ".SaveChanges", e); }
        }
    }

    static bool Locked(Entry entry, ConfigOption option)
    {
        if (Main.gameMenu)
            return false;
        return option.ReloadRequired;
    }

    internal static void Refresh(ModConfig active)
    {
        if (!open)
            return;
        foreach (Entry entry in entries)
            if (entry.Active == active)
                entry.Draft = active.Clone();
    }

    static void DrawScreen()
    {
        SettingsOverlay_Layout s = SettingsOverlay_Layout.Instance;
        if (guard)
        {
            guardIdle = Main.mouseLeft ? 0 : guardIdle + 1;
            if (guardIdle >= 2)
                guard = false;
        }

        GUIPanel.Draw(s.Backing, false, default, default, default);
        GUIPanel.Draw(s.MenuDivider, false, default, default, default);
        GUIPanel.Draw(s.MenuDivider2, false, default, default, default);
        List<Mod> mods = Host.Mods.Where(m => EntriesOf(m).Any(e => e.Options.Any(o => o.Kind != ConfigOptionKind.Data))).ToList();
        Mod? mod = mods.Contains(selected!) ? selected : mods.FirstOrDefault();
        if (mod != null)
            mod = DrawTabs(s, mods, mod);
        selected = mod;
        string title = mod != null ? (string.IsNullOrEmpty(mod.DisplayName) ? mod.Name : mod.DisplayName) : Text("button");
        GUIStringButton.DrawButton(s.Title, title, ref titleScale, false, true);
        DrawList(s, mod);
        string? action = DrawActions(mod);
        try { DrawPopup(s); }
        catch (Exception e) { Report("popup", e); }
        if (action != null && !Blocked())
        {
            XNAUIInputLayer.Instance.CaptureUICrusorDrag(-1);
            if (action == "defaults")
            {
                Defaults(EntriesOf(mod!));
                SoundEngine.PlaySound(TickSound);
            }
            else if (action == "apply")
            {
                Apply();
                Begin();
                SoundEngine.PlaySound(TickSound);
            }
            else
            {
                Close(true);
                return;
            }
        }

        if (KeyboardInput.GetKeyUp(27))
        {
            GUIInstance gi = GUIInstance.Active;
            if (!gi.IsNull && !gi.GUIKeyboardMappings.IsNull)
                gi.GUIKeyboardMappings.DisableEscapeKeyUsage();
            if (popup != null)
                popup = null;
            else
                Close();
        }
    }

    static string? DrawActions(Mod? mod)
    {
        KeyboardMappings_Layout k = KeyboardMappings_Layout.Instance;
        List<Entry> modEntries = mod != null ? EntriesOf(mod).ToList() : new List<Entry>();
        bool pending = Pending();
        string? clicked = null;
        if (DrawAction(k.ResetSettings, pending ? Language.GetTextValue("Mobile.Discard") : BackText(), false, ref discardScale))
            clicked = "discard";
        bool noDefaults = modEntries.Count == 0 || AtDefaults(modEntries);
        if (DrawAction(k.Defaults, Language.GetTextValue("Mobile.Default"), noDefaults, ref defaultsScale) && !noDefaults)
            clicked = "defaults";
        if (DrawAction(k.Close, Language.GetTextValue("Mobile.ApplyChanges"), !pending, ref applyScale) && pending)
            clicked = "apply";
        return clicked;
    }

    static bool DrawAction(TransactionButton_Layout layout, string text, bool disabled, ref float scale) =>
        GUITransactionButton.Draw(layout, default(Item), text, disabled, ref scale, false, Focus(), false, false) == GUITransactionButton.InputState.Clicked;

    static Vector2 Anchored(ControlAnchor.ControlId control, LayoutCalculator.AnchorType anchor, Vector2 position) =>
        LayoutCalculator.GetAnchoredPosition(control, anchor, position);

    static Rectangle Rect(float x, float y, float width, float height) =>
        new((int)MathF.Round(x), (int)MathF.Round(y), (int)MathF.Round(width), (int)MathF.Round(height));

    static Mod DrawTabs(SettingsOverlay_Layout s, List<Mod> mods, Mod chosenMod)
    {
        var c = s.Categories;
        GUIPanel.Draw(c.Backing, false, default, default, default);
        TabShape shape = tabShape ??= MakeTabShape(c);
        int n = mods.Count;
        if (n == 1)
        {
            Tab only = TabOf(shape.Middle, mods[0], "middle", shape.IconPx);
            only.Layout.Location = new Vector2(shape.Start.Location.X, shape.Start.Location.Y);
            GUITransactionButton.Draw(only.Layout, default(Item), ModName(mods[0]), false, ref only.Scale, true, Focus(), false, false);
            return mods[0];
        }

        var xs = new List<(string Kind, float X)>();
        float x = shape.Start.Location.X;
        for (int i = 0; i < n; i++)
        {
            string kind = i == 0 ? "start" : i == n - 1 ? "end" : "middle";
            if (i > 0)
                x += kind == "end" ? shape.ToEnd : i == 1 ? shape.ToMiddle : shape.MiddleStep;
            xs.Add((kind, x));
        }

        TransactionButton_Layout start = shape.Start;
        Vector2 At(TransactionButton_Layout layout, float lx) => Anchored(layout.AnchorControl, layout.Anchor, new Vector2(lx, layout.Location.Y));
        Vector2 origin = At(start, start.Location.X);
        float perUnit = (At(start, start.Location.X + 100).X - origin.X) / 100;
        if (perUnit == 0)
            perUnit = 1;
        (float listLeft, float listWidth) = ListBounds(s);
        float left = origin.X, right = listLeft + listWidth;
        var last = xs[n - 1];
        TransactionButton_Layout lastShape = last.Kind == "end" ? shape.End : last.Kind == "start" ? shape.Start : shape.Middle;
        float contentRight = origin.X + (last.X - start.Location.X) * perUnit + lastShape.overloadedSize.X;
        float height = start.overloadedSize.Y;
        Rectangle area = Rect(left, origin.Y, right - left, height);
        float max = Math.Max(0, contentRight - right);
        TabDrag(area, max);
        Mod chosen = chosenMod;
        bool clip = max > 0;
        Rectangle band = Rect(left, origin.Y - height, right - left, height * 3);
        if (clip)
            SpriteBatchItem.EnableClipping(band, band, Main.spriteBatch, false);
        try
        {
            for (int i = 0; i < n; i++)
            {
                Mod mod = mods[i];
                var (kind, tx) = xs[i];
                TransactionButton_Layout template = kind == "end" ? shape.End : kind == "start" ? shape.Start : shape.Middle;
                Tab tab = TabOf(template, mod, kind, shape.IconPx);
                tab.Layout.Location = new Vector2(tx - tabScroll / perUnit, start.Location.Y);
                bool on = mod == chosenMod;
                var state = GUITransactionButton.Draw(tab.Layout, default(Item), ModName(mod), false, ref tab.Scale, on, Focus(), false, false);
                if (state == GUITransactionButton.InputState.Clicked && tabPressIn && !tabDragged && !Blocked() && !on)
                {
                    chosen = mod;
                    ResetScroll();
                    SoundEngine.PlaySound(TickSound);
                }
            }
        }
        finally
        {
            if (clip)
                SpriteBatchItem.DisabledClipping();
        }

        if (!Main.mouseLeft)
        {
            tabDragged = false;
            tabPressIn = false;
        }

        return chosen;
    }

    static string ModName(Mod mod) => string.IsNullOrEmpty(mod.DisplayName) ? mod.Name : mod.DisplayName;

    static void TabDrag(Rectangle area, float max)
    {
        GUIInputRegionManager.Instance.RegisterInputRegion(area);
        bool inside = Main.mouseX >= area.X && Main.mouseX <= area.X + area.Width && Main.mouseY >= area.Y && Main.mouseY <= area.Y + area.Height;
        if (Main.mouseLeft)
        {
            if (tabPressX == null)
            {
                tabPressX = Main.mouseX;
                tabLastX = Main.mouseX;
                tabDragged = false;
                tabPressIn = inside && !guard && popup == null;
                tabMomentum = 0;
            }
            else if (max > 0 && tabPressIn && Math.Abs(Main.mouseX - tabPressX.Value) > DragPx)
            {
                tabDragged = true;
            }

            if (tabDragged)
            {
                float delta = tabLastX!.Value - Main.mouseX;
                tabScroll += delta;
                tabMomentum = delta;
            }

            tabLastX = Main.mouseX;
        }
        else
        {
            tabPressX = null;
            tabLastX = null;
            if (Math.Abs(tabMomentum) > 0.5f)
            {
                tabScroll += tabMomentum;
                tabMomentum *= Friction;
            }
            else
            {
                tabMomentum = 0;
            }
        }

        if (tabScroll < 0 || tabScroll > max)
        {
            tabScroll = Math.Clamp(tabScroll, 0, max);
            tabMomentum = 0;
        }
    }

    static TabShape MakeTabShape(SettingsOverlayCategories_Layout c)
    {
        TransactionButton_Layout general = c.General;
        var row = new List<TransactionButton_Layout>();
        foreach (TransactionButton_Layout layout in new[] { c.General, c.Cursor, c.Video, c.Language, c.Interface, c.Info, c.Gameplay, c.Sound, c.Multiplayer, c.GraphicalQuality, c.KeyboardMouse, c.GameplayControls })
        {
            if (layout.IsNull)
                continue;
            Vector2 p = layout.Location;
            if (Math.Abs(p.Y - general.Location.Y) >= 1 || p.X < general.Location.X - 1)
                continue;
            if (row.Any(r => Math.Abs(r.Location.X - p.X) < 1))
                continue;
            row.Add(layout);
        }

        row.Sort((a, b) => a.Location.X.CompareTo(b.Location.X));
        float X(int i) => row[i].Location.X;
        float step = row.Count > 1 ? X(1) - X(0) : general.overloadedSize.X + 4;
        int lastIndex = 0;
        while (lastIndex + 1 < row.Count && Math.Abs(X(lastIndex + 1) - X(lastIndex) - step) < 4)
            lastIndex++;
        var group = row.Take(lastIndex + 1).ToList();
        int k = group.Count;
        float G(int i) => group[i].Location.X;
        Texture2D theirs = general.IconTexture.IsNull ? default : general.IconTexture.Texture;
        float iconPx = (theirs.IsNull ? 32 : theirs.Height) * (general.ForceIconScale ? general.ForcedIconScale : 1);
        return new TabShape
        {
            IconPx = iconPx,
            Start = k > 0 ? group[0] : general,
            Middle = k > 2 ? group[1] : k > 0 ? group[0] : general,
            End = k > 0 ? group[k - 1] : general,
            ToMiddle = k > 1 ? G(1) - G(0) : step,
            MiddleStep = k > 2 ? G(2) - G(1) : step,
            ToEnd = k > 1 ? G(k - 1) - G(k - 2) : step
        };
    }

    static Tab TabOf(TransactionButton_Layout template, Mod mod, string kind, float iconPx)
    {
        string key = mod.Name + ":" + kind;
        if (tabs.TryGetValue(key, out Tab? tab))
            return tab;
        tab = new Tab { Layout = new TransactionButton_Layout(Clone(template.Pointer)) };
        try
        {
            Texture2D texture = ModIcon(mod);
            Texture_Layout icon = new(Keep(Texture_Layout.New().Pointer));
            icon.TextureId = "bunny/csmod/" + mod.Name;
            icon._lastTextureId = icon.TextureId;
            icon._texture = texture;
            tab.Layout.IconTexture = icon;
            tab.Layout.ForceIconScale = true;
            tab.Layout.ForcedIconScale = iconPx / Math.Max(1, texture.Height);
        }
        catch (Exception e) { Report("icon of " + mod.Name, e); }
        tabs[key] = tab;
        return tab;
    }

    static Texture2D ModIcon(Mod mod)
    {
        if (icons.TryGetValue(mod, out Texture2D texture))
            return texture;
        string? file = new[] { "icon_small.png", "icon.png" }.Select(n => Path.Combine(mod.Folder, n)).FirstOrDefault(File.Exists);
        texture = file != null ? new Asset<Texture2D>("bunny/csmod/" + mod.Name, file).Value : BunnyHead();
        icons[mod] = texture;
        return texture;
    }

    static Parts PartsOf(SettingsOverlay_Layout s)
    {
        if (parts != null)
            return parts;
        String_Layout CopyString(String_Layout x) => new(Clone(x.Pointer));
        Panel_Layout CopyPanel(Panel_Layout x) => new(Clone(x.Pointer));
        String_Layout ToScreen(String_Layout x)
        {
            x.AnchorControl = (ControlAnchor.ControlId)ScreenAnchor;
            x.Anchor = (LayoutCalculator.AnchorType)TopLeft;
            return x;
        }

        Panel_Layout PanelToScreen(Panel_Layout x)
        {
            x.AnchorControl = (ControlAnchor.ControlId)ScreenAnchor;
            x.Anchor = (LayoutCalculator.AnchorType)TopLeft;
            return x;
        }

        var t = s.ToggleTemplate;
        var p = s.PulldownTemplate;
        String_Layout empty = ToScreen(CopyString(t.Option1Label));
        empty.Alignment = (LayoutCalculator.AnchorType)0;
        String_Layout header = ToScreen(CopyString(p.PulldownTitle));
        var option = new TransactionButton_Layout(Clone(p.Option1.Pointer));
        option.AnchorControl = p.PulldownButton.AnchorControl;
        option.Anchor = p.PulldownButton.Anchor;
        parts = new Parts
        {
            Empty = empty,
            Header = header,
            Divider = PanelToScreen(CopyPanel(s.MenuDivider)),
            ChoiceOn = CopyPanel(t.Option1Enabled),
            ChoiceOff = CopyPanel(t.Option1Disabled),
            Swatch = CopyPanel(t.Option1Enabled),
            Cycle = CopyString(s.SliderTemplate.Value),
            Label = CopyString(t.Option1Label),
            Greyout = PanelToScreen(CopyPanel(p.PulldownGreyout)),
            PopBacking = PanelToScreen(CopyPanel(p.PulldownBacking)),
            Option = option
        };
        SetAlignment(empty, TopLeft);
        SetAlignment(header, Center);
        return parts;
    }

    static void SetAlignment(String_Layout layout, int value) => layout.Alignment = (LayoutCalculator.AnchorType)value;

    static List<Row> Rows(SettingsOverlay_Layout s, Mod? mod)
    {
        var rows = new List<Row>();
        if (mod == null)
            return rows;
        float height = s.ToggleTemplate.ToggleButton.overloadedSize.Y + Gap;
        List<Entry> modEntries = EntriesOf(mod).Where(e => e.Options.Any(o => o.Kind != ConfigOptionKind.Data)).ToList();
        foreach (Entry entry in modEntries)
        {
            if (modEntries.Count > 1 || entry.Active.Mode == ConfigScope.ServerSide)
            {
                string scope = Language.GetTextValue(entry.Active.Mode == ConfigScope.ServerSide ? "tModLoader.ModConfigServerSide" : "tModLoader.ModConfigClientSide");
                rows.Add(new Row { Kind = "header", Entry = entry, Header = $"{entry.Active.DisplayName.Value} ({scope})", Key = entry.Active.FullName + ":title", Height = height + HeaderExtra });
            }
            foreach (ConfigOption option in entry.Options)
            {
                if (option.Kind == ConfigOptionKind.Data)
                    continue;
                string key = entry.Active.FullName + "." + option.Name;
                if (option.HeaderText(entry.Active) is { } header)
                    rows.Add(new Row { Kind = "header", Entry = entry, Header = header, Key = key + ":header", Height = height + HeaderExtra });
                if (option.Kind == ConfigOptionKind.Color)
                {
                    rows.Add(new Row { Kind = "color", Entry = entry, Option = option, Key = key + ":color", Height = height });
                    rows.Add(new Row { Kind = "hue", Entry = entry, Option = option, Key = key + ":hue", Height = height });
                    if (option.ShowSaturationAndLightness)
                    {
                        rows.Add(new Row { Kind = "saturation", Entry = entry, Option = option, Key = key + ":saturation", Height = height });
                        rows.Add(new Row { Kind = "lightness", Entry = entry, Option = option, Key = key + ":lightness", Height = height });
                    }
                }
                else
                {
                    string kind = option.Kind switch
                    {
                        ConfigOptionKind.Toggle => "toggle",
                        ConfigOptionKind.Range => "range",
                        _ when option.Cycle || option.Choices.Length > 12 => "cycle",
                        _ when option.Dropdown => "dropdown",
                        _ => option.Choices.Length <= 3 ? "radio" : "dropdown"
                    };
                    rows.Add(new Row { Kind = kind, Entry = entry, Option = option, Key = key, Height = height });
                }
            }
        }

        return rows;
    }

    static (float Left, float Width) ListBounds(SettingsOverlay_Layout s)
    {
        float width = Achievements_Layout.Instance.ItemBacking.Size.X;
        float left = Anchored(s.Backing.AnchorControl, s.Backing.Anchor, Vector2.Zero).X - width / 2;
        return (left, width);
    }

    static void DrawList(SettingsOverlay_Layout s, Mod? mod)
    {
        Parts p = PartsOf(s);
        float top = Anchored(s.MenuDivider.AnchorControl, s.MenuDivider.Anchor, s.MenuDivider.Location).Y + 8;
        float bottom = Anchored(s.MenuDivider2.AnchorControl, s.MenuDivider2.Anchor, s.MenuDivider2.Location).Y - 6;
        (float left, float width) = ListBounds(s);
        List<Row> rows = Rows(s, mod);
        if (rows.Count == 0)
        {
            p.Empty.Location = new Vector2(left + 12, top + 10);
            GUIString.Draw(p.Empty, Text("empty"), p.Empty.Color, false);
            return;
        }

        float total = rows.Sum(r => r.Height) - Gap;
        Rectangle area = Rect(left, top, width, bottom - top);
        Scroll(area, total, left, top, width, bottom);
        Rectangle saved = ControlAnchor._gridItemRegion;
        SpriteBatchItem.EnableClipping(area, area, Main.spriteBatch, true);
        try
        {
            float y = top - scroll;
            foreach (Row row in rows)
            {
                float h = row.Height - Gap;
                if (y + h > top && y < bottom)
                {
                    ControlAnchor._gridItemRegion = Rect(left, y, width, h);
                    try { DrawOption(s, p, row); }
                    catch (Exception e) { Report(row.Key, e); }
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

    static bool Blocked() => guard || dragged || popup != null;

    static bool RowBlocked() => Blocked() || !pressInList;

    static void ResetScroll()
    {
        scroll = 0;
        momentum = 0;
    }

    static void ReleaseSliders()
    {
        slider = null;
        foreach (RowState st in rowState.Values)
            if (!st.Drag.IsNull)
                st.Drag.wasDragging = false;
    }

    static void Scroll(Rectangle area, float total, float left, float top, float width, float bottom)
    {
        GUIInputRegionManager.Instance.RegisterInputRegion(area);
        float max = Math.Max(0, total - (bottom - top));
        bool inside = Main.mouseX >= left && Main.mouseX <= left + width && Main.mouseY >= top && Main.mouseY <= bottom;
        if (Main.mouseLeft)
        {
            if (pressY == null)
            {
                pressY = Main.mouseY;
                lastY = Main.mouseY;
                dragged = false;
                pressInList = inside && !guard && popup == null;
                momentum = 0;
            }
            else if (slider == null && pressInList && Math.Abs(Main.mouseY - pressY.Value) > DragPx)
            {
                dragged = true;
            }

            if (dragged)
            {
                float delta = lastY!.Value - Main.mouseY;
                scroll += delta;
                momentum = delta;
            }

            lastY = Main.mouseY;
        }
        else
        {
            pressY = null;
            lastY = null;
            slider = null;
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

    static void DrawLabel(Parts p, String_Layout template, float x, float width, string text, Color color, bool centered = false)
    {
        String_Layout label = p.Label;
        label.AnchorControl = template.AnchorControl;
        label.Anchor = template.Anchor;
        int alignment = (int)template.Alignment;
        label.Alignment = (LayoutCalculator.AnchorType)(centered ? (alignment & ~7) | 2 : alignment);
        SpriteFont font = template.GetFont();
        float measured = font.IsNull ? 0 : font.MeasureString(text).X;
        float room = Math.Max(8, width - 8);
        label.Scale = measured > 0 ? Math.Min(template.Scale, room / measured) : template.Scale;
        label.Location = new Vector2(x, template.Location.Y);
        GUIString.Draw(label, text, color, false);
    }

    static RowState StateOf(Row row)
    {
        if (!rowState.TryGetValue(row.Key, out RowState? st))
            rowState[row.Key] = st = new RowState();
        return st;
    }

    static (float Y, float Each, float Height, Func<float, float> XOf) Segments(SettingsOverlayToggle_Layout t, int n)
    {
        Panel_Layout a = t.Option1Enabled, b = t.Option2Enabled;
        float y = a.Location.Y;
        Vector2 AnchorPos(float x) => Anchored(a.AnchorControl, a.Anchor, new Vector2(x, y));
        float dir = MathF.Sign((AnchorPos(a.Location.X).X - AnchorPos(b.Location.X).X) / (a.Location.X - b.Location.X));
        if (dir == 0 || float.IsNaN(dir))
            dir = 1;
        float ua = a.Location.X * dir, ub = b.Location.X * dir;
        float right = Math.Max(ua + a.Size.X / 2, ub + b.Size.X / 2);
        float span = right - Math.Min(ua - a.Size.X / 2, ub - b.Size.X / 2);
        float left = right - span * Math.Max(1, n / 2f);
        float each = (right - left) / n;
        return (y, each, a.Size.Y, i => (left + each * (i + 0.5f)) * dir);
    }

    static Vector2 SegmentCenter(SettingsOverlayToggle_Layout t, float x) => Anchored(t.Option1Enabled.AnchorControl, t.Option1Enabled.Anchor, new Vector2(x, t.Option1Enabled.Location.Y));

    static void DrawOption(SettingsOverlay_Layout s, Parts p, Row row)
    {
        bool enabled = row.Option == null || !Locked(row.Entry, row.Option);
        switch (row.Kind)
        {
            case "header": DrawHeader(p, row); break;
            case "toggle": DrawToggle(s, p, row, enabled); break;
            case "range": DrawRange(s, row, enabled); break;
            case "radio": DrawRadio(s, p, row, enabled); break;
            case "dropdown": DrawDropdown(s, row, enabled); break;
            case "cycle": DrawCycle(s, p, row, enabled); break;
            case "color": DrawColor(s, p, row, enabled); break;
            case "hue":
            case "saturation":
            case "lightness": DrawColorPart(s, row, enabled); break;
        }
    }

    static void DrawHeader(Parts p, Row row)
    {
        Rectangle r = ControlAnchor._gridItemRegion;
        Panel_Layout divider = p.Divider;
        divider.Size = new Vector2(r.Width, divider.Size.Y);
        divider.Location = new Vector2(r.X + r.Width / 2f, r.Y + r.Height - divider.Size.Y / 2);
        GUIPanel.Draw(divider, false, default, default, default);
        SetAlignment(p.Header, Middle | 2);
        p.Header.Location = new Vector2(r.X + r.Width / 2f, r.Y + r.Height / 2f - 2);
        GUIString.Draw(p.Header, row.Header ?? "", new Color(255, 255, 255, 255), false);
    }

    static string Label(Row row) => row.Option!.Label(row.Entry.Active);

    static void SetValue(Row row, object? value)
    {
        ConfigOption option = row.Option!;
        object? coerced = option.Coerce(value);
        if (coerced == null || Equals(option.Get(row.Entry.View), coerced))
            return;
        option.Set(row.Entry.View, coerced);
    }

    static void DrawToggle(SettingsOverlay_Layout s, Parts p, Row row, bool enabled)
    {
        RowState st = StateOf(row);
        var t = enabled ? s.ToggleTemplate : s.DisabledToggleTemplate;
        var state = GUITransactionButton.Draw(t.ToggleButton, default(Item), Label(row), !enabled, ref st.Scale, false, Focus(), false, false);
        bool value = (bool)row.Option!.Get(row.Entry.View)!;
        if (enabled && state == GUITransactionButton.InputState.Clicked && !RowBlocked())
        {
            SetValue(row, !value);
            value = !value;
            SoundEngine.PlaySound(TickSound);
        }

        if (value)
        {
            GUIPanel.Draw(t.Option2Disabled, false, default, default, default);
            GUIPanel.Draw(t.Option1Enabled, false, default, default, default);
        }
        else
        {
            GUIPanel.Draw(t.Option1Disabled, false, default, default, default);
            GUIPanel.Draw(t.Option2Enabled, false, default, default, default);
        }

        Color onColor = value && enabled ? t.Option1Label.Color : t.Option1DisabledLabelColour;
        Color offColor = !value && enabled ? t.Option2Label.Color : t.Option2DisabledLabelColour;
        DrawLabel(p, t.Option1Label, t.Option1Label.Location.X, t.Option1Enabled.Size.X, Language.GetTextValue("Mobile.ToggleSettingOn"), onColor);
        DrawLabel(p, t.Option2Label, t.Option2Label.Location.X, t.Option2Enabled.Size.X, Language.GetTextValue("Mobile.ToggleSettingOff"), offColor);
    }

    delegate bool SliderDraw(Slider_Layout layout, bool disablePick, ref float value, GUISlider.DragState drag, bool forceOver);

    static float? DrawSlider(SettingsOverlay_Layout s, Row row, string title, bool enabled, Slider_Layout layout, float value01, SliderDraw draw)
    {
        var t = s.SliderTemplate;
        RowState st = StateOf(row);
        GUITransactionButton.Draw(t.Title, default(Item), title, !enabled, ref st.Scale, false, Focus(), false, false);
        if (st.Drag.IsNull)
            st.Drag = new GUISlider.DragState(Keep(GUISlider.DragState.New().Pointer));
        st.Value = value01;
        Rectangle r = ControlAnchor._gridItemRegion;
        bool OnTrack(float x) => GUISlider.IsCursorOver(new Vector2(x, r.Y + r.Height / 2f), layout);
        float pad = r.Height / 2f;
        if (enabled && slider == null && Main.mouseLeft && pressInList && !dragged && !guard && popup == null &&
            Main.mouseY >= r.Y && Main.mouseY <= r.Y + r.Height && (OnTrack(Main.mouseX) || OnTrack(Main.mouseX - pad) || OnTrack(Main.mouseX + pad)))
            slider = row.Key;
        bool mine = slider == row.Key;
        bool dragging = draw(layout, !mine, ref st.Value, st.Drag, mine && Main.mouseLeft);
        st.Drag.wasDragging = dragging && mine;
        return mine ? st.Value : null;
    }

    static void DrawValue(SettingsOverlay_Layout s, string text, bool enabled)
    {
        var t = s.SliderTemplate;
        GUIString.Draw(t.Value, text, enabled ? t.Value.Color : t.DisabledValueColor, false);
    }

    static void DrawRange(SettingsOverlay_Layout s, Row row, bool enabled)
    {
        ConfigOption o = row.Option!;
        double current = Convert.ToDouble(o.Get(row.Entry.View), System.Globalization.CultureInfo.InvariantCulture);
        float? v = DrawSlider(s, row, Label(row), enabled, s.SliderTemplate.Option, (float)((current - o.Min) / (o.Max - o.Min)),
            (Slider_Layout l, bool disable, ref float value, GUISlider.DragState drag, bool force) => GUISlider.Draw(l, disable, ref value, drag, default, force, -1, -1, false));
        if (v != null)
            SetValue(row, o.Min + v.Value * (o.Max - o.Min));
        double shown = Convert.ToDouble(o.Get(row.Entry.View), System.Globalization.CultureInfo.InvariantCulture);
        string stepText = o.Step.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int digits = o.Integer ? 0 : stepText.Contains('.') ? stepText.Split('.')[1].Length : 0;
        DrawValue(s, shown.ToString("F" + digits, System.Globalization.CultureInfo.InvariantCulture), enabled);
    }

    static int ChoiceIndex(Row row)
    {
        object? value = row.Option!.Get(row.Entry.View);
        return Array.FindIndex(row.Option.Choices, c => Equals(c, value));
    }

    static void DrawRadio(SettingsOverlay_Layout s, Parts p, Row row, bool enabled)
    {
        ConfigOption o = row.Option!;
        RowState st = StateOf(row);
        var t = s.ToggleTemplate;
        var state = GUITransactionButton.Draw(t.ToggleButton, default(Item), Label(row), !enabled, ref st.Scale, false, Focus(), false, false);
        int n = o.Choices.Length;
        var seg = Segments(t, n);
        if (enabled && state == GUITransactionButton.InputState.Clicked && !RowBlocked())
        {
            int picked = -1;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = SegmentCenter(t, seg.XOf(i));
                if (Math.Abs(Main.mouseX - c.X) <= seg.Each / 2 && Math.Abs(Main.mouseY - c.Y) <= seg.Height / 2 + 4)
                    picked = i;
            }

            if (picked < 0)
                picked = (ChoiceIndex(row) + 1) % n;
            SetValue(row, o.Choices[picked]);
            SoundEngine.PlaySound(TickSound);
        }

        int chosen = ChoiceIndex(row);
        var order = Enumerable.Range(0, n).Where(i => i != chosen).ToList();
        if (chosen >= 0)
            order.Add(chosen);
        foreach (int i in order)
        {
            Panel_Layout piece = i == chosen && enabled ? p.ChoiceOn : p.ChoiceOff;
            piece.Location = new Vector2(seg.XOf(i), seg.Y);
            piece.Size = new Vector2(seg.Each - 2, seg.Height);
            GUIPanel.Draw(piece, false, default, default, default);
        }

        for (int i = 0; i < n; i++)
            DrawLabel(p, t.Option1Label, seg.XOf(i), seg.Each, o.ChoiceLabel(row.Entry.Active, i), i == chosen && enabled ? t.Option1Label.Color : t.Option1DisabledLabelColour, true);
    }

    static void DrawDropdown(SettingsOverlay_Layout s, Row row, bool enabled)
    {
        ConfigOption o = row.Option!;
        RowState st = StateOf(row);
        var p = s.PulldownTemplate;
        bool isOpen = popup != null && popup.Row.Key == row.Key;
        int index = ChoiceIndex(row);
        string value = index >= 0 ? o.ChoiceLabel(row.Entry.Active, index) : "";
        var state = GUITransactionButton.Draw(p.PulldownButton, default(Item), value, !enabled, ref st.Scale, isOpen, Focus(), false, false);
        GUIString.Draw(p.PulldownTitle, Label(row), enabled ? p.PulldownTitle.Color : p.DisabledPulldownLabelColour, false);
        if (enabled && state == GUITransactionButton.InputState.Clicked && !RowBlocked())
        {
            popup = new Popup { Row = row, Rect = ControlAnchor._gridItemRegion, Scales = Enumerable.Repeat(1f, o.Choices.Length).ToArray() };
            SoundEngine.PlaySound(TickSound);
        }
    }

    static void DrawPopup(SettingsOverlay_Layout s)
    {
        Popup? pop = popup;
        if (pop == null)
            return;
        Row row = pop.Row;
        ConfigOption o = row.Option!;
        Parts parts = PartsOf(s);
        var p = s.PulldownTemplate;
        SpriteBatch batch = Main.spriteBatch;
        batch.End();
        batch.Begin((SpriteSortMode)0, default, default, default, default, default, default, true);
        GUIInputRegionManager.Instance.RegisterInputRegion(Rect(-4000, -4000, 12000, 12000));
        parts.Greyout.Location = Vector2.Zero;
        parts.Greyout.Size = new Vector2(12000, 12000);
        GUIPanel.Draw(parts.Greyout, false, default, default, default);
        float h = p.Option1.overloadedSize.Y;
        float step = p.Option2.Location.Y - p.Option1.Location.Y;
        String_Layout label = p.Option1.Label;
        SpriteFont font = label.IsNull ? default : label.GetFont();
        int n = o.Choices.Length;
        float widest = 0;
        if (!font.IsNull)
            for (int i = 0; i < n; i++)
                widest = Math.Max(widest, font.MeasureString(o.ChoiceLabel(row.Entry.Active, i)).X * label.Scale);
        float width = Math.Min(pop.Rect.Width, Math.Max(p.Option1.overloadedSize.X, widest + 40));
        float blockH = (n - 1) * step + h;
        float x0 = pop.Rect.X + pop.Rect.Width - width - 6;
        Rectangle frame = GUIPanel.Region(s.Backing);
        float lowest = frame.Y + frame.Height - blockH - PopupMargin;
        float y0 = pop.Rect.Y + pop.Rect.Height + 4;
        if (y0 > lowest)
            y0 = pop.Rect.Y - 4 - blockH;
        y0 = Math.Max(frame.Y + PopupMargin, Math.Min(y0, lowest));
        Vector2 tl = p.PulldownBackingTLOffset, br = p.PulldownBackingBROffset;
        float bx = x0 + tl.X, by = y0 + tl.Y, bw = width - tl.X + br.X, bh = blockH - tl.Y + br.Y;
        parts.PopBacking.Location = new Vector2(bx + bw / 2, by + bh / 2);
        parts.PopBacking.Size = new Vector2(bw, bh);
        GUIPanel.Draw(parts.PopBacking, false, default, default, default);
        Rectangle saved = ControlAnchor._gridItemRegion;
        parts.Option.overloadedSize = new Vector2(width, h);
        parts.Option.Location = new Vector2(-width / 2, -h / 2);
        int chosen = ChoiceIndex(row);
        bool close = false;
        try
        {
            for (int i = 0; i < n; i++)
            {
                ControlAnchor._gridItemRegion = Rect(x0, y0 + i * step, width, h);
                var state = GUITransactionButton.Draw(parts.Option, default(Item), o.ChoiceLabel(row.Entry.Active, i), false, ref pop.Scales[i], i == chosen, Focus(), false, false);
                if (state == GUITransactionButton.InputState.Clicked && pop.Armed && !guard)
                {
                    SetValue(row, o.Choices[i]);
                    SoundEngine.PlaySound(TickSound);
                    close = true;
                }
            }
        }
        finally
        {
            ControlAnchor._gridItemRegion = saved;
        }

        bool inside = Main.mouseX >= bx && Main.mouseX <= bx + bw && Main.mouseY >= by && Main.mouseY <= by + bh;
        if (!Main.mouseLeft && !pop.Down)
            pop.Armed = true;
        if (Main.mouseLeft && pop.Armed)
        {
            if (!pop.Down)
            {
                pop.Down = true;
                pop.DownInside = inside;
            }
        }
        else if (pop.Down)
        {
            pop.Down = false;
            if (!pop.DownInside)
                close = true;
        }

        if (close)
        {
            popup = null;
            Guard();
        }
    }

    static void DrawCycle(SettingsOverlay_Layout s, Parts p, Row row, bool enabled)
    {
        ConfigOption o = row.Option!;
        RowState st = StateOf(row);
        int index = ChoiceIndex(row);
        var state = GUITransactionButton.Draw(s.ToggleTemplate.ToggleButton, default(Item), Label(row), !enabled, ref st.Scale, false, Focus(), false, false);
        String_Layout value = s.SliderTemplate.Value;
        p.Cycle.Location = new Vector2(CycleX, value.Location.Y);
        GUIString.Draw(p.Cycle, "<  " + (index >= 0 ? o.ChoiceLabel(row.Entry.Active, index) : "") + "  >", enabled ? value.Color : s.SliderTemplate.DisabledValueColor, false);
        if (enabled && state == GUITransactionButton.InputState.Clicked && !RowBlocked())
        {
            SetValue(row, o.Choices[(index + 1) % o.Choices.Length]);
            SoundEngine.PlaySound(TickSound);
        }
    }

    static float[] ToHsl(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        float l = (max + min) / 2;
        if (max == min)
            return new[] { 0f, 0f, l };
        float d = max - min;
        float sat = l > 0.5f ? d / (2 - max - min) : d / (max + min);
        float h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return new[] { h / 6, sat, l };
    }

    static Color FromHsl(float h, float sat, float l, byte alpha)
    {
        static float Channel(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6f) return p + (q - p) * 6 * t;
            if (t < 1 / 2f) return q;
            if (t < 2 / 3f) return p + (q - p) * (2 / 3f - t) * 6;
            return p;
        }

        float r = l, g = l, b = l;
        if (sat > 0)
        {
            float q = l < 0.5f ? l * (1 + sat) : l + sat - l * sat;
            float pp = 2 * l - q;
            r = Channel(pp, q, h + 1 / 3f);
            g = Channel(pp, q, h);
            b = Channel(pp, q, h - 1 / 3f);
        }

        static int Byte(float v) => (int)MathF.Round(Math.Clamp(v, 0, 1) * 255);
        return new Color(Byte(r), Byte(g), Byte(b), alpha);
    }

    static float[] HslOf(Row row)
    {
        string key = row.Entry.Active.FullName + "." + row.Option!.Name;
        Color current = (Color)row.Option.Get(row.Entry.View)!;
        if (!colorState.TryGetValue(key, out var cs) || !cs.Color.Equals(current))
        {
            cs = (current, ToHsl(current));
            colorState[key] = cs;
        }

        return cs.Hsl;
    }

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    static void DrawColor(SettingsOverlay_Layout s, Parts p, Row row, bool enabled)
    {
        RowState st = StateOf(row);
        var t = s.ToggleTemplate;
        GUITransactionButton.Draw(t.ToggleButton, default(Item), Label(row), !enabled, ref st.Scale, false, Focus(), false, false);
        Color color = (Color)row.Option!.Get(row.Entry.View)!;
        var seg = Segments(t, 1);
        p.Swatch.Location = new Vector2(seg.XOf(0), seg.Y);
        p.Swatch.Size = new Vector2(seg.Each - 2, seg.Height);
        GUIPanel.Draw(p.Swatch, false, default, default, default);
        Rectangle box = GUIPanel.Region(p.Swatch);
        Main.spriteBatch.Draw(Asset<Texture2D>.ForGameAsset(TextureAssets.MagicPixel.Pointer, "Images/MagicPixel").Value, Rect(box.X + SwatchInset, box.Y + SwatchInset, box.Width - SwatchInset * 2, box.Height - SwatchInset * 2), new Color(color.R, color.G, color.B, 255));
        String_Layout value = s.SliderTemplate.Value;
        GUIString.Draw(value, Hex(color), enabled ? value.Color : s.SliderTemplate.DisabledValueColor, false);
    }

    static void DrawColorPart(SettingsOverlay_Layout s, Row row, bool enabled)
    {
        var t = s.SliderTemplate;
        float[] hsl = HslOf(row);
        int index = row.Kind == "hue" ? 0 : row.Kind == "saturation" ? 1 : 2;
        string title = "   " + Language.GetTextValue(row.Kind == "hue" ? "Mobile.Hue" : row.Kind == "saturation" ? "Mobile.Saturation" : "Mobile.Lightness");
        Slider_Layout layout = row.Kind == "hue" ? t.HueOption : row.Kind == "saturation" ? t.SaturationOption : t.LightOption;
        SliderDraw draw = row.Kind switch
        {
            "hue" => (Slider_Layout l, bool disable, ref float value, GUISlider.DragState drag, bool force) => GUISlider.Draw(l, disable, ref value, drag, default, force, -1, -1, false),
            "saturation" => GUISlider.DrawSaturation,
            _ => GUISlider.DrawLightness
        };
        float h0 = Main.hBar, s0 = Main.sBar, l0 = Main.lBar;
        Main.hBar = hsl[0];
        Main.sBar = hsl[1];
        Main.lBar = hsl[2];
        float? v;
        try
        {
            v = DrawSlider(s, row, title, enabled, layout, hsl[index], draw);
        }
        finally
        {
            Main.hBar = h0;
            Main.sBar = s0;
            Main.lBar = l0;
        }

        if (v != null && v.Value != hsl[index])
        {
            hsl[index] = v.Value;
            Color old = (Color)row.Option!.Get(row.Entry.View)!;
            Color updated = FromHsl(hsl[0], hsl[1], hsl[2], row.Option.ColorNoAlpha ? (byte)255 : old.A);
            string key = row.Entry.Active.FullName + "." + row.Option.Name;
            colorState[key] = (updated, hsl);
            SetValue(row, updated);
        }

        DrawValue(s, (int)MathF.Round(hsl[index] * 100) + "%", enabled);
    }
}
