using System.Collections.Generic;
using ExampleMod.Content.Tiles;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ExampleMod.Content.Subworlds;

public class ExampleSubworld : ModSubworld
{
    public override int Width => 1000;

    public override int Height => 500;

    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Example Plains", (progress, configuration) =>
        {
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
