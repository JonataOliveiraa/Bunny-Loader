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

        static bool[] cannotDropSouls = Array.Empty<bool>();

        public static bool[] CannotDropSouls
        {
            get
            {
                int count = NPCLoader.NPCCount;
                if (cannotDropSouls.Length >= count) return cannotDropSouls;
                bool first = cannotDropSouls.Length == 0;
                Array.Resize(ref cannotDropSouls, count);
                if (first)
                {
                    foreach (int type in new[] { 1, 13, 14, 15, 121, 535 })
                    {
                        if (type < count) cannotDropSouls[type] = true;
                    }
                }
                return cannotDropSouls;
            }
        }

        static bool[] actsLikeTownNPC = Array.Empty<bool>(), noTownNPCHappiness = Array.Empty<bool>(), spawnsWithCustomName = Array.Empty<bool>(), cannotSitOnFurniture = Array.Empty<bool>(), isPetSmallForPetting = Array.Empty<bool>();
        static int[] playerDistanceWhilePetting = Array.Empty<int>();

        static T[] Sized<T>(ref T[] set)
        {
            int count = NPCLoader.NPCCount;
            if (set.Length < count) Array.Resize(ref set, count);
            return set;
        }

        public static bool[] ActsLikeTownNPC => Sized(ref actsLikeTownNPC);
        public static bool[] NoTownNPCHappiness => Sized(ref noTownNPCHappiness);
        public static bool[] SpawnsWithCustomName => Sized(ref spawnsWithCustomName);
        public static bool[] CannotSitOnFurniture => Sized(ref cannotSitOnFurniture);
        public static bool[] IsPetSmallForPetting => Sized(ref isPetSmallForPetting);
        public static int[] PlayerDistanceWhilePetting => Sized(ref playerDistanceWhilePetting);

        internal static bool[] WhipImmunity = Array.Empty<bool>();
        internal static bool[] RegularImmunity = Array.Empty<bool>();
        internal static bool?[][] SpecificImmunity = Array.Empty<bool?[]>();

        public static ref bool[] ImmuneToWhipTags { get { NPCImmunity.Enable(); return ref WhipImmunity; } }
        public static ref bool[] ImmuneToRegularBuffs { get { NPCImmunity.Enable(); return ref RegularImmunity; } }
        public static ref bool?[][] SpecificDebuffImmunity { get { NPCImmunity.Enable(); return ref SpecificImmunity; } }
    }
}
