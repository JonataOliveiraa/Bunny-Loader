using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.CustomModType
{

    public class NonAutoloadVictoryPose : ModVictoryPose, ICustomAutoload
    {
        public static void Autoload(Mod mod) {

            mod.AddContent(new NonAutoloadVictoryPose("ShortPose", 120));
            mod.AddContent(new NonAutoloadVictoryPose("LongPose", 180));
        }

        private readonly string nameOverride;
        private readonly int duration;

        public override string Name => nameOverride;

        public override string Texture => "ExampleMod/Content/Items/ExampleTooltipsItem";

        public NonAutoloadVictoryPose(string name, int duration) {
            this.nameOverride = name;
            this.duration = duration;
        }

        public override void SetStaticDefaults() {
            PoseTime = duration;

            VictoryPoseID.Sets.NonBoss[Type] = true;
        }

        public override Rectangle? GetTextureFrame(Asset<Texture2D> texture) {

            return texture.Frame(1, 4, 0, Main.rand.Next(4));
        }

        public override void OnStartPose(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                Main.blockMouse = true;
            }
        }

        public override void Update(Player player) {
            if (player.itemAnimation == 0) {
                player.bodyFrame.Y = player.bodyFrame.Height;
            }
        }
    }
}
