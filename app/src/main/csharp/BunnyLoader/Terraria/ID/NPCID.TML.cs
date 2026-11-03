using System;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria.ID;

public readonly partial struct NPCID
{
    public static partial class Sets
    {
        static bool[] neverDropsResourcePickups = Array.Empty<bool>();

        public static bool[] NeverDropsResourcePickups
        {
            get
            {
                int count = NPCLoader.NPCCount;
                if (neverDropsResourcePickups.Length >= count) return neverDropsResourcePickups;
                bool first = neverDropsResourcePickups.Length == 0;
                Array.Resize(ref neverDropsResourcePickups, count);
                if (first)
                {
                    foreach (int type in new[] { MotherSlime, CorruptSlime, Slimer })
                    {
                        if (type < count) neverDropsResourcePickups[type] = true;
                    }
                }
                return neverDropsResourcePickups;
            }
        }

        internal static bool[] WhipImmunity = Array.Empty<bool>();
        internal static bool[] RegularImmunity = Array.Empty<bool>();
        internal static bool?[][] SpecificImmunity = Array.Empty<bool?[]>();

        public static ref bool[] ImmuneToWhipTags { get { NPCImmunity.Enable(); return ref WhipImmunity; } }
        public static ref bool[] ImmuneToRegularBuffs { get { NPCImmunity.Enable(); return ref RegularImmunity; } }
        public static ref bool?[][] SpecificDebuffImmunity { get { NPCImmunity.Enable(); return ref SpecificImmunity; } }
    }
}
