using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent.UI;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class GameTipHooks
{
    const string DefaultCategory = "LoadingTips_Default", KeyboardCategory = "LoadingTips_Keyboard", GamepadCategory = "LoadingTips_GamePad";

    static List<GameTipData>? tips;
    static int defaultCount, keyboardCount;
    static readonly Random random = new();
    static readonly List<LocalizedText> pool = new();
    static LocalizedText last;

    internal static void Install() => ItemHooks.Once("GameTipVisibility", () => On_GameTipsDisplay.AddNewTip += Add);

    static void Add(On_GameTipsDisplay.orig_AddNewTip orig, GameTipsDisplay self, double time)
    {
        orig(self, time);
        if (self.isCharacterCreationTips) return;
        List<GameTipData> all = tips ??= Build();
        XNAUnityRunner.ForcedInputMode mode = XNAUnityRunner.PrimaryInputMode;
        pool.Clear();
        for (int i = 0; i < all.Count; i++)
        {
            if (!all[i].Visible) continue;
            bool keyboard = i >= defaultCount && i < defaultCount + keyboardCount;
            bool gamepad = i >= defaultCount + keyboardCount && all[i].Mod == null;
            if (keyboard && mode != XNAUnityRunner.ForcedInputMode.KeyboardMouse) continue;
            if (gamepad && mode != XNAUnityRunner.ForcedInputMode.Controller) continue;
            pool.Add(all[i].TipText);
        }
        if (pool.Count == 0) return;
        var current = new GameList(self._currentTips);

        if (current.IsNull || current.Count == 0) return;
        var tip = current.Get<GameTipsDisplay.GameTip>(current.Count - 1);

        if (tip.IsNull) return;
        LocalizedText chosen = pool[random.Next(pool.Count)];

        if (pool.Count > 1 && chosen == last) chosen = pool[(pool.IndexOf(chosen) + 1 + random.Next(pool.Count - 1)) % pool.Count];
        tip._textKey = chosen;
        tip._formattedText = chosen.FormatWith(Lang.CreateDialogSubstitutionObject(default));
        self._lastTip = chosen;
        last = chosen;
    }

    static List<GameTipData> Build()
    {
        var all = new List<GameTipData>();
        defaultCount = AddCategory(all, DefaultCategory);
        keyboardCount = AddCategory(all, KeyboardCategory);
        AddCategory(all, GamepadCategory);
        
        foreach (Mod mod in Host.Mods)
            foreach (string key in LocalizationLoader.KeysWithPrefix(mod, $"Mods.{mod.Name}.GameTips."))
                all.Add(new(Language.GetText(key), mod));
        foreach (ModSystem system in SystemLoader.With(nameof(ModSystem.ModifyGameTipVisibility)))
        {
            try { system.ModifyGameTipVisibility(all.AsReadOnly()); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyGameTipVisibility", e); }
        }
        return all;
    }

    static int AddCategory(List<GameTipData> all, string category)
    {
        LanguageManager manager = LanguageManager.Instance;
        int size = manager.IsNull ? 0 : manager.GetCategorySize(category);
        int found = 0;
        for (int i = 0; found < size && i < size + 1000; i++)
        {
            string key = $"{category}.{i}";
            if (!Language.Exists(key)) continue;
            all.Add(new(Language.GetText(key)));
            found++;
        }
        return found;
    }
}
