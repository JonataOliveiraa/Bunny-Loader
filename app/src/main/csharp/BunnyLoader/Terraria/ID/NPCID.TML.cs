using System;
using Terraria.ModLoader.Core;

namespace Terraria.ID;

public readonly partial struct NPCID
{
    public static partial class Sets
    {
        internal static bool[] WhipImmunity = Array.Empty<bool>();
        internal static bool[] RegularImmunity = Array.Empty<bool>();
        internal static bool?[][] SpecificImmunity = Array.Empty<bool?[]>();

        public static ref bool[] ImmuneToWhipTags { get { NPCImmunity.Enable(); return ref WhipImmunity; } }
        public static ref bool[] ImmuneToRegularBuffs { get { NPCImmunity.Enable(); return ref RegularImmunity; } }
        public static ref bool?[][] SpecificDebuffImmunity { get { NPCImmunity.Enable(); return ref SpecificImmunity; } }
    }
}
