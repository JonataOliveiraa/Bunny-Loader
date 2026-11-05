using Microsoft.Xna.Framework;
using Terraria.GameContent.UI;
using Terraria.ModLoader;

namespace ExampleMod.Content.EmoteBubbles
{
    public abstract class ModTownEmote : ModEmoteBubble
    {
        public override string Texture => "ExampleMod/Content/EmoteBubbles/NPCEmotes";

        public override void SetStaticDefaults() {
            AddToCategory(EmoteID.Category.Town);
        }

        public virtual int Row => 0;

        public override Rectangle? GetFrame() {
            return new Rectangle(EmoteBubble.frame * 34, 28 * Row, 34, 28);
        }

        public override Rectangle? GetFrameInEmoteMenu(int frame, int frameCounter) {
            return new Rectangle(frame * 34, 28 * Row, 34, 28);
        }
    }

    public class ExamplePersonEmote : ModTownEmote
    {
        public override void OnSpawn() {
            EmoteBubble.lifeTime *= 2;
            EmoteBubble.lifeTimeStart *= 2;
        }

        public override int Row => 0;
    }

    public class ExampleTravellingMerchantEmote : ModTownEmote
    {
        public override int Row => 1;
    }

    public class ExampleBoneMerchantEmote : ModTownEmote
    {
        public override int Row => 2;
    }
}
