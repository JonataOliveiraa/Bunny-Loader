using ExampleMod.Content;
using ExampleMod.Content.Items.Accessories;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{

    public class ExampleInfoDisplayPlayer : ModPlayer
    {

        public bool showMinionCount;

        public override void ResetInfoAccessories() {
            showMinionCount = false;
        }

        public override void RefreshInfoAccessoriesFromTeamPlayers(Player otherPlayer) {
            if (otherPlayer.GetModPlayer<ExampleInfoDisplayPlayer>().showMinionCount) {
                showMinionCount = true;
            }
        }
    }
}
