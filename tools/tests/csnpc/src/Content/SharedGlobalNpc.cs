using Terraria;
using Terraria.ModLoader;

namespace CsNpc.Content;

public sealed class SharedGlobalNpc : GlobalNPC
{
    public static int StaticCalls;

    public override void SetStaticDefaults() => StaticCalls++;
}
