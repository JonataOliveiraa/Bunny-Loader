using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.Systems
{
    public class SimpleDataAtParticularLocations : ModSystem
    {
        public PosData<byte>[] myMap;

        public override void ClearWorld() {
            myMap = [];
        }

        public override void SaveWorldData(TagCompound tag) {
            if (myMap.Length != 0) {
                tag["myMap"] = myMap.Select(info => new TagCompound {
                    ["pos"] = info.pos,
                    ["data"] = info.value
                }).ToList();
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            List<PosData<byte>> list = new List<PosData<byte>>();
            foreach (var entry in tag.GetList<TagCompound>("myMap")) {
                list.Add(new PosData<byte>(
                    entry.GetInt("pos"),
                    entry.Get<byte>("data")
                ));
            }
            myMap = list.ToArray();
        }

        public override void PostWorldGen() {
            var builder = new PosData<byte>.OrderedSparseLookupBuilder(compressEqualValues: false);

            int xCenter = Main.maxTilesX / 2;

            for (int y = 0; y < Main.maxTilesY; y++) {
                builder.Add(
                    xCenter, y,
                    (byte)(y % 255)
                );
            }

            myMap = builder.Build();
        }

        public override void PreUpdateWorld() {
            if (myMap.Length == 0) {
                return;
            }
            foreach (var player in Main.ActivePlayers) {
                UpdateFromNearestInMap(player);
            }
        }

        public void UpdateFromNearestInMap(Player player) {
            Point z = player.position.ToTileCoordinates();

            if (PosData.NearbySearchOrderedPosMap(myMap, z, 32, out var entry)) {
                var data = entry.value;

                for (int i = -2; i < 3; i++) {
                    for (int j = 0; j < 2; j++) {
                        Tile tile = Main.tile[z.X + i, z.Y + j];
                        if (tile.HasTile) {
                            tile.TileColor = data;
                        }
                    }
                }
            }
        }
    }
}
