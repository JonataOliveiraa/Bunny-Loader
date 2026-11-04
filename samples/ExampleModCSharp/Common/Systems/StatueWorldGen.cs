using ExampleMod.Content.Tiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ExampleMod.Common.Systems;

public class StatueWorldGen : ModSystem
{
    public override void Load()
    {
        On_WorldGen.SetupStatueList += On_WorldGen_SetupStatueList;
    }

    private void On_WorldGen_SetupStatueList(On_WorldGen.orig_SetupStatueList orig)
    {
        orig();

        int startIndex = GenVars.statueList.Length;

        (int type, bool shouldBeWired, ushort placeStyle)[] statueTypesToAdd = [
            (ModContent.TileType<ExampleStatue>(), false, 0),

        ];

        GenVars.statueList = GenVars.statueList.Resized(GenVars.statueList.Length + statueTypesToAdd.Length);

        for (int i = 0; i < statueTypesToAdd.Length; i++)
        {
            int arrayIndex = startIndex + i;
            (int statueType, bool shouldBeWired, ushort placeStyle) = statueTypesToAdd[i];

            GenVars.statueList[arrayIndex] = new Point16(statueType, placeStyle);

            if (shouldBeWired)
            {
                new BunnyLoader.Interop.GameList(GenVars.StatuesWithTraps.Pointer).Add(arrayIndex);
            }
        }
    }
}
