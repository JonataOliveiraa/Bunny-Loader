using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader;

public class TooltipLine
{
    public readonly string Mod;

    public readonly string Name;

    public string Text;

    public bool IsModifier;

    public bool IsModifierBad;

    public Color? OverrideColor;

    public bool OneDropLogo;

    internal string Role = "";

    internal uint? RoleColor;

    public bool Visible { get; private set; } = true;

    public string FullName => $"{Mod}/{Name}";

    public TooltipLine(Mod mod, string name, string text)
    {
        Mod = mod.Name;
        Name = name;
        Text = text;
    }

    internal TooltipLine(string mod, string name, string text)
    {
        Mod = mod;
        Name = name;
        Text = text;
    }

    public void Hide() => Visible = false;

    public override string ToString() => $"{FullName}: {Text}";
}
