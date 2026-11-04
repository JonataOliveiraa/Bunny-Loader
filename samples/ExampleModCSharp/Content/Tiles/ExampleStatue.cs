using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public class ExampleStatue : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileObsidianKill[Type] = true;
        Main.tileSpelunker[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileID.Sets.Wiring.IsAMechanism[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleMultiplier = 2;
        TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
        TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
        TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
        TileObjectData.addAlternate(1);
        TileObjectData.addTile(Type);

        DustType = DustID.Silver;

        AddMapEntry(new Color(144, 148, 144), Language.GetText("MapObject.Statue"));
    }

    public override void HitWire(int i, int j)
    {
        (int x, int y) = TileObjectData.TopLeft(i, j);

        const int TileWidth = 2;
        const int TileHeight = 3;

        for (int yy = y; yy < y + TileHeight; yy++)
        {
            for (int xx = x; xx < x + TileWidth; xx++)
            {
                Wiring.SkipWire(xx, yy);
            }
        }

        float spawnX = (x + TileWidth * 0.5f) * 16;
        float spawnY = (y + TileHeight * 0.65f) * 16;

        var entitySource = new EntitySource_TileBreak(x, y, "ExampleStatue");

        if (Main.rand.NextFloat() < .95f)
        {
            if (Wiring.CheckMech(x, y, 60) && Item.MechSpawn(spawnX, spawnY, ItemID.SilverCoin) && Item.MechSpawn(spawnX, spawnY, ItemID.GoldCoin) && Item.MechSpawn(spawnX, spawnY, ItemID.PlatinumCoin))
            {
                int id = ItemID.SilverCoin;

                if (Main.rand.NextBool(100))
                {
                    id++;

                    if (Main.rand.NextBool(100))
                    {
                        id++;
                    }
                }

                Item.NewItem(entitySource, new Vector2(spawnX, spawnY - 20), id);
            }
        }
        else
        {
            int npcIndex = -1;

            int spawnedNpcId = NPCID.Goldfish;

            if (Wiring.CheckMech(x, y, 30) && NPC.MechSpawn(spawnX, spawnY, spawnedNpcId))
            {
                npcIndex = NPC.NewNPC(entitySource, (int)spawnX, (int)spawnY - 12, spawnedNpcId);
            }

            if (npcIndex >= 0)
            {
                var npc = Main.npc[npcIndex];

                npc.value = 0f;
                npc.npcSlots = 0f;

                npc.SpawnedFromStatue = true;
                npc.CanBeReplacedByOtherNPCs = true;
            }
        }
    }
}
