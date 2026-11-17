using System.Collections.Generic;
using ExampleMod.Content.Tiles;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ExampleMod.Content.Subworlds;

// A ModSubworld is a separate world the player can travel to and come back from, like another dimension. Use ExampleSubworldKey to visit this one.
// This is the simplest kind: a small, flat world generated from a single pass. It isn't Persistent, so it is generated again on every trip.
// See ExampleAdvancedSubworld for the other options, and ExampleWorldFileSubworld for a subworld loaded from a .wld file.
// The world's name comes from the Subworlds entries of the localization files.
public class ExampleSubworld : ModSubworld
{
    // The size of the world in tiles. Subworlds can be anywhere from 200 x 160 to 8400 x 2400.
    public override int Width => 1000;

    public override int Height => 500;

    // The passes that generate the world, just like the ones added in ModSystem.ModifyWorldGenTasks.
    // Before they run, the world is cleared and Main.worldSurface is set to 30% of the height.
    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Example Plains", (progress, configuration) =>
        {
            // A layer of grass on the surface, 12 tiles of dirt below it, and ExampleBlock all the way down.
            int surface = (int)Main.worldSurface;
            ushort exampleBlock = (ushort)ModContent.TileType<ExampleBlock>();
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                Main.tile[x, surface].ResetToType(TileID.Grass);
                for (int y = surface + 1; y < Main.maxTilesY; y++)
                    Main.tile[x, y].ResetToType(y < surface + 12 ? TileID.Dirt : exampleBlock);
            }
        }),
    };
}
