using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropOneByOne : NativeDropRule
{
    public struct Parameters
    {
        public int ChanceNumerator;
        public int ChanceDenominator;
        public int MinimumItemDropsCount;
        public int MaximumItemDropsCount;
        public int MinimumStackPerChunkBase;
        public int MaximumStackPerChunkBase;
        public int BonusMinDropsPerChunkPerPlayer;
        public int BonusMaxDropsPerChunkPerPlayer;
    }

    public DropOneByOne(int itemId, Parameters parameters) : base(N.DropOneByOne.New(itemId, new N.DropOneByOne.Parameters
    {
        ChanceNumerator = parameters.ChanceNumerator,
        ChanceDenominator = parameters.ChanceDenominator,
        MinimumItemDropsCount = parameters.MinimumItemDropsCount,
        MaximumItemDropsCount = parameters.MaximumItemDropsCount,
        MinimumStackPerChunkBase = parameters.MinimumStackPerChunkBase,
        MaximumStackPerChunkBase = parameters.MaximumStackPerChunkBase,
        BonusMinDropsPerChunkPerPlayer = parameters.BonusMinDropsPerChunkPerPlayer,
        BonusMaxDropsPerChunkPerPlayer = parameters.BonusMaxDropsPerChunkPerPlayer,
    }).Pointer, typeof(DropOneByOne)) { }
}
