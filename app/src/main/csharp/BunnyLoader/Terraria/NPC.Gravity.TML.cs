using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct NPC
{
    public ref MultipliableFloat GravityMultiplier => ref NPCGravityHooks.Of(this).Multiplier;
}
