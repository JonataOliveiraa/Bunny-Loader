using System.Collections.Generic;
using ExampleMod.Content.Tiles;
using Terraria;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ExampleMod.Common.Systems;

public sealed class ExampleOreSystem : ModSystem
{
    public static LocalizedText ExampleOrePassMessage { get; private set; }

    public override void SetStaticDefaults()
    {
        ExampleOrePassMessage = Mod.GetLocalization("WorldGen.ExampleOrePassMessage", () => "Generating example ore");
    }

    public override void ModifyWorldGenTasks(List<GenPass> tasks)
    {
        int index = tasks.FindIndex(pass => pass.Name == "Shinies");
        if (index >= 0)
            tasks.Insert(index + 1, new ExampleOrePass("Example Mod Ores", 237.4298));
    }
}

public sealed class ExampleOrePass : GenPass
{
    public ExampleOrePass(string name, double loadWeight) : base(name, loadWeight)
    {
    }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = ExampleOreSystem.ExampleOrePassMessage.Value;
        int count = (int)(Main.maxTilesX * Main.maxTilesY * 6E-05);
        for (int k = 0; k < count; k++)
        {
            int x = WorldGen.genRand.Next(0, Main.maxTilesX);
            int y = WorldGen.genRand.Next((int)GenVars.worldSurfaceLow, Main.maxTilesY);
            WorldGen.TileRunner(x, y, WorldGen.genRand.Next(3, 6), WorldGen.genRand.Next(2, 6), ModContent.TileType<ExampleOre>());
        }
    }
}
