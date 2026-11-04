using Terraria;

namespace ExampleMod.Content.CustomModType
{

    public class HandsUpVictoryPose : ModVictoryPose
    {
        public override void SetStaticDefaults() {
            VictoryPoseID.Sets.NonBoss[Type] = true;
        }

        public override void OnStartPose(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                Main.blockMouse = true;
            }
        }

        public override void Update(Player player) {
            if (player.itemAnimation == 0) {
                player.bodyFrame.Y = player.bodyFrame.Height * 5;
            }
        }
    }
}
