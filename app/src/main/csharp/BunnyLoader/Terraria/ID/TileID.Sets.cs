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

        static bool[] isValidSpawnPoint = Array.Empty<bool>();

        public static bool[] IsValidSpawnPoint => Grow(ref isValidSpawnPoint, Beds);

        public static bool[] TreeSapling => Grow(ref treeSapling, Saplings, GemSaplings, VanityTreeSakuraSaplings, VanityTreeWillowSaplings);

        static bool[] Grow(ref bool[] set, params int[] vanilla)
        {
            int count = Math.Max(TileLoader.TileCount, Main.tileFrameImportant.Length);
            lock (Gate)
            {
                if (set.Length >= count) return set;
                bool first = set.Length == 0;
                Array.Resize(ref set, count);
                if (first)
                {
                    foreach (int type in vanilla)
                    {
                        if (type < count) set[type] = true;
                    }
                }
                return set;
            }
        }
    }
}
