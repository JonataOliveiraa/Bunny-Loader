using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Biomes;

public class ExampleSurfaceBiome : ModBiome
{
    public override bool IsBiomeActive(Player player) => false;
}
