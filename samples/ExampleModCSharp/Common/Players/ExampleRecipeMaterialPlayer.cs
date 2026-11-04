using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{

    public class ExampleRecipeMaterialPlayer : ModPlayer
    {
        private int _chestIndexNearby = -1;

        public override void PostUpdateMiscEffects() {
            if (Main.dedServ) {

                return;
            }

            int oldChestIndex = _chestIndexNearby;

            var legPosition = Player.Bottom - new Vector2(0f, 20f);
            var legPositionInTile = legPosition.ToTileCoordinates();

            _chestIndexNearby = -1;

            for (int x = -1; x <= 1; x++) {
                var pos = new Point(legPositionInTile.X + x, legPositionInTile.Y);
                if (!WorldGen.InWorld(pos.X, pos.Y)) {
                    continue;
                }

                var tile = Main.tile[pos.X, pos.Y];

                if (!tile.HasTile || !TileID.Sets.IsAContainer[tile.TileType] || tile.TileType is TileID.Dressers) {
                    continue;
                }

                if (tile.TileFrameX % 36 != 0) {
                    pos.X--;
                }

                if (tile.TileFrameY != 0) {
                    pos.Y--;
                }

                int chestIndex = Chest.FindChest(pos.X, pos.Y);
                if (chestIndex > -1 && !Chest.IsLocked(pos.X, pos.Y)) {
                    Chest chest = Main.chest[chestIndex];

                    if (!chest.IsNull) {
                        _chestIndexNearby = chestIndex;
                        break;
                    }
                }
            }
        }

        public override IEnumerable<Item> AddMaterialsForCrafting(out ItemConsumedCallback itemConsumedCallback) {

            if (_chestIndexNearby is -1 || Player.chest == _chestIndexNearby || Main.chest[_chestIndexNearby].IsNull)
                return base.AddMaterialsForCrafting(out itemConsumedCallback);

            int chestIndex = _chestIndexNearby;
            Chest chest = Main.chest[chestIndex];
            itemConsumedCallback = (item, index) => {
                chest.SetItem(index, item);
                if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {

                    NetMessage.SendData(MessageID.SyncChestItem, number: chestIndex, number2: index);
                }
            };

            var materials = new Item[chest.item.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = chest.GetItem(i);
            return materials;
        }
    }
}
