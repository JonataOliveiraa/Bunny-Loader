using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.EmoteBubbles
{
    public class ExampleBiomeEmote : ModEmoteBubble
    {
        public override void SetStaticDefaults() {
            AddToCategory(EmoteID.Category.BiomesAndEvents);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle frame, Vector2 origin, SpriteEffects spriteEffects) {
            Texture2D bubbleTexture = TextureAssets.Extra[ExtrasID.EmoteBubble].Value;

            Rectangle bubbleFrame = bubbleTexture.Frame(8, 39, (EmoteBubble.lifeTime >= 6 && EmoteBubble.lifeTimeStart - EmoteBubble.lifeTime >= 6) ? 1 : 0);

            spriteBatch.Draw(bubbleTexture, position, bubbleFrame, Color.White, 0f, origin, 1f, spriteEffects, 0f);

            if (!(EmoteBubble.lifeTime >= 6 && EmoteBubble.lifeTimeStart - EmoteBubble.lifeTime >= 6)) {
                return false;
            }

            spriteBatch.Draw(texture, position, frame, Color.White, 0f, origin, 1f, spriteEffects, 0f);

            return false;
        }
    }
}
