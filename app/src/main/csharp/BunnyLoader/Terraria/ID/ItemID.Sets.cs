using System;

namespace Terraria.ID;

public readonly partial struct ItemID
{
    public static partial class Sets
    {
        static readonly object Gate = new();

        static bool[] preHardmodeLikeBossBag = Array.Empty<bool>();

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
