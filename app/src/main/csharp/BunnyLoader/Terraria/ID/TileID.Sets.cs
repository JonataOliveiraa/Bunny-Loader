using System;
using Terraria.ModLoader;

namespace Terraria.ID;

public readonly partial struct TileID
{
    public static partial class Sets
    {
        static readonly object Gate = new();

        public static int[] CountsAsPylon = new int[] { TeleportationPylon };

        static bool[] treeSapling = Array.Empty<bool>();

        public static bool[] TreeSapling
        {
            get
            {
                int count = Math.Max(TileLoader.TileCount, Main.tileFrameImportant.Length);
                lock (Gate)
                {
                    if (treeSapling.Length >= count) return treeSapling;
                    bool first = treeSapling.Length == 0;
                    Array.Resize(ref treeSapling, count);
                    if (first)
                    {
                        foreach (int type in new[] { Saplings, GemSaplings, VanityTreeSakuraSaplings, VanityTreeWillowSaplings })
                        {
                            if (type < count) treeSapling[type] = true;
                        }
                    }
                    return treeSapling;
                }
            }
        }
    }
}
