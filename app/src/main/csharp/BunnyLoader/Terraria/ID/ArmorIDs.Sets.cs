using System;
using Terraria.ModLoader.Core;

namespace Terraria.ID;

public readonly partial struct ArmorIDs
{
    public readonly partial struct Head
    {
        public readonly partial struct Sets
        {
            internal static readonly SlotSet FullHair = new(() => Count, false, ItemArmorDrawHooks.WantHair,
                10, 12, 28, 42, 62, 97, 106, 113, 116, 119, 133, 138, 139, 163, 178, 181, 191, 198, 217, 218, 220, 222, 224, 225, 228, 229, 230, 232,
                235, 238, 242, 243, 244, 245, 272, 273, 274, 277);

            internal static readonly SlotSet HatHair = new(() => Count, false, ItemArmorDrawHooks.WantHair,
                13, 14, 15, 16, 18, 21, 24, 25, 26, 29, 40, 44, 51, 56, 59, 60, 63, 64, 65, 67, 68, 69, 81, 92, 94, 95, 100, 114, 121, 126, 130, 136,
                140, 143, 145, 158, 159, 161, 182, 184, 190, 195, 215, 216, 219, 223, 226, 227, 231, 233, 234, 262, 263, 264, 265, 267, 275, 279, 280, 281);

            internal static readonly SlotSet BackHairWithoutHeadgear = new(() => Count, false, ItemArmorDrawHooks.WantHair, 0, 23, 259);

            public static bool[] DrawFullHair => FullHair.Values;

            public static bool[] DrawHatHair => HatHair.Values;

            public static bool[] DrawsBackHairWithoutHeadgear => BackHairWithoutHeadgear.Values;
        }
    }

    public readonly partial struct Body
    {
        public readonly partial struct Sets
        {
            internal static readonly SlotSet TopSkin = new(() => Count, false, ItemArmorDrawHooks.WantSkin, 21, 22, 82, 83, 93);

            internal static readonly SlotSet BottomSkin = new(() => Count, false, ItemArmorDrawHooks.WantSkin, 93);

            internal static readonly SlotSet Hands = new(() => Count, true, ItemArmorDrawHooks.WantSkin,
                77, 103, 41, 100, 10, 11, 12, 13, 14, 43, 15, 16, 20, 39, 50, 38, 40, 57, 44, 52, 53, 68, 81, 85, 88, 98, 86, 87, 99, 165, 166, 167, 171,
                45, 168, 169, 42, 180, 181, 183, 186, 187, 188, 64, 189, 191, 192, 198, 199, 202, 203, 58, 59, 60, 61, 62, 63, 36, 104, 184, 74, 78, 185,
                196, 197, 182, 76, 209, 210, 211, 213);

            internal static readonly SlotSet Arms = new(() => Count, false, ItemArmorDrawHooks.WantSkin, 83);

            public static bool[] HidesTopSkin => TopSkin.Values;

            public static bool[] HidesBottomSkin => BottomSkin.Values;

            public static bool[] HidesHands => Hands.Values;

            public static bool[] HidesArms => Arms.Values;
        }
    }

    public readonly partial struct Legs
    {
        public readonly partial struct Sets
        {
            internal static readonly SlotSet TopSkin = new(() => Count, false, ItemArmorDrawHooks.WantSkin);

            internal static readonly SlotSet BottomSkin = new(() => Count, false, ItemArmorDrawHooks.WantSkin, 20, 21, 216, 214, 215);

            public static bool[] HidesTopSkin => TopSkin.Values;

            public static bool[] HidesBottomSkin => BottomSkin.Values;
        }
    }

    internal sealed class SlotSet
    {
        readonly Func<int> count;
        readonly bool fill;
        readonly int[] flipped;
        readonly Action touched;
        bool[] values = Array.Empty<bool>();

        internal SlotSet(Func<int> count, bool fill, Action touched, params int[] flipped)
        {
            this.count = count;
            this.fill = fill;
            this.touched = touched;
            this.flipped = flipped;
        }

        internal bool[] Values
        {
            get
            {
                touched();
                int n = count();
                lock (this)
                {
                    if (values.Length >= n) return values;
                    int old = values.Length;
                    bool[] grown = new bool[n];
                    Array.Copy(values, grown, old);
                    for (int i = old; i < n; i++) grown[i] = fill;
                    foreach (int slot in flipped)
                    {
                        if (slot >= old && slot < n) grown[slot] = !fill;
                    }
                    values = grown;
                    return values;
                }
            }
        }

        internal bool this[int slot]
        {
            get
            {
                bool[] current = values;
                if ((uint)slot < (uint)current.Length) return current[slot];
                return slot >= 0 && Array.IndexOf(flipped, slot) >= 0 ? !fill : fill;
            }
        }
    }
}
