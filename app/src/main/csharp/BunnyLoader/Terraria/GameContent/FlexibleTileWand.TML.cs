namespace Terraria.GameContent;

public readonly unsafe partial struct FlexibleTileWand
{
    public FlexibleTileWand AddVariations(int itemType, int tileIdToPlace, params int[] stylesToPlace)
    {
        AddVariations(itemType, (ushort)tileIdToPlace, (BunnyLoader.Interop.GameArray<int>)stylesToPlace);
        return this;
    }
}
