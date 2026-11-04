using ExampleMod.Content.Items.Mounts;
using ExampleMod.Content.Pets.ExampleLightPet;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Systems;

public class ChestItemWorldGen : ModSystem
{
    public override void PostWorldGen()
    {
        int[] itemsToPlaceInFrozenChests = [ModContent.ItemType<ExampleMountItem>(), ModContent.ItemType<ExampleLightPetItem>(), ItemID.PinkJellyfishJar];

        int itemsToPlaceInFrozenChestsChoice = 0;

        int itemsPlaced = 0;
        int maxItems = 6;

        for (int chestIndex = 0; chestIndex < Main.maxChests; chestIndex++)
        {
            Chest chest = Main.chest[chestIndex];
            if (chest.IsNull)
            {
                continue;
            }
            Tile chestTile = Main.tile[chest.x, chest.y];

            if (chestTile.TileType == TileID.Containers && chestTile.TileFrameX == 11 * 36)
            {
                if (WorldGen.genRand.NextBool(3))
                    continue;

                for (int inventoryIndex = 0; inventoryIndex < chest.maxItems; inventoryIndex++)
                {
                    if (chest.item[inventoryIndex].type == ItemID.None)
                    {
                        chest.item[inventoryIndex].SetDefaults(itemsToPlaceInFrozenChests[itemsToPlaceInFrozenChestsChoice]);

                        itemsToPlaceInFrozenChestsChoice = (itemsToPlaceInFrozenChestsChoice + 1) % itemsToPlaceInFrozenChests.Length;

                        itemsPlaced++;
                        break;
                    }
                }
            }

            if (itemsPlaced >= maxItems)
            {
                break;
            }
        }
    }
}
