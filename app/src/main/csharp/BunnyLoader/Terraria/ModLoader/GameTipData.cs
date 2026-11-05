using Terraria.Localization;

namespace Terraria.ModLoader;

public sealed class GameTipData
{
    public LocalizedText TipText { get; }
    public Mod? Mod { get; }
    public string Name { get; }
    public string FullName { get; }
    internal bool Visible = true;
    public GameTipData(LocalizedText text, Mod mod)
    {
        TipText = text; Mod = mod;
        Name = (text.Key ?? "").Replace($"Mods.{mod.Name}.GameTips.", "");
        FullName = mod.Name + "/" + Name;
    }
    internal GameTipData(LocalizedText text)
    {
        TipText = text; Name = text.Key ?? ""; FullName = "Terraria/" + Name;
    }
    public void Hide() => Visible = false;
}

public static class GameTipID
{
    public static readonly int BloodMoonZombieDoorOpening = 8;
    public static readonly int SolarEclipseCreepyMonsters = 74;
}
