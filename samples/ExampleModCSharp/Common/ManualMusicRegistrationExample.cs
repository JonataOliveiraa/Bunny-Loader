using Terraria.ModLoader;

namespace ExampleMod.Common
{

    // An example ICustomAutoload showing off manual music loading.
    // Manual loading is rarely needed, as Bunny Loader registers a .wav, .ogg or .mp3 file as a music track the first time MusicLoader.GetMusicSlot asks for it.
    public sealed class ManualMusicRegistrationExample : ICustomAutoload
    {
        public static void Autoload(Mod mod) {

            // When registering music manually, you will have to provide an instance to your mod.
            // Since you're providing an instance of your mod, you should not start the path with your mod's name.
            // Accepted music formats are: .mp3, .ogg, and .wav files.
            // Do NOT add the file extension in your code when adding music!

            // MusicLoader.AddMusic(mod, "Assets/Music/MysteriousMystery");

            // An example of registration of Music Boxes can be found in 'Content/Items/Placeable/ExampleMusicBox.cs'.
        }
    }
}
