using BunnyLoader.Interop;
using Terraria.ModLoader.Core;

namespace Terraria.Localization;

public static partial class Language
{
    public static LocalizedText SelectRandom(LanguageSearchFilter filter) => SelectRandom(filter, Main.rand);

    internal static void InstallModVariations() => ItemHooks.Once("ModTextVariations", () => On_Language.TryGetVariation += Variation);
    static bool Variation(On_Language.orig_TryGetVariation orig, string? key, string? variant, out GameString value)
    {
        if (orig(key, variant, out value)) return true;
        string combined = key + "$" + variant;
        LocalizedText text = GetText(combined);
        if (text.IsNull || text.Value == combined) return false;
        value = text.Value;
        return true;
    }
}
