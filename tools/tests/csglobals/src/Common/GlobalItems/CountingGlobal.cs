using Terraria;
using Terraria.ModLoader;

namespace CsGlobals.Common.GlobalItems;

public class CountingGlobal : GlobalItem
{
    public static int StaticDefaults, SetDefaultsCalls;

    public override void SetStaticDefaults() => StaticDefaults++;

    public override void SetDefaults(Item entity) => SetDefaultsCalls++;
}
