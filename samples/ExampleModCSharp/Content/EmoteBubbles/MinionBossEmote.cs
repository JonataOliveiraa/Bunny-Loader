using ExampleMod.Common.Systems;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace ExampleMod.Content.EmoteBubbles
{
    public class MinionBossEmote : ModEmoteBubble
    {
        public override void SetStaticDefaults() {
            AddToCategory(EmoteID.Category.Bosses);
        }

        public override bool IsUnlocked() {
            return DownedBossSystem.downedMinionBoss;
        }
    }
}
