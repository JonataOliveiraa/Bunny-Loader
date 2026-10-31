using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria.ID;

public readonly partial struct BuffID
{
    public readonly partial struct Sets
    {
        internal static bool[] TagBuffs = Array.Empty<bool>();
        internal static List<int>[] ImmunityParents = Array.Empty<List<int>>();

        public static ref bool[] IsATagBuff { get { NPCImmunity.Enable(); return ref TagBuffs; } }
        public static ref List<int>[] GrantImmunityWith { get { NPCImmunity.Enable(); return ref ImmunityParents; } }

        internal static void ResizeImmunity()
        {
            int count = BuffLoader.BuffCount, old = ImmunityParents.Length;
            Array.Resize(ref TagBuffs, count);
            Array.Resize(ref ImmunityParents, count);
            for (int i = old; i < count; i++) ImmunityParents[i] = new List<int>();
            if (old != 0) return;
            TagBuffs[310] = TagBuffs[362] = true;
            ImmunityParents[OnFire3].Add(OnFire);
            ImmunityParents[OnFire].Add(OnFire3);
            ImmunityParents[Frostburn2].Add(Frostburn);
            ImmunityParents[Frostburn].Add(Frostburn2);
            ImmunityParents[Poisoned].Add(Venom);
        }
    }
}
