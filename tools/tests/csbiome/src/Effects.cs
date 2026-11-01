using System;
using Terraria;
using Terraria.ModLoader;

namespace CsBiome;

public sealed class TestBiome : ModBiome
{
    public static bool Active, Throw;
    public static int Enters, Leaves, InBiome, Visuals, VisualsInactive;

    public override int Music => 5;

    public override bool IsBiomeActive(Player player)
    {
        if (Throw) throw new InvalidOperationException("csbiome test exception");
        return Active;
    }

    public override void OnEnter(Player player) => Enters++;

    public override void OnInBiome(Player player) => InBiome++;

    public override void OnLeave(Player player) => Leaves++;

    public override void SpecialVisuals(Player player, bool isActive)
    {
        Visuals++;
        if (!isActive) VisualsInactive++;
    }
}

public sealed class OtherBiome : ModBiome
{
    public override bool IsBiomeActive(Player player) => false;
}

public sealed class EventEffect : ModSceneEffect
{
    public static bool Active;

    public override int Music => 7;

    public override SceneEffectPriority Priority => SceneEffectPriority.Event;

    public override bool IsSceneEffectActive(Player player) => Active;
}
