namespace Terraria.Localization;

public readonly partial struct LocalizedText
{
    public override string ToString() => Pointer == 0 ? "" : Value ?? "";
}
