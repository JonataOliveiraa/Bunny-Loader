using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static unsafe class MenuHooks
{
    struct LogoDraw
    {
        public Texture2D Texture;
        public Vector2 Position;
        public bool HasSource;
        public Rectangle Source;
        public Color Color;
        public float Rotation;
        public Vector2 Origin;
        public float Scale;
        public SpriteEffects Effects;
        public float Depth;
    }

    const int SettingNone = 192;
    const int GeneralCategory = 0;
    const int PortugueseCulture = 8;
    const float CaptionPadding = 16;

    static readonly List<LogoDraw> Captured = new();
    static GUIOpenUIButtonSetting entry;
    static GameHandle? entryRoot;
    static Hook? getOption, getCaption, openUI;

    internal static void Install(Type type)
    {
        ItemHooks.Once("Menu", () =>
        {
            On_GUILogo.Draw += DrawLogo;
            On_GUISettingsOverlay.SetCategory += SetCategory;
            getOption = ItemHooks.Raw("", "GUIOpenUIButtonSetting", "string GetOption()", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr>)&GetOption);
            getCaption = ItemHooks.Raw("", "GUIOpenUIButtonSetting", "string GetButtonCaption()", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr>)&GetButtonCaption);
            openUI = ItemHooks.Raw("", "GUIOpenUIButtonSetting", "void OpenUI()", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, void>)&OpenUI);
            MusicHooks.Install();
        });
        if (ItemHooks.Overrides(type, typeof(ModMenu), "get_SunTexture") || ItemHooks.Overrides(type, typeof(ModMenu), "get_MoonTexture"))
            ItemHooks.Once("MenuSky", () => On_Main.DrawSunAndMoon += DrawSunAndMoon);
    }

    static void DrawLogo(On_GUILogo.orig_Draw orig, GUILogo self)
    {
        if (!MenuLoader.Ready)
        {
            orig(self);
            return;
        }
        try { MenuLoader.Tick(); }
        catch (Exception e) { ItemHooks.Report("ModMenu (switch)", e); }
        ModMenu menu = MenuLoader.CurrentMenu;
        if (menu is VanillaMenu)
        {
            orig(self);
            return;
        }
        Captured.Clear();
        if (Host.Native()->BeginDrawCapture(IntPtr.Zero, &Capture) == 0)
        {
            orig(self);
            return;
        }
        try { orig(self); }
        finally { Host.Native()->EndDrawCapture(); }
        try { Draw(menu); }
        catch (Exception e) { ItemHooks.Report($"{menu.FullName} (logo)", e); }
    }

    [UnmanagedCallersOnly]
    static int Capture(DrawCapture* draw)
    {
        Captured.Add(new LogoDraw
        {
            Texture = new Texture2D(draw->Texture),
            Position = new Vector2(draw->X, draw->Y),
            HasSource = draw->HasSource != 0,
            Source = new Rectangle(draw->SourceX, draw->SourceY, draw->SourceWidth, draw->SourceHeight),
            Color = *(Color*)&draw->Color,
            Rotation = draw->Rotation,
            Origin = new Vector2(draw->OriginX, draw->OriginY),
            Scale = draw->ScaleX,
            Effects = (SpriteEffects)draw->Effects,
            Depth = draw->Depth,
        });
        return 1;
    }

    static void Blit(Texture2D texture, Vector2 position, Rectangle? source, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth) =>
        Main.spriteBatch.Draw(texture, position, source, color, rotation, origin, scale, effects, depth);

    static void Draw(ModMenu menu)
    {
        if (Captured.Count == 0) return;
        LogoDraw first = Captured[0];
        int r = 0, g = 0, b = 0, a = 0;
        foreach (LogoDraw d in Captured)
        {
            r += d.Color.R;
            g += d.Color.G;
            b += d.Color.B;
            a += d.Color.A;
        }
        var baseColor = new Color(Math.Min(255, r), Math.Min(255, g), Math.Min(255, b), Math.Min(255, a));
        Vector2 center = first.Position;
        float rotation = first.Rotation, scale = first.Scale;
        Color color = baseColor;
        SpriteBatch batch = Main.spriteBatch;
        bool draw = true;
        try { draw = menu.PreDrawLogo(batch, ref center, ref rotation, ref scale, ref color); }
        catch (Exception e) { ItemHooks.Report($"{menu.FullName}.PreDrawLogo", e); }
        if (draw)
        {
            Asset<Texture2D>? logo = null;
            try { logo = menu.Logo; }
            catch (Exception e) { ItemHooks.Report($"{menu.FullName}.Logo", e); }
            if (logo != null)
            {
                Texture2D texture = logo.Value;
                Blit(texture, center, null, color, rotation, new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0f);
            }
            else
            {
                bool same = color.Equals(baseColor);
                foreach (LogoDraw d in Captured)
                {
                    Color c = same ? d.Color : color * (a > 0 ? d.Color.A / (float)a : 1f / Captured.Count);
                    Blit(d.Texture, center, d.HasSource ? d.Source : null, c, rotation, d.Origin, scale, d.Effects, d.Depth);
                }
            }
        }
        try { menu.PostDrawLogo(batch, center, rotation, scale, color); }
        catch (Exception e) { ItemHooks.Report($"{menu.FullName}.PostDrawLogo", e); }
    }

    static IntPtr AssetOf(ModMenu menu, Func<ModMenu, Asset<Texture2D>?> get, string what)
    {
        try
        {
            Asset<Texture2D>? asset = get(menu);
            if (asset == null || asset.IsDirect) return IntPtr.Zero;
            asset.Wait();
            return asset.GameAsset;
        }
        catch (Exception e)
        {
            ItemHooks.Report($"{menu.FullName}.{what}", e);
            return IntPtr.Zero;
        }
    }

    static void DrawSunAndMoon(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        if (!Main.gameMenu || MenuLoader.Active() is not ModMenu menu)
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }
        IntPtr sun = AssetOf(menu, m => m.SunTexture, nameof(ModMenu.SunTexture));
        IntPtr moon = AssetOf(menu, m => m.MoonTexture, nameof(ModMenu.MoonTexture));
        GameArray<GameObject> moons = TextureAssets.Moon.Raw;
        if ((sun == IntPtr.Zero && moon == IntPtr.Zero) || moons.Length == 0)
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }
        int index = Math.Clamp(Main.moonType, 0, moons.Length - 1);
        Asset<Texture2D> oldSun = TextureAssets.Sun;
        GameObject oldMoon = moons[index];
        int oldPhase = Main.moonPhase;
        try
        {
            if (sun != IntPtr.Zero) TextureAssets.Sun = Asset<Texture2D>.FromGame(sun);
            if (moon != IntPtr.Zero)
            {
                moons[index] = new GameObject(moon);
                Main.moonPhase = 0;
            }
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
        }
        finally
        {
            TextureAssets.Sun = oldSun;
            moons[index] = oldMoon;
            Main.moonPhase = oldPhase;
        }
    }

    static void SetCategory(On_GUISettingsOverlay.orig_SetCategory orig, GUISettingsOverlay self, SettingsOverlayOptionCategories_Layout.Category category)
    {
        orig(self, category);
        try { AddEntry(self); }
        catch (Exception e) { ItemHooks.Report("ModMenu (settings row)", e); }
    }

    static void AddEntry(GUISettingsOverlay overlay)
    {
        if (!Main.gameMenu || (int)overlay.Selected != GeneralCategory || MenuLoader.All.Count == 0) return;
        var list = new GameList(overlay._categorySettings);
        if (list.IsNull) return;
        if (entry.IsNull)
        {
            entry = GUIOpenUIButtonSetting.New((GUISettingID)SettingNone);
            entry._category = (SettingsOverlayOptionCategories_Layout.Category)GeneralCategory;
            entryRoot = new GameHandle(entry.Pointer);
        }
        if (list.IndexOf(entry.Pointer) >= 0) return;
        int at = 0;
        for (int k = 0; k < list.Count; k++)
        {
            var item = new GameObject(list[k]);
            if (list[k] == entry.Pointer || !GUIOpenUIButtonSetting.Is(item) || (int)new GUIOpenUIButtonSetting(list[k])._id != SettingNone) continue;
            at = k + 1;
            break;
        }
        list.Insert(at, entry.Pointer);
    }

    static string Label()
    {
        try { return LanguageManager.Instance.ActiveCulture.LegacyId == PortugueseCulture ? "Tema do Menu:" : "Menu Theme:"; }
        catch (Exception) { return "Menu Theme:"; }
    }

    static string Caption()
    {
        string name = MenuLoader.NameOf(MenuLoader.CurrentMenu);
        try
        {
            TransactionButton_Layout caption = entry.GetLayout().Caption;
            String_Layout label = caption.Label;
            SpriteFont font = label.GetFont();
            float width = caption.overloadedSize.X - CaptionPadding;
            if (font.IsNull || width <= 0) return name;
            float Measure(string text) => font.MeasureString(text).X * label.Scale;
            if (Measure(name) <= width) return name;
            string cut = name;
            while (cut.Length > 1 && Measure(cut + "...") > width) cut = cut[..^1];
            return cut + "...";
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModMenu (settings caption width)", e);
            return name;
        }
    }

    static IntPtr Original(Hook? hook, IntPtr self, IntPtr method, string what)
    {
        if (hook == null) return IntPtr.Zero;
        var call = GameCall.Returning(RetKind.Int);
        call.Arg(self);
        call.Arg(method);
        return (IntPtr)ItemHooks.Original(ref call, hook, what);
    }

    [UnmanagedCallersOnly]
    static IntPtr GetOption(IntPtr self, IntPtr method)
    {
        if (self != IntPtr.Zero && self == entry.Pointer)
        {
            try { return ((GameString)Label()).Pointer; }
            catch (Exception e) { ItemHooks.Report("ModMenu (settings label)", e); }
        }
        return Original(getOption, self, method, "GUIOpenUIButtonSetting.GetOption");
    }

    [UnmanagedCallersOnly]
    static IntPtr GetButtonCaption(IntPtr self, IntPtr method)
    {
        if (self != IntPtr.Zero && self == entry.Pointer)
        {
            try { return ((GameString)Caption()).Pointer; }
            catch (Exception e) { ItemHooks.Report("ModMenu (settings caption)", e); }
        }
        return Original(getCaption, self, method, "GUIOpenUIButtonSetting.GetButtonCaption");
    }

    [UnmanagedCallersOnly]
    static void OpenUI(IntPtr self, IntPtr method)
    {
        if (self != IntPtr.Zero && self == entry.Pointer)
        {
            try { MenuLoader.Next(); }
            catch (Exception e) { ItemHooks.Report("ModMenu (next theme)", e); }
            return;
        }
        Original(openUI, self, method, "GUIOpenUIButtonSetting.OpenUI");
    }
}
