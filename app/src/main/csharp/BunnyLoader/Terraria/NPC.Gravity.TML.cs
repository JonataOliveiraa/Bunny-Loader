using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct NPC
{
    public ref MultipliableFloat GravityMultiplier => ref NPCGravityHooks.Of(this).Multiplier;

    public ref MultipliableFloat MaxFallSpeedMultiplier => ref NPCGravityHooks.Of(this).MaxFallSpeed;

    public static float[] GravityWetMultipliers { get; } = { 2f / 3f, 2f / 3f, 1f / 3f, 1.5f / 3f };

    public static float[] MaxFallSpeedWetMultipliers { get; } = { 0.7f, 0.7f, 0.4f, 0.55f };
}
