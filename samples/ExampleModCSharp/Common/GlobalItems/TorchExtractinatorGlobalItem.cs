using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalItems;

public class TorchExtractinatorGlobalItem : GlobalItem
{
    public override void ExtractinatorUse(int extractType, int extractinatorBlockType, ref int resultType, ref int resultStack)
    {
        if (extractType != ItemID.Torch)
            return;

        resultStack = 1;
        if (extractinatorBlockType == TileID.ChlorophyteExtractinator && Main.rand.NextBool(10))
        {
            resultType = ItemID.UltrabrightTorch;
            return;
        }

        resultType = Main.rand.Next(TorchExtractinatorModSystem.TorchItems);
    }
}

public class TorchExtractinatorModSystem : ModSystem
{
    internal static List<int> TorchItems;

    public override void PostSetupContent()
    {
        ItemID.Sets.ExtractinatorMode[ItemID.Torch] = ItemID.Torch;
        TorchItems = new List<int>();

        for (int i = 0; i < ItemLoader.ItemCount; i++)
        {
            int createTile = ContentSamples.ItemsByType[i].createTile;
            if (createTile != -1 && TileID.Sets.Torches[createTile] && ItemID.Sets.ExtractinatorMode[i] == -1)
            {
                ItemID.Sets.ExtractinatorMode[i] = ItemID.Torch;
                TorchItems.Add(i);
            }
        }
    }
}
