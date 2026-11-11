using ExampleMod.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace ExampleMod.Common
{
    public class ExampleMapLayer : ModMapLayer
    {
        public override Position GetDefaultPosition() => new Before(IMapLayer.Pings);

        public override void Draw(ref MapOverlayDrawContext context, ref string text) {
            const float scaleIfNotSelected = 1f;
            const float scaleIfSelected = scaleIfNotSelected * 2f;

            if (Main.dungeonX < 0 || Main.dungeonY < 0) {
                return;
            }

            var dungeonTexture = TextureAssets.NpcHeadBoss[19].Value;

            if (context.Draw(dungeonTexture, new Vector2(Main.dungeonX, Main.dungeonY), Color.White, new SpriteFrame(1, 1, 0, 0), scaleIfNotSelected, scaleIfSelected, Alignment.Center).IsMouseOver) {
                text = Language.GetTextValue("Bestiary_Biomes.TheDungeon");
            }
        }
    }

    public class ExampleMapLayerSystem : ModSystem
    {
        public override void NetSend(BinaryWriter writer) {
            writer.Write(Main.dungeonX);
            writer.Write(Main.dungeonY);
        }
        public override void NetReceive(BinaryReader reader) {
            Main.dungeonX = reader.ReadInt32();
            Main.dungeonY = reader.ReadInt32();
        }
    }

    public class ExampleItemMapLayer : ModMapLayer
    {
        public override Position GetDefaultPosition() => new Before(IMapLayer.Pings);

        public override IEnumerable<Position> GetModdedConstraints() {
            yield return new Before(ModContent.GetInstance<ExampleMapLayer>());
        }

        public override void Draw(ref MapOverlayDrawContext context, ref string text) {
            if (Main.mapStyle == 2) {
                return;
            }

            foreach (WorldItem item in Main.ActiveItems) {
                if (item.type == ModContent.ItemType<ExampleItem>()) {
                    Texture2D itemTexture = TextureAssets.Item[item.type].Value;

                    Vector2 tilePosition = item.position / 16f;

                    context.Draw(itemTexture, tilePosition, Alignment.Center);
                }
            }
        }
    }
}
