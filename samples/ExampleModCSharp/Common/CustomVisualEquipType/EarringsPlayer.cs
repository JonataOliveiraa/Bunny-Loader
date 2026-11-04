using Terraria.ModLoader;

namespace ExampleMod.Common.CustomVisualEquipType
{

    public class EarringsPlayer : ModPlayer
    {

        public int earring;

        public int earringShader;

        public override void ResetEffects() {
            earring = 0;
            earringShader = 0;
        }
    }
}
