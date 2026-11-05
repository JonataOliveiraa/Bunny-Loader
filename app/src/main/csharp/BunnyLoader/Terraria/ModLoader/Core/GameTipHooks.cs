using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.GameContent.UI;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static class GameTipHooks
{
    internal static void Install() => ItemHooks.Once("GameTipVisibility", () => On_GameTipsDisplay.AddNewTip += Add);
    static void Add(On_GameTipsDisplay.orig_AddNewTip orig, GameTipsDisplay self, double time)
    {
        var defaults = self._tipsDefault;
        var gamepad = self._tipsGamepad;
        var keyboard = self._tipsKeyboard;
        LocalizedText[] defaultTexts = defaults, gamepadTexts = gamepad, keyboardTexts = keyboard;
        var tips = new List<GameTipData>();
        foreach (LocalizedText text in defaultTexts) tips.Add(new(text));
        foreach (LocalizedText text in gamepadTexts) tips.Add(new(text));
        foreach (LocalizedText text in keyboardTexts) tips.Add(new(text));
        int nativeCount = tips.Count;
        foreach (Mod mod in Host.Mods)
            foreach (LocalizedText text in (LocalizedText[])Language.FindAll((key, value) => key != null && key.StartsWith($"Mods.{mod.Name}.GameTips.", StringComparison.Ordinal)))
                tips.Add(new(text, mod));
        foreach (ModSystem system in SystemLoader.With(nameof(ModSystem.ModifyGameTipVisibility)))
        {
            try { system.ModifyGameTipVisibility(tips.AsReadOnly()); }
            catch (Exception e) { ItemHooks.Report($"{system.FullName}.ModifyGameTipVisibility", e); }
        }
        var filtered = new List<LocalizedText>();
        for (int i = 0; i < defaultTexts.Length; i++) if (tips[i].Visible) filtered.Add(tips[i].TipText);
        for (int i = nativeCount; i < tips.Count; i++) if (tips[i].Visible) filtered.Add(tips[i].TipText);
        var pads = new List<LocalizedText>();
        for (int i = 0; i < gamepadTexts.Length; i++) if (tips[defaultTexts.Length + i].Visible) pads.Add(gamepadTexts[i]);
        var keys = new List<LocalizedText>();
        for (int i = 0; i < keyboardTexts.Length; i++) if (tips[defaultTexts.Length + gamepadTexts.Length + i].Visible) keys.Add(keyboardTexts[i]);
        if (filtered.Count + pads.Count + keys.Count == 0) return;
        using var pinDefault = new GameHandle(defaults.Pointer);
        using var pinPad = new GameHandle(gamepad.Pointer);
        using var pinKeys = new GameHandle(keyboard.Pointer);
        self._tipsDefault = GameArrayBuilder.Create<LocalizedText>(filtered.ToArray());
        if (!gamepad.IsNull) self._tipsGamepad = GameArrayBuilder.Create<LocalizedText>(pads.ToArray());
        if (!keyboard.IsNull) self._tipsKeyboard = GameArrayBuilder.Create<LocalizedText>(keys.ToArray());
        try { orig(self, time); }
        finally { self._tipsDefault = defaults; self._tipsGamepad = gamepad; self._tipsKeyboard = keyboard; }
    }
}
