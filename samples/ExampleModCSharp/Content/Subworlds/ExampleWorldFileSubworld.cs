using Terraria.ModLoader;

namespace ExampleMod.Content.Subworlds;

// Instead of generating a world, a subworld can load a world built in the game. Use ExampleWorldFileSubworldKey to visit this one.
// The size, the spawn point and everything in the world come from the file, so Width, Height and GenerationTasks aren't needed.
// Blocks, walls and NPCs from mods are kept in files beside the world (ExampleArena.wld.tiles.bl and so on), which must be placed next to the .wld as well.
public class ExampleWorldFileSubworld : ModSubworld
{
    // The path of the .wld inside the mod. Since this subworld isn't Persistent, the world is loaded fresh from this file on every trip.
    public override string WorldFile => "Content/Subworlds/ExampleArena.wld";
}
