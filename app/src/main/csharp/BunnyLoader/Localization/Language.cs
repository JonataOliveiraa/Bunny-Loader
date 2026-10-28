using System;
using Terraria.ModLoader;

namespace Terraria.Localization;

public static partial class Language
{
    public static string GetTextValue(string key, params object?[] args) => Formatting.Apply(GetTextValue(key) ?? key, args);

    public static LocalizedText GetOrRegister(string key, Func<string>? makeDefaultValue = null) =>
        LocalizationLoader.GetOrRegister(key, makeDefaultValue);
}
