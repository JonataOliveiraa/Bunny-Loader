using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Buffs
{

    public class AnimatedBuff : ModBuff
    {

        public static readonly int FrameCount = 4;
        public static readonly int AnimationSpeed = 60;
        public static readonly string AnimationSheetPath = "ExampleMod/Content/Buffs/AnimatedBuff_Animation";

        public static readonly int DamageBonus = 10;

        private Asset<Texture2D> animatedTexture;

        public override LocalizedText Description => base.Description.WithFormatArgs(DamageBonus);

        public override void SetStaticDefaults() {
            animatedTexture = ModContent.Request<Texture2D>(AnimationSheetPath);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, int buffIndex, ref BuffDrawParams drawParams) {

            Texture2D ourTexture = animatedTexture.Value;

            Rectangle ourSourceRectangle = ourTexture.Frame(verticalFrames: FrameCount, frameY: (int)Main.GameUpdateCount / AnimationSpeed % FrameCount);

            drawParams.Texture = ourTexture;
            drawParams.SourceRectangle = ourSourceRectangle;

            return true;

        }

        public override void Update(Player player, ref int buffIndex) {

            player.GetDamage<GenericDamageClass>() += DamageBonus / 100f;
        }
    }
}
