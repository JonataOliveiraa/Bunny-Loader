using System;

namespace Terraria.ID;

public readonly partial struct ItemID
{
    public readonly partial struct Sets
    {
        static readonly object Gate = new();

        static bool[] preHardmodeLikeBossBag = Array.Empty<bool>();

        static bool[] spears = Array.Empty<bool>();

        public static bool[] Spears
        {
            get
            {
                int count = BossBag.Length;
                lock (Gate)
                {
                    if (spears.Length >= count) return spears;
                    bool first = spears.Length == 0;
                    Array.Resize(ref spears, count);
                    if (first)
                    {
                        foreach (int type in new[] { Spear, Trident, Swordfish, ThunderSpear, TheRottedFork, DarkLance, CobaltNaginata, PalladiumPike, MythrilHalberd, OrichalcumHalberd, AdamantiteGlaive, TitaniumTrident, ObsidianSwordfish, Gungnir, MushroomSpear, MonkStaffT1, MonkStaffT2, MonkStaffT3, ChlorophytePartisan, NorthPole, SlimeSpear })
                        {
                            if (type < count) spears[type] = true;
                        }
                    }
                    return spears;
                }
            }
        }

        public static bool[] PreHardmodeLikeBossBag
        {
            get
            {
                int count = BossBag.Length;
                lock (Gate)
                {
                    if (preHardmodeLikeBossBag.Length >= count) return preHardmodeLikeBossBag;
                    bool first = preHardmodeLikeBossBag.Length == 0;
                    Array.Resize(ref preHardmodeLikeBossBag, count);
                    if (first)
                    {
                        foreach (int type in new[] { KingSlimeBossBag, EyeOfCthulhuBossBag, EaterOfWorldsBossBag, BrainOfCthulhuBossBag, QueenBeeBossBag, SkeletronBossBag, DeerclopsBossBag })
                        {
                            if (type < count) preHardmodeLikeBossBag[type] = true;
                        }
                    }
                    return preHardmodeLikeBossBag;
                }
            }
        }
    }
}
