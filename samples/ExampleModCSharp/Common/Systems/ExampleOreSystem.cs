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

// ExampleOreSystem contains code related to spawning ExampleOre. It contains both spawning ore during world generation, seen in ModifyWorldGenTasks, and spawning ore after defeating a boss, seen in BlessWorldWithExampleOre and MinionBossBody.OnKill.
public sealed class ExampleOreSystem : ModSystem
{
    public static LocalizedText ExampleOrePassMessage { get; private set; }

    // This method is called from MinionBossBody.OnKill the first time the boss is killed.
    // The logic is located here for organizational purposes.
    public void BlessWorldWithExampleOre()
    {
        if ((Main.netMode & NetmodeID.MultiplayerClient) != 0 && (Main.netMode & NetmodeID.Server) == 0) return; // This should not happen, but just in case.

        // Broadcast a message to notify the user.
        if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(Mod.GetLocalization("WorldGen.BlessedWithExampleOreMessage").Value, 50, 255, 130);
        else ChatHelper.BroadcastChatMessage(Mod.GetLocalization("WorldGen.BlessedWithExampleOreMessage").ToNetworkText(), new Color(50, 255, 130));

        // 100 controls how many splotches of ore are spawned into the world, scaled by world size. For comparison, the first 3 times altars are smashed about 275, 190, or 120 splotches of the respective hardmode ores are spawned.
        int splotches = (int)(100 * (Main.maxTilesX / 4200f));
        int highestY = (int)Utils.Lerp(Main.rockLayer, Main.UnderworldLayer, 0.5);
        for (int iteration = 0; iteration < splotches; iteration++)
        {
            // Find a point in the lower half of the rock layer but above the underworld depth.
            int i = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
            int j = WorldGen.genRand.Next(highestY, Main.UnderworldLayer);

            // OreRunner will spawn ExampleOre in splotches. OnKill only runs on the server or single player, so it is safe to run world generation code.
            WorldGen.OreRunner(i, j, WorldGen.genRand.Next(5, 9), WorldGen.genRand.Next(5, 9), (ushort)ModContent.TileType<ExampleOre>());
        }
    }

    public override void SetStaticDefaults()
    {
        ExampleOrePassMessage = Mod.GetLocalization("WorldGen.ExampleOrePassMessage", () => "Generating example ore");
    }

    // World generation is explained more in https://github.com/tModLoader/tModLoader/wiki/World-Generation
    public override void ModifyWorldGenTasks(List<GenPass> tasks)
    {
        // Because world generation is like layering several images on top of each other, we need to do some steps between the original world generation steps.

        // Most vanilla ores are generated in a step called "Shinies", so for maximum compatibility, we will also do this.
        // First, we find out which step "Shinies" is.
        int index = tasks.FindIndex(pass => pass.Name == "Shinies");
        // Next, we insert our pass directly after the original "Shinies" pass.
        // ExampleOrePass is a class seen below
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
        // progress.Message is the message shown to the user while the following code is running.
        // Try to make your message clear. You can be a little bit clever, but make sure it is descriptive enough for troubleshooting purposes.
        progress.Message = ExampleOreSystem.ExampleOrePassMessage.Value;

        // Ores are quite simple, we simply use a for loop and the WorldGen.TileRunner to place splotches of the specified Tile in the world.
        // "6E-05" is "scientific notation". It simply means 0.00006 but in some ways is easier to read.
        int count = (int)(Main.maxTilesX * Main.maxTilesY * 6E-05);
        for (int k = 0; k < count; k++)
        {
            // The inside of this for loop corresponds to one single splotch of our Ore.
            // First, we randomly choose any coordinate in the world by choosing a random x and y value.
            int x = WorldGen.genRand.Next(0, Main.maxTilesX);

            // WorldGen.worldSurfaceLow is actually the highest surface tile. In practice you might want to use WorldGen.rockLayer or other WorldGen values.
            int y = WorldGen.genRand.Next((int)GenVars.worldSurfaceLow, Main.maxTilesY);

            // Then, we call WorldGen.TileRunner with random "strength" and random "steps", as well as the Tile we wish to place.
            // Feel free to experiment with strength and step to see the shape they generate.
            WorldGen.TileRunner(x, y, WorldGen.genRand.Next(3, 6), WorldGen.genRand.Next(2, 6), ModContent.TileType<ExampleOre>());

            // Alternately, we could check the tile already present in the coordinate we are interested.
            // Wrapping WorldGen.TileRunner in the following condition would make the ore only generate in Snow.
            // Tile tile = Framing.GetTileSafely(x, y);
            // if (tile.HasTile && tile.TileType == TileID.SnowBlock) {
            // 	WorldGen.TileRunner(.....);
            // }
        }
    }
}
