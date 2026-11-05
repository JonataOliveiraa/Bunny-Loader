using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Systems
{

    public class LanguageSubstitutionSystem : ModSystem
    {
        public override void Load() {

            Lang.RegisterModdedGlobalSubstitution(Mod, "ExampleBiome", () => ExampleConditions.InExampleBiome.IsMet());
            Lang.RegisterModdedGlobalSubstitution(Mod, "DownedMinionBoss", () => DownedBossSystem.downedMinionBoss);

        }
    }
}
