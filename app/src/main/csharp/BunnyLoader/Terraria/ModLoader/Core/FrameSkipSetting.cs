using System;
using System.IO;
using System.Text.Json;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal enum FrameSkipOption
{
    Auto,
    On,
    Off,
}

internal static class FrameSkipSetting
{
    const int SettingNone = 192;
    const int GeneralCategory = 0;
    const float CaptionPadding = 16;

    static GameHandle? entry;
    static bool forcing;

    internal static FrameSkipOption Mode { get; private set; }

    internal static void Install() =>
        ItemHooks.Once("FrameSkipSetting", () =>
        {
            Restore();

            On_GUISettingsOverlay.SetCategory += SetCategory;
            On_GUIOpenUIButtonSetting.GetOption += GetOption;
            On_GUIOpenUIButtonSetting.GetButtonCaption += GetButtonCaption;
            On_GUIOpenUIButtonSetting.OpenUI += OpenUI;
        });

    static string File => Path.Combine(Main.SavePath, "BunnyLoader.frameskip.json");

    static void Restore()
    {
        FrameSkipOption mode = FrameSkipOption.Auto;
        string file;
        try { file = File; }
        catch (Exception e)
        {
            BunnyLoader.Log.Warn($"FrameSkip: save path unavailable ({e.Message}); using Auto");
            Apply(mode);
            return;
        }

        if (System.IO.File.Exists(file))
        {
            try
            {
                using JsonDocument json = JsonDocument.Parse(System.IO.File.ReadAllText(file));
                if (json.RootElement.TryGetProperty("Mode", out JsonElement value) && value.ValueKind == JsonValueKind.String
                    && Enum.TryParse(value.GetString(), out FrameSkipOption parsed) && Enum.IsDefined(parsed))
                    mode = parsed;
            }
            catch (Exception e)
            {
                BunnyLoader.Log.Warn($"FrameSkip: {file} is broken ({e.Message}); using Auto");
            }
        }

        Apply(mode);
    }

    internal static void Set(FrameSkipOption mode)
    {
        Apply(mode);

        try { System.IO.File.WriteAllText(File, JsonSerializer.Serialize(new { Mode = mode.ToString() })); }
        catch (Exception e) { ItemHooks.Report("FrameSkipSetting (save)", e); }
    }

    // TODO: a stall longer than 0.25 s is never caught up in any mode; Game.UnityUpdate resets its clock natively.
    // TODO: Off and On were only measured on MuMu (60 Hz); check them on a phone with a 90/120 Hz screen (highFrameRate path).
    static void Apply(FrameSkipOption mode)
    {
        Mode = mode;

        XNAUnityRunner runner = XNAUnityRunner._instance;
        if (!runner.IsNull) runner.DisableVariableUpdate = mode == FrameSkipOption.Off;

        bool force = mode == FrameSkipOption.On;
        if (force == forcing) return;

        forcing = force;
        if (force) Microsoft.Xna.Framework.On_Game.UnityUpdate += ForceVariableUpdate;
        else Microsoft.Xna.Framework.On_Game.UnityUpdate -= ForceVariableUpdate;
    }

    // TODO: the once-per-second fps check inside orig can still turn variable mode off for one frame (56-62 updates/s), dropping that frame's backlog.
    static void ForceVariableUpdate(Microsoft.Xna.Framework.On_Game.orig_UnityUpdate orig, Microsoft.Xna.Framework.Game self, float elapsedTime)
    {
        self.updateModeVariable = true;
        orig(self, elapsedTime);
    }

    static bool Ours(GUIOpenUIButtonSetting self) => entry != null && self.Pointer == entry.Target;

    static void SetCategory(On_GUISettingsOverlay.orig_SetCategory orig, GUISettingsOverlay self, SettingsOverlayOptionCategories_Layout.Category category)
    {
        orig(self, category);

        try { AddEntry(self); }
        catch (Exception e) { ItemHooks.Report("FrameSkipSetting (row)", e); }
    }

    static void AddEntry(GUISettingsOverlay overlay)
    {
        if ((int)overlay.Selected != GeneralCategory) return;

        var list = new GameList(overlay._categorySettings);
        if (list.IsNull) return;

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
        Ours(self) ? Text("FrameSkipRow") : orig(self);

    static string? GetButtonCaption(On_GUIOpenUIButtonSetting.orig_GetButtonCaption orig, GUIOpenUIButtonSetting self)
    {
        if (!Ours(self)) return orig(self);

        string value = Text("FrameSkip" + Mode);
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
        catch (Exception e) { ItemHooks.Report("FrameSkipSetting (caption width)", e); }
        return value;
    }

    static void OpenUI(On_GUIOpenUIButtonSetting.orig_OpenUI orig, GUIOpenUIButtonSetting self)
    {
        if (!Ours(self))
        {
            orig(self);
            return;
        }

        Set((FrameSkipOption)(((int)Mode + 1) % 3));
    }
}
