using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.CustomModType
{

    public class VictoryPoseLoader
    {
        internal static readonly List<ModVictoryPose> victoryPoses = [];

        public static IReadOnlyList<ModVictoryPose> VictoryPoses => victoryPoses;

        internal static int Add(ModVictoryPose victoryPose) {
            int type = victoryPoses.Count;
            victoryPoses.Add(victoryPose);
            return type;
        }

        public static bool StartPose(ModVictoryPose pose, bool forced = false) {
            if (Main.dedServ) {
                return false;
            }
            VictoryPosePlayer victoryPosePlayer = Main.LocalPlayer.GetModPlayer<VictoryPosePlayer>();
            bool success = victoryPosePlayer.StartPose(pose);
            if (success) {
                if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {


                }
            }
            return success;
        }

        public static void CancelPose() {
            VictoryPosePlayer victoryPosePlayer = Main.LocalPlayer.GetModPlayer<VictoryPosePlayer>();
            if (victoryPosePlayer.activeVictoryPose != null) {
                victoryPosePlayer.EndPose();

                if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {


                }
            }
        }

        public static ModVictoryPose GetActivePose(Player player) => player.GetModPlayer<VictoryPosePlayer>().activeVictoryPose;
    }
}
