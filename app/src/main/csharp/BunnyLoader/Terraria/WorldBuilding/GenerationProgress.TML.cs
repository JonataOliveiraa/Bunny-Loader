namespace Terraria.WorldBuilding;

public readonly partial struct GenerationProgress
{
    public GenerationProgress() => Pointer = New().Pointer;
}
