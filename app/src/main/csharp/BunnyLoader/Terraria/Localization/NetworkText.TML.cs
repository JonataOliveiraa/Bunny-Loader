namespace Terraria.Localization;

public readonly partial struct NetworkText
{
    public override string ToString() => Pointer == 0 ? "" : ToString_() ?? "";
}
