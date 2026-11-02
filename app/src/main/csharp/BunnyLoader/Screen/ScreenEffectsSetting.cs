using System;
using System.IO;
using System.Text.Json;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace BunnyLoader;

internal static class ScreenEffectsSetting
{
    const int SettingNone = 192;
    const int GeneralCategory = 0;
    const float CaptionPadding = 16;

    static GameHandle? entry;
    static bool restored;

    internal static void Install() =>
        ItemHooks.Once("ScreenEffectsSetting", () =>
        {
            On_GUISettingsOverlay.SetCategory += SetCategory;
            On_GUIOpenUIButtonSetting.GetOption += GetOption;
            On_GUIOpenUIButtonSetting.GetButtonCaption += GetButtonCaption;
            On_GUIOpenUIButtonSetting.OpenUI += OpenUI;
        });

    static string File => Path.Combine(Main.SavePath, "BunnyLoader.screen.json");

    internal static void Restore()
    {
        if (restored) return;
        restored = true;
        string file;
        try { file = File; }
        catch (Exception)
        {
            restored = false;
            return;
        }
        if (!System.IO.File.Exists(file)) return;
        try
        {
            using JsonDocument json = JsonDocument.Parse(System.IO.File.ReadAllText(file));
            if (json.RootElement.TryGetProperty("Enabled", out JsonElement enabled) && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
                ScreenEffects.Enabled = enabled.GetBoolean();
        }
        catch (Exception e)
        {
            Log.Error($"ScreenEffects: {file} is broken ({e.Message}); screen effects stay on");
        }
    }

    internal static void Set(bool enabled)
    {
        restored = true;
        ScreenEffects.Enabled = enabled;
        try { System.IO.File.WriteAllText(File, JsonSerializer.Serialize(new { Enabled = enabled })); }
        catch (Exception e) { ItemHooks.Report("ScreenEffectsSetting (save)", e); }
    }

    static bool Ours(GUIOpenUIButtonSetting self) => entry != null && self.Pointer == entry.Target;

    static void SetCategory(On_GUISettingsOverlay.orig_SetCategory orig, GUISettingsOverlay self, SettingsOverlayOptionCategories_Layout.Category category)
    {
        orig(self, category);
        try { AddEntry(self); }
        catch (Exception e) { ItemHooks.Report("ScreenEffectsSetting (row)", e); }
    }

    static void AddEntry(GUISettingsOverlay overlay)
    {
        if ((int)overlay.Selected != GeneralCategory) return;
        var list = new GameList(overlay._categorySettings);
        if (list.IsNull) return;
        Restore();
        if (entry == null)
        {
            GUIOpenUIButtonSetting created = GUIOpenUIButtonSetting.New((GUISettingID)SettingNone);
            created._category = (SettingsOverlayOptionCategories_Layout.Category)GeneralCategory;
            entry = new GameHandle(created.Pointer);
        }
        if (list.IndexOf(entry.Target) >= 0) return;
        int at = 0;
        for (int k = 0; k < list.Count; k++)
        {
            var item = new GameObject(list[k]);
            if (!GUIOpenUIButtonSetting.Is(item) || (int)new GUIOpenUIButtonSetting(list[k])._id != SettingNone) break;
            at = k + 1;
        }
        list.Insert(at, entry.Target);
    }

    static string Text(string key) => Language.GetTextValue("BunnyLoader." + key);

    static string? GetOption(On_GUIOpenUIButtonSetting.orig_GetOption orig, GUIOpenUIButtonSetting self) =>
        Ours(self) ? Text("ScreenEffectsRow") : orig(self);

    static string? GetButtonCaption(On_GUIOpenUIButtonSetting.orig_GetButtonCaption orig, GUIOpenUIButtonSetting self)
    {
        if (!Ours(self)) return orig(self);
        string value = Text(ScreenEffects.Enabled ? "ScreenEffectsOn" : "ScreenEffectsOff");
        try
        {
            TransactionButton_Layout caption = self.GetLayout().Caption;
            String_Layout label = caption.Label;
            SpriteFont font = label.GetFont();
            float width = caption.overloadedSize.X - CaptionPadding;
            if (font.IsNull || width <= 0) return value;
            float Measure(string text) => font.MeasureString(text).X * label.Scale;
            if (Measure(value) <= width) return value;
            string cut = value;
            while (cut.Length > 1 && Measure(cut + "...") > width) cut = cut[..^1];
            return cut + "...";
        }
        catch (Exception e) { ItemHooks.Report("ScreenEffectsSetting (caption width)", e); }
        return value;
    }

    static void OpenUI(On_GUIOpenUIButtonSetting.orig_OpenUI orig, GUIOpenUIButtonSetting self)
    {
        if (!Ours(self))
        {
            orig(self);
            return;
        }
        Set(!ScreenEffects.Enabled);
    }
}
