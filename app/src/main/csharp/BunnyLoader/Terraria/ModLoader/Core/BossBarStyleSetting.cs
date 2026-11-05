using System;
using BunnyLoader.Interop;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class BossBarStyleSetting
{
    const int SettingNone = 192;
    const int GeneralCategory = 0;

    static GameHandle? entry;

    internal static void Install() =>
        ItemHooks.Once("BossBarStyleSetting", () =>
        {
            On_GUISettingsOverlay.SetCategory += SetCategory;
            On_GUIOpenUIButtonSetting.GetOption += GetOption;
            On_GUIOpenUIButtonSetting.GetButtonCaption += GetButtonCaption;
            On_GUIOpenUIButtonSetting.OpenUI += OpenUI;
        });

    static bool Ours(GUIOpenUIButtonSetting self) => entry != null && self.Pointer == entry.Target;

    static void SetCategory(On_GUISettingsOverlay.orig_SetCategory orig, GUISettingsOverlay self, SettingsOverlayOptionCategories_Layout.Category category)
    {
        orig(self, category);
        try { AddEntry(self); }
        catch (Exception e) { ItemHooks.Report("BossBarStyleSetting (row)", e); }
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

    static string? GetOption(On_GUIOpenUIButtonSetting.orig_GetOption orig, GUIOpenUIButtonSetting self) =>
        Ours(self) ? Language.GetTextValue("BunnyLoader.BossBarStyleRow") : orig(self);

    static string? GetButtonCaption(On_GUIOpenUIButtonSetting.orig_GetButtonCaption orig, GUIOpenUIButtonSetting self) =>
        Ours(self) ? BossBarLoader.CurrentStyle.DisplayName : orig(self);

    static void OpenUI(On_GUIOpenUIButtonSetting.orig_OpenUI orig, GUIOpenUIButtonSetting self)
    {
        if (!Ours(self))
        {
            orig(self);
            return;
        }
        BossBarLoader.NextBossBarStyle();
    }
}
