namespace Terraria.Localization;

public readonly partial struct LocalizedText
{
    public string Format(params object?[] args) => Formatting.Apply(Value ?? "", args);
}
