using Terraria.ModLoader;

namespace ExampleMod.Common.Systems;

public class KeybindSystem : ModSystem
{
    public static ModKeybind RandomBuffKeybind { get; private set; }
    public static ModKeybind LearningExampleKeybind { get; private set; }

    public override void Load()
    {
        RandomBuffKeybind = KeybindLoader.RegisterKeybind(Mod, "RandomBuff", "P");
        LearningExampleKeybind = KeybindLoader.RegisterKeybind(Mod, "LearningExample", "O");
    }

    public override void Unload()
    {
        RandomBuffKeybind = null;
        LearningExampleKeybind = null;
    }
}
