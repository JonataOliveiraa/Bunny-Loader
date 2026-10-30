using System;
using BunnyLoader;
using Terraria.DataStructures;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCImmunity
{
    static readonly object Gate = new();
    [ThreadStatic] static bool resizing;

    internal static void Enable()
    {
        NPCLoader.WantDefaults();
        Resize();
    }

    internal static void Resize()
    {
        lock (Gate)
        {
            if (resizing) return;
            resizing = true;
            try
            {
                int count = NPCLoader.NPCCount, buffs = BuffLoader.BuffCount;
                int old = NPCID.Sets.SpecificImmunity.Length;
                if (old == count && (old == 0 || NPCID.Sets.SpecificImmunity[0].Length == buffs)) return;
                BuffID.Sets.ResizeImmunity();
                bool[] whips = new bool[count], regular = new bool[count];
                bool?[][] specific = new bool?[count][];
                Array.Copy(NPCID.Sets.WhipImmunity, whips, Math.Min(old, count));
                Array.Copy(NPCID.Sets.RegularImmunity, regular, Math.Min(old, count));
                var shimmer = NPCID.Sets.ShimmerImmunity;
                for (int type = 0; type < count; type++)
                {
                    bool?[] row = type < old ? NPCID.Sets.SpecificImmunity[type] : new bool?[buffs];
                    Array.Resize(ref row, buffs);
                    specific[type] = row;
                    if (type < old) continue;
                    var data = new NPCDebuffImmunityData(type < NPCID.Count ? Host.Native()->NPCImmunityData(type) : 0);
                    if (!data.IsNull)
                    {
                        whips[type] = data.ImmuneToWhips;
                        regular[type] = data.ImmuneToAllBuffsThatAreNotWhips;
                        if (!data.SpecificallyImmuneTo.IsNull)
                            foreach (int buff in data.SpecificallyImmuneTo.AsSpan())
                                if ((uint)buff < (uint)buffs) row[buff] = true;
                    }
                    row[BuffID.Shimmer] = type < NPCID.Count && !shimmer.IsNull && type < shimmer.Length && shimmer[type];
                }
                NPCID.Sets.WhipImmunity = whips;
                NPCID.Sets.RegularImmunity = regular;
                NPCID.Sets.SpecificImmunity = specific;
            }
            finally { resizing = false; }
        }
    }

    internal static void Apply(NPC npc)
    {
        if (resizing) return;
        Resize();
        int count = BuffLoader.BuffCount, type = npc.type;
        if ((uint)type >= (uint)NPCID.Sets.SpecificImmunity.Length) return;
        if (npc.buffImmune.Length != count) npc.buffImmune = npc.buffImmune.Resized(count);
        var immunity = npc.buffImmune;
        bool regular = NPCID.Sets.RegularImmunity[type], tags = NPCID.Sets.WhipImmunity[type];
        bool?[] specific = NPCID.Sets.SpecificImmunity[type];
        for (int buff = 0; buff < count; buff++)
            immunity[buff] = regular && !BuffID.Sets.TagBuffs[buff] || tags && BuffID.Sets.TagBuffs[buff];
        bool broad = regular || tags;
        for (int buff = 0; buff < count; buff++)
            if (specific[buff] == !broad) immunity[buff] = !broad;
        for (int buff = 0; buff < count; buff++)
        {
            var parents = BuffID.Sets.ImmunityParents[buff];
            if (parents.Count == 0) continue;
            bool inherited = broad;
            foreach (int parent in parents)
            {
                bool value = (uint)parent < (uint)count && immunity[parent];
                if (broad) inherited &= !value;
                else inherited |= value;
            }
            if (inherited) immunity[buff] = !broad;
        }
        for (int buff = 0; buff < count; buff++)
            if (specific[buff] == broad) immunity[buff] = broad;
    }
}
