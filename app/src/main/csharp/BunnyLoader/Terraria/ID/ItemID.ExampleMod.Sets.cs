using System;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria.ID;

public readonly partial struct ItemID
{
    public readonly partial struct Sets
    {
        static bool[] catchingTools = Array.Empty<bool>();
        static bool[] golfClubs = Array.Empty<bool>();
        public static bool[] CatchingTool
        {
            get
            {
                ItemExampleHooks.WantCatchingTools();
                lock (Gate)
                {
                    int count = ItemLoader.ItemCount;
                    if (catchingTools.Length < count) Array.Resize(ref catchingTools, count);
                    catchingTools[BugNet] = catchingTools[GoldenBugNet] = catchingTools[FireproofBugNet] = true;
                    return catchingTools;
                }
            }
        }
        public static bool[] IsAGolfClub
        {
            get
            {
                ItemExampleHooks.WantGolfItems();
                lock (Gate)
                {
                    if (golfClubs.Length < ItemLoader.ItemCount) Array.Resize(ref golfClubs, ItemLoader.ItemCount);
                    return golfClubs;
                }
            }
        }
    }
}
