using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;

namespace ExampleMod.Content.BossBars
{
    public class ExampleBossBarStyle : ModBossBarStyle
    {
        public override bool PreventDraw => true;

        public override void Draw(SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info) {
            if (currentBar.IsNull) {
                return;
            }

            if (CommonBossBigProgressBar.Is(currentBar)) {
                NPC npc = Main.npc[info.npcIndexToAimAt];
                float lifePercent = Utils.Clamp(npc.life / (float)npc.lifeMax, 0f, 1f);

                BigProgressBarHelper.DrawBareBonesBar(spriteBatch, lifePercent);

                if (info.showText && BigProgressBarSystem.ShowText) {
                    Rectangle barDimensions = Utils.CenteredRectangle(new Vector2(Main.ScreenSize.X * 0.5f, Main.ScreenSize.Y) + new Vector2(0f, -50f), new Vector2(400f, 20f));
                    BigProgressBarHelper.DrawHealthText(spriteBatch, barDimensions, npc.life, npc.lifeMax);
                }
            }
            else {
                currentBar.Draw(ref info, spriteBatch);
            }
        }
    }
}
