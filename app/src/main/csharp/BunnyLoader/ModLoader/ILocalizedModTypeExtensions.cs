using System;
using Terraria.Localization;

namespace Terraria.ModLoader;

public static class ILocalizedModTypeExtensions
{
    public static string GetLocalizationKey(this ILocalizedModType self, string suffix) =>
        $"Mods.{self.Mod.Name}.{self.LocalizationCategory}.{self.Name}.{suffix}";

    public static LocalizedText GetLocalization(this ILocalizedModType self, string suffix, Func<string>? makeDefaultValue = null) =>
        Language.GetOrRegister(self.GetLocalizationKey(suffix), makeDefaultValue);

    public static string GetLocalizedValue(this ILocalizedModType self, string suffix) => self.GetLocalization(suffix).Value ?? "";
}
