using BunnyLoader.Interop;

namespace Terraria.ID;

public readonly partial struct SetFactory
{
    internal static void GrowToContent()
    {
        ItemID.Sets.Factory._size = Terraria.ModLoader.ItemLoader.ItemCount;
        ProjectileID.Sets.Factory._size = Terraria.ModLoader.ProjectileLoader.ProjectileCount;
        NPCID.Sets.Factory._size = Terraria.ModLoader.NPCLoader.NPCCount;
        BuffID.Sets.Factory._size = Terraria.ModLoader.BuffLoader.BuffCount;
        TileID.Sets.Factory._size = Terraria.ModLoader.TileLoader.TileCount;
        WallID.Sets.Factory._size = Terraria.ModLoader.WallLoader.WallCount;
    }

    public GameArray<int> CreateIntSet(int defaultState, params int[] inputs)
    {
        var values = GameArrayBuilder.Create<int>(inputs);
        using var pin = new GameHandle(values.Pointer);
        return CreateIntSet(defaultState, values);
    }
}
