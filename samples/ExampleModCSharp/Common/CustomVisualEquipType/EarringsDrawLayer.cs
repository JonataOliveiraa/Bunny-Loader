using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ExampleMod.Common.CustomVisualEquipType
{

    public class EarringsDrawLayer : PlayerDrawLayer
    {
        public override bool IsHeadLayer => true;

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            return drawInfo.drawPlayer.GetModPlayer<EarringsPlayer>().earring > 0;
        }

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            Player drawPlayer = drawInfo.drawPlayer;
            EarringsPlayer earringsPlayer = drawPlayer.GetModPlayer<EarringsPlayer>();
            int earring = earringsPlayer.earring;
            var earringTexture = EarringsLoader.earringItemToTexture[earring];

            var drawPosition = new Vector2(
                (int)(drawInfo.Position.X - Main.screenPosition.X - drawPlayer.bodyFrame.Width / 2 + drawPlayer.width / 2),
                (int)(drawInfo.Position.Y - Main.screenPosition.Y + drawPlayer.height - drawPlayer.bodyFrame.Height + 4f)
            ) + drawPlayer.headPosition + drawInfo.headVect + Main.OffsetsPlayerHeadgear[drawPlayer.bodyFrame.Y / drawPlayer.bodyFrame.Height] * drawPlayer.gravDir;

            Rectangle drawFrame = drawPlayer.bodyFrame;
            drawFrame.Y = 0;

            var drawData = new DrawData(
                earringTexture.Value,
                drawPosition,
                drawFrame,
                drawInfo.colorArmorHead,
                drawPlayer.headRotation,
                drawInfo.headVect,
                1f,
                drawInfo.playerEffect
            );
            drawData.shader = earringsPlayer.earringShader;
            drawInfo.DrawDataCache.Add(drawData);
        }
    }
}
