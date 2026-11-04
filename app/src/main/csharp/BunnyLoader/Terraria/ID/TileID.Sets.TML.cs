using System;
using Terraria.ModLoader;

namespace Terraria.ID;

public readonly partial struct TileID
{
    public static partial class Sets
    {
        static bool[] countsAsHoneyForCrafting = Array.Empty<bool>();

        static bool[] countsAsLavaForCrafting = Array.Empty<bool>();

        static bool[] multiTileSway = Array.Empty<bool>();

        static bool[] clock = Array.Empty<bool>();

        static int[] openDoorID = Array.Empty<int>();

        static int[] closeDoorID = Array.Empty<int>();

        public static bool[] CountsAsHoneyForCrafting => Grow(ref countsAsHoneyForCrafting);

        public static bool[] CountsAsLavaForCrafting => Grow(ref countsAsLavaForCrafting);

        public static bool[] MultiTileSway => Grow(ref multiTileSway);

        public static bool[] Clock => Grow(ref clock, GrandfatherClocks);

        public static int[] OpenDoorID => GrowInt(ref openDoorID);

        public static int[] CloseDoorID => GrowInt(ref closeDoorID);

        static int[] GrowInt(ref int[] set)
        {
            int count = Math.Max(TileLoader.TileCount, Main.tileFrameImportant.Length);
            lock (Gate)
            {
                if (set.Length >= count) return set;
                int old = set.Length;
                Array.Resize(ref set, count);
                Array.Fill(set, -1, old, count - old);
                return set;
            }
        }
    }
}
