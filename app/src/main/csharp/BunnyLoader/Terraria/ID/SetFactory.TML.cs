using BunnyLoader.Interop;

namespace Terraria.ID;

public readonly partial struct SetFactory
{
    public GameArray<int> CreateIntSet(int defaultState, params int[] inputs)
    {
        var values = GameArrayBuilder.Create<int>(inputs);
        using var pin = new GameHandle(values.Pointer);
        return CreateIntSet(defaultState, values);
    }
}
