using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles
{
    public class ExamplePressurePlate : ModTile
    {
        public override void SetStaticDefaults() {
            TileID.Sets.PressurePlate[Type] = -3;

            TileID.Sets.Wiring.IsATrigger[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileObsidianKill[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.CoordinateHeights = [18];
            TileObjectData.newTile.CoordinatePadding = 0;
            TileObjectData.newTile.DrawYOffset = 2;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override bool IsTileDangerous(int i, int j, Player player) => true;

        public override void HitSwitch(int i, int j) {
            SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));
            Wiring.TripWire(i, j, 1, 1);
        }

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem) {
            if (!fail) {
                PressurePlateHelper.DestroyPlate(new Point(i, j));
            }
        }

        public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY) {
            offsetY += 2;
        }

        public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset) {
            if (PressurePlateHelper.IsPressed(new Point(i, j))) {
                frameXOffset += 18;
            }
        }
    }
}
