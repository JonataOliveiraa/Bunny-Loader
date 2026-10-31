using System;

namespace Terraria.ID;

public class HairID
{
    public const int Count = 228;

    public static class Sets
    {
        public static bool[] DrawBackHair = Vanilla();

        static bool[] Vanilla()
        {
            var set = new bool[Count];
            for (int hair = 0; hair < Count; hair++)
            {
                set[hair] = hair > 50 && (hair < 56 || hair > 63) && (hair < 74 || hair > 77) && (hair < 88 || hair > 89) &&
                            hair != 94 && hair != 100 && hair != 104 && hair != 112 && hair < 116;
                if (hair is 133 or 134 or 146 or 162 or 6) set[hair] = true;
            }
            return set;
        }

        internal static void Resize(int count)
        {
            if (DrawBackHair.Length < count) Array.Resize(ref DrawBackHair, count);
        }
    }
}
