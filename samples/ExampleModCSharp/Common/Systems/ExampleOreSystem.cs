using System.Collections.Generic;
using ExampleMod.Content.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ExampleMod.Common.Systems;

public sealed class ExampleOreSystem : ModSystem
{
    public static LocalizedText ExampleOrePassMessage { get; private set; }

    public void BlessWorldWithExampleOre()
    {
        if ((Main.netMode & NetmodeID.MultiplayerClient) != 0 && (Main.netMode & NetmodeID.Server) == 0) return;

        if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(Mod.GetLocalization("WorldGen.BlessedWithExampleOreMessage").Value, 50, 255, 130);
        else ChatHelper.BroadcastChatMessage(Mod.GetLocalization("WorldGen.BlessedWithExampleOreMessage").ToNetworkText(), new Color(50, 255, 130));

        int splotches = (int)(100 * (Main.maxTilesX / 4200f));
        int highestY = (int)Utils.Lerp(Main.rockLayer, Main.UnderworldLayer, 0.5);
        for (int iteration = 0; iteration < splotches; iteration++)
        {
            int i = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
            int j = WorldGen.genRand.Next(highestY, Main.UnderworldLayer);
            WorldGen.OreRunner(i, j, WorldGen.genRand.Next(5, 9), WorldGen.genRand.Next(5, 9), (ushort)ModContent.TileType<ExampleOre>());
        }
    }

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
