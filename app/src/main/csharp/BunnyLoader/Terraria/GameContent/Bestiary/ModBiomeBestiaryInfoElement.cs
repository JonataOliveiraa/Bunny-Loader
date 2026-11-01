namespace Terraria.GameContent.Bestiary;

public readonly struct ModBiomeBestiaryInfoElement
{
    public readonly SpawnConditionBestiaryInfoElement Element;

    internal ModBiomeBestiaryInfoElement(SpawnConditionBestiaryInfoElement element) => Element = element;

    public bool IsNull => Element.IsNull;

    public string GetDisplayNameKey() => Element.GetDisplayNameKey() ?? "";

    public static implicit operator IBestiaryInfoElement(ModBiomeBestiaryInfoElement element) => new(element.Element.Pointer);
}
