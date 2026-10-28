using Terraria.ModLoader;

namespace CsLoc;

public sealed class CsLocMod : Mod
{
    public override void Load()
    {
        Checks.Report("GetLocalization in Load", GetLocalization("Plain").Value == "plain text", $"{GetLocalization("Plain").Value}");
        Checks.Report("GetLocalizationKey", GetLocalizationKey("Plain") == "Mods.CsLoc.Plain", GetLocalizationKey("Plain"));
    }
}
