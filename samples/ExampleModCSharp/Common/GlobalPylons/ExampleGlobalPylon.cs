using ExampleMod.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Map;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalPylons
{
    public class ExampleGlobalPylon : GlobalPylon
    {
        public override bool? ValidTeleportCheck_PreNPCCount(TeleportPylonInfo pylonInfo, ref int defaultNecessaryNPCCount) {
            if (!Main.dayTime) {
                defaultNecessaryNPCCount = 0;
            }

            return base.ValidTeleportCheck_PreNPCCount(pylonInfo, ref defaultNecessaryNPCCount);
        }

        public override bool PreDrawMapIcon(ref MapOverlayDrawContext context, ref string mouseOverText, ref TeleportPylonInfo pylonInfo, ref bool isNearPylon, ref Color drawColor, ref float deselectedScale, ref float selectedScale) {
            if (!isNearPylon) {
                drawColor = Color.Lerp(drawColor, Color.Red, 0.75f);
            }

            return base.PreDrawMapIcon(ref context, ref mouseOverText, ref pylonInfo, ref isNearPylon, ref drawColor, ref deselectedScale, ref selectedScale);
        }

        public override bool? PreCanPlacePylon(int x, int y, int tileType, TeleportPylonType pylonType) {
            if (pylonType == TeleportPylonType.Victory) {
                return true;
            }

            if (pylonType == ModContent.PylonType<ExamplePylonTileAdvanced>()) {
                return null;
            }

            return base.PreCanPlacePylon(x, y, tileType, pylonType);
        }

        public override bool? ValidTeleportCheck_PreBiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData) {
            if (pylonInfo.TypeOfPylon == TeleportPylonType.Jungle) {
                return sceneData.EnoughTilesForJungle || sceneData.EnoughTilesForSnow ? true : null;
            }

            return base.ValidTeleportCheck_PreBiomeRequirements(pylonInfo, sceneData);
        }

        public override void PostValidTeleportCheck(TeleportPylonInfo destinationPylonInfo, TeleportPylonInfo nearbyPylonInfo, ref bool destinationPylonValid, ref bool validNearbyPylonFound, ref string errorKey) {
            if (validNearbyPylonFound && errorKey == "Net.CannotTeleportToPylonBecauseAccessingLihzahrdTempleEarly") {
                destinationPylonValid = true;
            }
        }
    }
}
